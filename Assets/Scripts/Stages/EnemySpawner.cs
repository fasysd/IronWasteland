using System;
using System.Collections.Generic;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Sinh quai cho man choi. Component nay SO HUU thiet ke dot quai cua man:
    /// danh sach <see cref="EnemyWave"/> (dot nao, luc nao, quai gi) va cac
    /// <see cref="SpawnZone"/> de biet quai roi o dau.
    ///
    /// Truoc day phan thiet ke nay nam trong asset <c>StageDefinition</c> va
    /// <see cref="StageDirector"/> tu bam gio tung dot. Nay StageDirector chi con
    /// nhiem vu dieu kien thang/thua; spawner tu chay lich wave qua
    /// <see cref="WaveTimeline"/> khi duoc goi BeginWaves()/TickWaves().
    ///
    /// Vi sao can Spawner thay vi dat quai san tren map:
    /// - Quai phai xuat hien theo dot (wave) va theo thoi gian, khong the dat tay.
    /// - Moi lan spawn phai Clone() chi so (EnemyStats) roi moi chinh, neu dung chung
    ///   1 instance thi HP se bi nhan don (SetCoopMode nhan doi, SpeedMultiplier nhan)
    ///   => quai thu 2, thu 3 manh dan bat thuong.
    ///
    /// Luu y ve pooling: nhieu brain quai hien tai tu goi Destroy(gameObject) trong
    /// OnDeath() (vi du NormalEnemy_BaseChaser), nen object da chet KHONG tai su dung duoc.
    /// Spawner van giu pool va chi tai su dung object con song; object da bi huy duoc
    /// bo qua (Unity tra ve null gia). Khi nao cac brain thoi tu huy thi pool se co tac dung.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Thiết kế đợt quái của màn")]
        [Tooltip("Đợt thường bắt đầu theo StartTime. Cửa dùng thứ tự trong danh sách để chọn đợt bị khóa.")]
        [SerializeField] private List<EnemyWave> waves = new List<EnemyWave>();

        [Tooltip("Các vùng sinh quái trên map. Đợt nào khai báo ZoneId thì quái rơi trong vùng đó.")]
        [SerializeField] private List<SpawnZone> spawnZones = new List<SpawnZone>();

        [Header("Gioi han")]
        [Tooltip("So quai song toi da cung luc (0 = khong gioi han).")]
        [SerializeField, Min(0)] private int maxAlive = 64;

        [Header("Tuy chon")]
        [Tooltip("Cha cua cac object quai duoc sinh ra.")]
        [SerializeField] private Transform container;

        [Tooltip("Che do Co-op: nhan doi mau toi da cua quai (theo GDD).")]
        [SerializeField] private bool coopMode = false;

        [SerializeField] private bool logEvents = false;

        private readonly Dictionary<EnemyBrain, Stack<EnemyBrain>> m_Pools = new Dictionary<EnemyBrain, Stack<EnemyBrain>>();
        private readonly Dictionary<EnemyBrain, Action> m_DeathHandlers = new Dictionary<EnemyBrain, Action>();
        private readonly Dictionary<EnemyBrain, EnemyBrain> m_SourcePrefabs = new Dictionary<EnemyBrain, EnemyBrain>();
        private readonly List<EnemyBrain> m_Active = new List<EnemyBrain>();
        private readonly List<int> m_DueWaves = new List<int>();
        private readonly Dictionary<int, StageWaveGate> m_WaveGates = new Dictionary<int, StageWaveGate>();
        private readonly HashSet<int> m_StartedWaves = new HashSet<int>();
        private readonly Dictionary<EnemyBrain, int> m_EnemyWaves = new Dictionary<EnemyBrain, int>();
        private readonly Dictionary<int, int> m_LivingWaveEnemies = new Dictionary<int, int>();

        private WaveTimeline m_Timeline;
        private bool m_WavesRunning;
        private int m_RemoteWavesFired;

        /// <summary>Vua sinh ra 1 quai (da Initialize xong).</summary>
        public event Action<EnemyBrain> EnemySpawned;

        /// <summary>Mot quai vua chet (truoc khi bi tra ve pool).</summary>
        public event Action<EnemyBrain> EnemyDied;

        /// <summary>Một đợt quái vừa được kích hoạt (index trong list waves, dữ liệu đợt).</summary>
        public event Action<int, EnemyWave> WaveStarted;

        public event Action<int> WaveCleared;

        public int AliveCount => m_Active.Count;
        public IReadOnlyList<EnemyBrain> Active => m_Active;
        public bool IsFull => maxAlive > 0 && m_Active.Count >= maxAlive;

        /// <summary>Thiết kế đợt quái của màn (do component này sở hữu).</summary>
        public IReadOnlyList<EnemyWave> Waves => waves;

        /// <summary>Các vùng sinh quái của màn.</summary>
        public IReadOnlyList<SpawnZone> SpawnZones => spawnZones;

        public int TotalWaves => waves != null ? waves.Count : 0;

        /// <summary>Index đợt vừa bắn gần nhất, -1 = chưa bắn đợt nào.</summary>
        public int WaveIndex { get; private set; } = -1;

        /// <summary>Lịch wave đang chạy hay không.</summary>
        public bool WavesRunning => m_WavesRunning;

        internal bool AllGatedWavesStarted
        {
            get
            {
                foreach (int index in m_WaveGates.Keys)
                    if (!m_StartedWaves.Contains(index)) return false;

                return true;
            }
        }

        internal bool RegisterWaveGate(StageWaveGate gate)
        {
            int index = gate.NextWaveIndex;
            if (gate.CompletedWaveIndex < 0 || index >= TotalWaves || gate.CompletedWaveIndex >= index ||
                waves[index] == null || m_WaveGates.ContainsKey(index))
                return false;

            m_WaveGates.Add(index, gate);
            return true;
        }

        internal void UnregisterWaveGate(StageWaveGate gate)
        {
            if (m_WaveGates.TryGetValue(gate.NextWaveIndex, out StageWaveGate registered) && registered == gate)
                m_WaveGates.Remove(gate.NextWaveIndex);
        }

        internal bool TryStartGatedWave(StageWaveGate gate)
        {
            return m_WavesRunning &&
                m_WaveGates.TryGetValue(gate.NextWaveIndex, out StageWaveGate registered) && registered == gate &&
                m_LivingWaveEnemies.TryGetValue(gate.CompletedWaveIndex, out int remaining) && remaining == 0 &&
                SpawnWave(gate.NextWaveIndex);
        }

        internal void ApplyRemoteWaveCount(int count)
        {
            m_RemoteWavesFired = Mathf.Clamp(count, 0, TotalWaves);
        }

        /// <summary>Tổng số quái mà thiết kế của màn sẽ sinh ra.</summary>
        public int TotalEnemyCount
        {
            get
            {
                int total = 0;
                if (waves == null) return total;
                for (int i = 0; i < waves.Count; i++)
                {
                    if (waves[i] != null) total += waves[i].TotalCount;
                }
                return total;
            }
        }

        public bool CoopMode
        {
            get => coopMode;
            set => coopMode = value;
        }

        private void Awake()
        {
            if (container == null) container = transform;
        }

        /// <summary>Gán thiết kế đợt quái + vùng spawn bằng code (migration, test, tool editor).</summary>
        public void ConfigureDesign(IEnumerable<EnemyWave> design, IEnumerable<SpawnZone> zones = null)
        {
            if (design != null)
            {
                waves.Clear();
                waves.AddRange(design);
            }

            if (zones != null)
            {
                spawnZones.Clear();
                spawnZones.AddRange(zones);
            }
        }

        /// <summary>Chuẩn bị lịch wave. Gọi từ StageDirector khi màn bắt đầu.</summary>
        public void BeginWaves()
        {
            if (m_WavesRunning) return;

            m_WavesRunning = true;
            WaveIndex = -1;
            foreach (StageWaveGate gate in m_WaveGates.Values) gate.ResetGate();

            int count = TotalWaves;
            var startTimes = new List<float>(count);
            for (int i = 0; i < count; i++)
            {
                EnemyWave wave = waves[i];
                startTimes.Add(wave != null && wave.StartRadar == null ? Mathf.Max(0f, wave.StartTime) : 0f);
            }

            m_Timeline = new WaveTimeline(startTimes);

            if (logEvents)
            {
                Debug.Log($"[EnemySpawner] Lịch {count} đợt quái - {TotalEnemyCount} con trên {spawnZones.Count} vùng spawn.", this);
            }
        }

        /// <summary>Số đợt đã bắt đầu theo giờ hoặc qua cửa, dùng cho HUD.</summary>
        public int WavesFired => Mathf.Max(m_StartedWaves.Count, m_RemoteWavesFired);

        /// <summary>
        /// Đẩy thời gian cho lịch wave. Tách khỏi Update() để StageDirector (hoặc test)
        /// điều khiển thời gian và có thể pause mà không phải sửa logic.
        /// </summary>
        public void TickWaves(float deltaTime)
        {
            if (!m_WavesRunning || m_Timeline == null) return;

            m_Timeline.Advance(deltaTime);

            m_Timeline.TakeDue(m_DueWaves);
            for (int i = 0; i < m_DueWaves.Count;)
            {
                int index = m_DueWaves[i];
                if (m_WaveGates.ContainsKey(index))
                {
                    m_DueWaves.RemoveAt(i);
                    continue;
                }

                RadarStation radar = waves[index]?.StartRadar;
                if (radar != null && !radar.HasStartedActivation)
                {
                    i++;
                    continue;
                }

                // Giữ lại đợt đến giờ nếu người chơi chưa đi qua cửa của đợt trước.
                bool waitingForGate = false;
                foreach (int gatedIndex in m_WaveGates.Keys)
                {
                    if (gatedIndex >= index || m_StartedWaves.Contains(gatedIndex)) continue;
                    waitingForGate = true;
                    break;
                }
                if (waitingForGate)
                {
                    i++;
                    continue;
                }

                m_DueWaves.RemoveAt(i);
                SpawnWave(index);
            }
        }

        /// <summary>Dừng lịch wave (đổi màn / thua).</summary>
        public void StopWaves()
        {
            m_WavesRunning = false;
            m_Timeline = null;
            m_DueWaves.Clear();
            m_StartedWaves.Clear();
            m_EnemyWaves.Clear();
            m_LivingWaveEnemies.Clear();
            m_RemoteWavesFired = 0;
            WaveIndex = -1;
        }

        private bool SpawnWave(int index)
        {
            if (waves == null || index < 0 || index >= waves.Count) return false;

            if (!m_StartedWaves.Add(index)) return false;
            WaveIndex = index;
            EnemyWave wave = waves[index];
            if (wave == null) return true;

            int spawned = 0;
            if (wave.Spawns != null)
            {
                for (int i = 0; i < wave.Spawns.Count; i++)
                {
                    EnemyWaveSpawn spawn = wave.Spawns[i];
                    if (spawn == null || spawn.Enemy == null) continue;

                    SpawnZone zone = PickZone(spawn.ZoneId);
                    int count = Mathf.Max(1, spawn.Count);
                    for (int c = 0; c < count; c++)
                    {
                        Vector2 position = zone != null ? zone.RandomPoint() : (Vector2)transform.position;
                        EnemyBrain enemy = Spawn(spawn.Enemy, position, spawn.SpeedMultiplier);
                        if (enemy == null) continue;
                        m_EnemyWaves[enemy] = index;
                        spawned++;
                    }
                }
            }

            if (spawned > 0) m_LivingWaveEnemies[index] = spawned;
            WaveStarted?.Invoke(index, wave);
            foreach (StageWaveGate gate in m_WaveGates.Values) gate.PublishState();

            if (logEvents)
            {
                Debug.Log($"[EnemySpawner] Đợt {index + 1}/{TotalWaves} '{wave.Label}' - {wave.TotalCount} quái tại {m_Timeline.Elapsed:0.0}s");
            }
            return true;
        }

        private SpawnZone PickZone(string zoneId)
        {
            if (spawnZones == null || spawnZones.Count == 0) return null;

            SpawnZone fallback = null;
            int matchCount = 0;
            for (int i = 0; i < spawnZones.Count; i++)
            {
                SpawnZone zone = spawnZones[i];
                if (zone == null) continue;
                if (fallback == null) fallback = zone;
                if (zone.Matches(zoneId)) matchCount++;
            }

            if (matchCount == 0) return fallback;

            int pick = UnityEngine.Random.Range(0, matchCount);
            for (int i = 0; i < spawnZones.Count; i++)
            {
                SpawnZone zone = spawnZones[i];
                if (zone == null || !zone.Matches(zoneId)) continue;
                if (pick-- == 0) return zone;
            }
            return fallback;
        }

        /// <summary>Tao truoc object de luc vao man khong bi giat (khong bat buoc).</summary>
        public void Prewarm(EnemyBrain prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            Stack<EnemyBrain> pool = GetPool(prefab);
            for (int i = 0; i < count; i++)
            {
                EnemyBrain instance = Instantiate(prefab, container);
                instance.name = prefab.name + "_pooled";
                instance.gameObject.SetActive(false);
                m_SourcePrefabs[instance] = prefab;
                pool.Push(instance);
            }
        }

        /// <summary>
        /// Sinh 1 quai. Tra ve null neu khong con cho (IsFull) hoac prefab rong.
        /// </summary>
        public EnemyBrain Spawn(EnemyBrain prefab, Vector2 position, float speedMultiplier = 1f)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[EnemySpawner] Prefab rong, khong spawn duoc.", this);
                return null;
            }

            if (IsFull)
            {
                if (logEvents) Debug.Log($"[EnemySpawner] Da dat gioi han {maxAlive} quai, bo qua lan spawn nay.");
                return null;
            }

            // Client trong phiên mạng không tự spawn quái; host spawn qua NGO.
            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive &&
                Unity.Netcode.NetworkManager.Singleton != null &&
                !Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                return null;
            }

            // BAY QUAN TRONG: phai Clone() chi so truoc khi chinh.
            // Dung chung instance se lam HP/ATK bi nhan don qua tung lan spawn.
            EnemyStats stats = prefab.Stats != null ? prefab.Stats.Clone() : new EnemyStats();
            stats.MoveSpeed *= Mathf.Max(0.01f, speedMultiplier);
            if (coopMode || IronWasteland.Tanks.TankBrain.NetworkSessionActive || IronWasteland.UI.CoopEntry.IsCoop)
            {
                stats.MaxHealth *= 2f;
            }

            EnemyBrain enemy;
            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive && IronWasteland.Net.NetSpawnService.Instance != null)
            {
                enemy = IronWasteland.Net.NetSpawnService.Instance.SpawnEnemy(prefab, position, stats);
                if (enemy == null) return null;
            }
            else
            {
                enemy = TakeFromPool(prefab);
                if (enemy == null)
                {
                    enemy = Instantiate(prefab, position, Quaternion.identity, container);
                    enemy.name = prefab.name + "_spawned";
                }

                enemy.transform.SetPositionAndRotation(position, Quaternion.identity);
                if (!enemy.gameObject.activeSelf) enemy.gameObject.SetActive(true);
                enemy.Initialize(stats);
            }

            m_SourcePrefabs[enemy] = prefab;
            TrackDeath(enemy);
            m_Active.Add(enemy);

            EnemySpawned?.Invoke(enemy);
            if (logEvents) Debug.Log($"[EnemySpawner] +{enemy.DisplayName} tai {position} (song {m_Active.Count}/{maxAlive})");
            return enemy;
        }

        /// <summary>Thu hoi thu cong (dung khi chuyen man hoac test).</summary>
        public void Despawn(EnemyBrain enemy)
        {
            if (enemy == null) return;
            ReturnToPool(enemy);
        }

        /// <summary>Thu hoi toan bo quai dang song.</summary>
        public void DespawnAll()
        {
            for (int i = m_Active.Count - 1; i >= 0; i--)
            {
                EnemyBrain enemy = m_Active[i];
                if (enemy != null) ReturnToPool(enemy);
            }
            m_Active.Clear();
        }

        private void OnDisable()
        {
            DespawnAll();
        }

        private Stack<EnemyBrain> GetPool(EnemyBrain prefab)
        {
            if (!m_Pools.TryGetValue(prefab, out Stack<EnemyBrain> pool))
            {
                pool = new Stack<EnemyBrain>();
                m_Pools[prefab] = pool;
            }
            return pool;
        }

        private EnemyBrain TakeFromPool(EnemyBrain prefab)
        {
            if (!m_Pools.TryGetValue(prefab, out Stack<EnemyBrain> pool)) return null;
            while (pool.Count > 0)
            {
                EnemyBrain candidate = pool.Pop();
                // Object da bi Destroy() o noi khac se so sanh bang null (Unity null gia).
                if (candidate != null) return candidate;
            }
            return null;
        }

        private void TrackDeath(EnemyBrain enemy)
        {
            if (m_DeathHandlers.ContainsKey(enemy)) return;

            Action handler = null;
            handler = () => HandleDeath(enemy);
            m_DeathHandlers[enemy] = handler;
            enemy.OnDeathEvent += handler;
        }

        private void UntrackDeath(EnemyBrain enemy)
        {
            if (!m_DeathHandlers.TryGetValue(enemy, out Action handler)) return;
            if (enemy != null) enemy.OnDeathEvent -= handler;
            m_DeathHandlers.Remove(enemy);
        }

        private void HandleDeath(EnemyBrain enemy)
        {
            // EnemyBrain.OnDeathEvent khong co tham so nen handler duoc tao theo tung instance.
            if (enemy == null) return;
            bool fromWave = m_EnemyWaves.Remove(enemy, out int index);
            EnemyDied?.Invoke(enemy);
            ReturnToPool(enemy);
            if (fromWave && m_LivingWaveEnemies.TryGetValue(index, out int remaining))
            {
                m_LivingWaveEnemies[index] = remaining - 1;
                if (remaining == 1) WaveCleared?.Invoke(index);
            }
        }

        private void ReturnToPool(EnemyBrain enemy)
        {
            m_EnemyWaves.Remove(enemy);
            m_Active.Remove(enemy);
            UntrackDeath(enemy);

            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive)
            {
                m_SourcePrefabs.Remove(enemy);
                if (enemy != null)
                {
                    IronWasteland.Net.NetEntity.Despawn(enemy.gameObject);
                }
                return;
            }

            if (enemy == null)
            {
                m_SourcePrefabs.Remove(enemy);
                return;
            }

            if (m_SourcePrefabs.TryGetValue(enemy, out EnemyBrain prefab) && prefab != null)
            {
                enemy.transform.SetParent(container, false);
                enemy.gameObject.SetActive(false);
                GetPool(prefab).Push(enemy);
            }
            m_SourcePrefabs.Remove(enemy);

            if (logEvents) Debug.Log($"[EnemySpawner] -{enemy.name} (con {m_Active.Count})");
        }
    }
}
