using System;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk
{
    /// <summary>
    /// Không gian buồng lái pháo hạm BÊN TRONG (D1onk Mothership Sub-Arena):
    /// - Tọa độ độc lập trên cùng scene (mặc định 200, 100).
    /// - Giam giữ Tank (Solo: 1 Tank; Co-op: 2 Tank gần nhất).
    /// - Trái Tim Boss ở trung tâm buồng lái: 10% Boss Max HP, khiên Bất Hoại.
    /// - 4 cột năng lượng ở 4 góc: Trắng, Đỏ, Vàng, Xanh (500 HP/cột).
    /// - Sinh ngẫu nhiên 1 quái vật thường mỗi 5s.
    /// - Solo: Phá 3/4 cột -> Xóa khiên Bất Hoại của Trái Tim.
    /// - Co-op: Phá cột kích hoạt thử thách bên ngoài; hoàn thành 3 thử thách -> Xóa khiên Bất Hoại.
    /// - Tiêu diệt Trái Tim -> Hoàn thành Bùng Nổ Năng Lượng (Thắng).
    /// 
    /// RÀNG BUỘC COLLIDER:
    /// Tuyệt đối không gắn ColliderOwnerRef trỏ về Boss Brain trên bất kỳ đối tượng buồng lái nào!
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkMothershipSubArena : MonoBehaviour
    {
        [Header("Arena Bounds & Configuration")]
        [Tooltip("Tọa độ trung tâm buồng lái lõi pháo hạm.")]
        [SerializeField] private Vector2 arenaCenter = new Vector2(200f, 100f);

        [Tooltip("Bán kính sàn đấu buồng lái (m).")]
        [SerializeField, Min(5f)] private float arenaRadius = 14f;

        [Tooltip("Độ đệm an toàn ranh giới buồng lái khi cản Tank (m).")]
        [SerializeField, Min(0f)] private float boundaryMargin = 0.5f;

        [Header("Energy Pillars Configuration")]
        [Tooltip("Khoảng cách ngang (X) đặt các cột năng lượng từ tâm buồng lái (m).")]
        [SerializeField, Min(1f)] private float pillarOffsetDistance = 7f;

        [Tooltip("Tỷ lệ khoảng cách dọc (Y) của cột so với khoảng cách ngang (mặc định 0.5 = pillarOffsetDistance * 0.5).")]
        [SerializeField] private float pillarVerticalOffsetRatio = 0.5f;

        [Tooltip("Lượng máu tối đa của mỗi Cột năng lượng khi khởi tạo.")]
        [SerializeField, Min(1f)] private float pillarHealth = 500f;

        [Header("Boss Heart Configuration")]
        [Tooltip("Tỷ lệ máu của Trái Tim Boss so với máu tối đa của Boss gốc (0.1 = 10%).")]
        [SerializeField, Range(0.01f, 1f)] private float heartHealthRatio = 0.10f;

        [Header("Monster Spawn Settings")]
        [Tooltip("Tần suất xuất hiện quái vật thường ngẫu nhiên bên trong buồng lái (giây). Mặc định 5s.")]
        [SerializeField, Min(0.5f)] private float normalEnemySpawnInterval = 5.0f;

        [Tooltip("Tỷ lệ bán kính buồng lái dùng để tạo ngẫu nhiên vị trí quái xuất hiện (0.6 = 60% arenaRadius).")]
        [SerializeField, Range(0.1f, 1f)] private float monsterSpawnRadiusFactor = 0.6f;

        [Header("Tank Trapping Spacing")]
        [Tooltip("Khoảng cách sắp xếp vị trí các Tank khi bị kéo vào buồng lái (X: độ lệch sang 2 bên, Y: khoảng cách giữa các hàng).")]
        [SerializeField] private Vector2 tankTrappedSpacing = new Vector2(2.5f, 2.0f);

        [Tooltip("Độ lệch Y ban đầu tính từ tâm buồng lái khi đặt Tank bị giam.")]
        [SerializeField] private float tankTrappedBaseYOffset = -3.5f;

        [Header("Prefabs")]
        [Tooltip("Prefab Trái Tim Boss (D1onkHeart).")]
        [SerializeField] private GameObject heartPrefab;

        [Tooltip("Prefab Cột Trắng (D1onkEnergyPillar).")]
        [SerializeField] private GameObject whitePillarPrefab;

        [Tooltip("Prefab Cột Đỏ (D1onkEnergyPillar).")]
        [SerializeField] private GameObject redPillarPrefab;

        [Tooltip("Prefab Cột Năng Lượng dự phòng.")]
        [SerializeField] private GameObject pillarPrefab;

        [Tooltip("Danh sách Prefab quái thường để triệu hồi ngẫu nhiên.")]
        [SerializeField] private GameObject[] normalEnemyPrefabs;

        private D1onkBrain m_Boss;
        private D1onkCoopChallenges m_CoopChallenges;
        private bool m_IsCoop;

        private readonly List<TankBrain> m_TrappedTanks = new List<TankBrain>();
        private readonly Dictionary<TankBrain, Vector2> m_SavedTankPositions = new Dictionary<TankBrain, Vector2>();

        private D1onkHeart m_Heart;
        private readonly List<D1onkEnergyPillar> m_Pillars = new List<D1onkEnergyPillar>();
        private readonly HashSet<D1onkPillarColor> m_DestroyedPillars = new HashSet<D1onkPillarColor>();
        private readonly List<EnemyBrain> m_SpawnedMinionsInside = new List<EnemyBrain>();

        private float m_MonsterSpawnTimer;
        private bool m_IsArenaActive;

        public GameObject HeartPrefab { get => heartPrefab; set => heartPrefab = value; }
        public GameObject WhitePillarPrefab { get => whitePillarPrefab; set => whitePillarPrefab = value; }
        public GameObject RedPillarPrefab { get => redPillarPrefab; set => redPillarPrefab = value; }
        public GameObject PillarPrefab { get => pillarPrefab; set => pillarPrefab = value; }

        public Vector2 ArenaCenter => arenaCenter;
        public float ArenaRadius => arenaRadius;
        public float BoundaryMargin { get => boundaryMargin; set => boundaryMargin = value; }
        public float PillarOffsetDistance { get => pillarOffsetDistance; set => pillarOffsetDistance = value; }
        public float PillarVerticalOffsetRatio { get => pillarVerticalOffsetRatio; set => pillarVerticalOffsetRatio = value; }
        public float PillarHealth { get => pillarHealth; set => pillarHealth = value; }
        public float HeartHealthRatio { get => heartHealthRatio; set => heartHealthRatio = value; }
        public float NormalEnemySpawnInterval { get => normalEnemySpawnInterval; set => normalEnemySpawnInterval = value; }
        public float MonsterSpawnRadiusFactor { get => monsterSpawnRadiusFactor; set => monsterSpawnRadiusFactor = value; }
        public Vector2 TankTrappedSpacing { get => tankTrappedSpacing; set => tankTrappedSpacing = value; }
        public float TankTrappedBaseYOffset { get => tankTrappedBaseYOffset; set => tankTrappedBaseYOffset = value; }
        public IReadOnlyList<TankBrain> TrappedTanks => m_TrappedTanks;
        public bool IsTankTrapped(TankBrain tank) => tank != null && m_TrappedTanks.Contains(tank);
        public D1onkHeart Heart => m_Heart;
        public IReadOnlyList<D1onkEnergyPillar> Pillars => m_Pillars;
        public int DestroyedPillarsCount => m_DestroyedPillars.Count;
        public bool IsArenaActive => m_IsArenaActive;
        public bool IsCoop => m_IsCoop;

        public void SetNormalEnemyPrefabs(GameObject[] prefabs)
        {
            if (prefabs != null && prefabs.Length > 0)
            {
                normalEnemyPrefabs = prefabs;
            }
        }

        public void InitializeArena(D1onkBrain boss, D1onkCoopChallenges challenges, bool isCoop, Vector2? customCenter = null)
        {
            m_Boss = boss;
            m_CoopChallenges = challenges;
            m_IsCoop = isCoop;

            if (customCenter.HasValue)
            {
                arenaCenter = customCenter.Value;
            }
            transform.position = arenaCenter;

            m_TrappedTanks.Clear();
            m_SavedTankPositions.Clear();
            m_DestroyedPillars.Clear();
            m_SpawnedMinionsInside.Clear();
            m_MonsterSpawnTimer = 0f;
            m_IsArenaActive = true;

            if (m_CoopChallenges != null)
            {
                m_CoopChallenges.OnTwoChallengesCompleted += HandleTwoChallengesCompleted;
                m_CoopChallenges.OnThreeChallengesCompleted += HandleTwoChallengesCompleted;
            }

            BuildVisualFloor();
            SpawnHeart();
            SpawnPillars();
        }

        private void BuildVisualFloor()
        {
            // Floor đã được cấu hình trên Prefab con hoặc sàn arena
            Transform existingFloor = transform.Find("D1onkMothership_Floor");
            if (existingFloor != null) return;
        }

        private void SpawnHeart()
        {
            float bossHp = m_Boss != null ? m_Boss.MaxHealth : (m_IsCoop ? 54000f : 27000f);
            float heartHp = bossHp * heartHealthRatio; // Mặc định 10% Max HP của Boss

#if UNITY_EDITOR
            if (heartPrefab == null)
            {
                heartPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/D1onk/D1onkHeart.prefab");
            }
#endif

            if (heartPrefab == null)
            {
                Debug.LogError("[D1onkMothershipSubArena] heartPrefab chưa được gán trong Inspector! Vui lòng gán Prefab D1onkHeart.", this);
                return;
            }

            bool isNetServer = IronWasteland.Tanks.TankBrain.NetworkSessionActive &&
                Unity.Netcode.NetworkManager.Singleton != null &&
                Unity.Netcode.NetworkManager.Singleton.IsServer;

            if (isNetServer && m_Boss != null)
            {
                m_Boss.GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                    "MothershipFloor", arenaCenter, 0f, default, arenaRadius, 0f);
            }

            GameObject heartObj = Instantiate(heartPrefab, arenaCenter, Quaternion.identity, isNetServer ? null : transform);
            if (isNetServer && heartObj.TryGetComponent(out Unity.Netcode.NetworkObject heartNetObj) && !heartNetObj.IsSpawned)
            {
                heartNetObj.Spawn(true);
            }
            m_Heart = heartObj.GetComponent<D1onkHeart>();
            if (m_Heart != null)
            {
                m_Heart.InitializeHeart(m_Boss, this, heartHp);
            }
            else
            {
                Debug.LogError("[D1onkMothershipSubArena] heartPrefab không chứa component D1onkHeart!", this);
            }
        }

        private void SpawnPillars()
        {
            m_Pillars.Clear();

#if UNITY_EDITOR
            if (whitePillarPrefab == null)
            {
                whitePillarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/D1onk/D1onkEnergyPillar_White.prefab");
            }
            if (redPillarPrefab == null)
            {
                redPillarPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/D1onk/D1onkEnergyPillar_Red.prefab");
            }
#endif

            // Chỉ tạo 2 Cột: Trắng (White) và Đỏ (Red)
            float verticalOffset = pillarOffsetDistance * pillarVerticalOffsetRatio;
            var configs = new[]
            {
                new { color = D1onkPillarColor.White, offset = new Vector2(-pillarOffsetDistance, verticalOffset), prefab = whitePillarPrefab != null ? whitePillarPrefab : pillarPrefab },
                new { color = D1onkPillarColor.Red, offset = new Vector2(pillarOffsetDistance, verticalOffset), prefab = redPillarPrefab != null ? redPillarPrefab : pillarPrefab }
            };

            for (int i = 0; i < configs.Length; i++)
            {
                var cfg = configs[i];
                Vector2 spawnPos = arenaCenter + cfg.offset;

                if (cfg.prefab == null)
                {
                    Debug.LogError($"[D1onkMothershipSubArena] Prefab cho cột {cfg.color} chưa được gán trong Inspector!", this);
                    continue;
                }

                bool isNetServer = IronWasteland.Tanks.TankBrain.NetworkSessionActive &&
                    Unity.Netcode.NetworkManager.Singleton != null &&
                    Unity.Netcode.NetworkManager.Singleton.IsServer;

                GameObject pillarObj = Instantiate(cfg.prefab, spawnPos, Quaternion.identity, isNetServer ? null : transform);
                if (isNetServer && pillarObj.TryGetComponent(out Unity.Netcode.NetworkObject pillarNetObj) && !pillarNetObj.IsSpawned)
                {
                    pillarNetObj.Spawn(true);
                }
                D1onkEnergyPillar pillar = pillarObj.GetComponent<D1onkEnergyPillar>();
                if (pillar != null)
                {
                    pillar.InitializePillar(cfg.color, this, pillarHealth);
                    m_Pillars.Add(pillar);
                }
                else
                {
                    Debug.LogError($"[D1onkMothershipSubArena] Prefab cho cột {cfg.color} không chứa component D1onkEnergyPillar!", this);
                }
            }
        }

        public void TrapTank(TankBrain tank)
        {
            if (tank == null || !tank.IsAlive) return;

            if (!m_SavedTankPositions.ContainsKey(tank))
            {
                m_SavedTankPositions[tank] = tank.transform.position;
            }

            if (!m_TrappedTanks.Contains(tank))
            {
                m_TrappedTanks.Add(tank);
            }

            // Đặt vị trí bên trong buồng lái theo cấu hình spacing
            int idx = m_TrappedTanks.IndexOf(tank);
            float xOffset = (idx % 2 == 0) ? -tankTrappedSpacing.x : tankTrappedSpacing.x;
            float yOffset = tankTrappedBaseYOffset - (idx / 2) * tankTrappedSpacing.y;
            IronWasteland.Net.NetTeleport.SetTankAreaBounds(
                tank, true, IronWasteland.Net.NetTeleport.CreateCircleAreaRect(arenaCenter, arenaRadius));
            IronWasteland.Net.NetTeleport.Move(tank, arenaCenter + new Vector2(xOffset, yOffset));
        }

        public void OnPillarDestroyed(D1onkEnergyPillar pillar)
        {
            if (pillar == null || m_DestroyedPillars.Contains(pillar.PillarColor)) return;

            m_DestroyedPillars.Add(pillar.PillarColor);
            Debug.Log($"[D1onkMothershipSubArena] Cột năng lượng {pillar.PillarColor} bị phá hủy! (Tổng: {m_DestroyedPillars.Count}/2)");

            string colorName = pillar.PillarColor == D1onkPillarColor.White ? "Trắng" : "Đỏ";
            IronWasteland.UI.BossActionAnnouncer.Announce(
                $"CỘT {colorName.ToUpperInvariant()} BỊ PHÁ",
                $"Kích hoạt thử thách Cột {colorName} ({m_DestroyedPillars.Count}/2)",
                IronWasteland.UI.BossAnnouncementType.PhaseEvent,
                2.2f);

            // Ở cả 2 chế độ (Solo và Co-op), phá cột kích hoạt thử thách tương ứng (Trắng hoặc Đỏ)
            if (m_CoopChallenges != null)
            {
                m_CoopChallenges.ActivateChallenge(pillar.PillarColor);
            }
        }

        private void HandleTwoChallengesCompleted()
        {
            if (m_Heart != null && m_Heart.InvincibleShieldActive)
            {
                Debug.Log("[D1onkMothershipSubArena] Hoàn thành 2 thử thách cột Trắng và Đỏ! Gỡ bỏ Khiên Bất Hoại của Trái Tim!");
                m_Heart.RemoveInvincibleShield();
                IronWasteland.UI.BossActionAnnouncer.Announce(
                    "KHIÊN TRÁI TIM ĐÃ GỠ",
                    "Có thể tấn công trực tiếp Trái Tim Pháo Hạm!",
                    IronWasteland.UI.BossAnnouncementType.PhaseEvent,
                    2.4f);
            }
        }

        private void HandleThreeCoopChallengesCompleted()
        {
            HandleTwoChallengesCompleted();
        }

        public void OnHeartKilled()
        {
            if (!m_IsArenaActive) return;

            Debug.Log("[D1onkMothershipSubArena] Trái Tim Boss ĐÃ BỊ TIÊU DIỆT! Kích hoạt chiến thắng Bùng Nổ Năng Lượng!");
            m_IsArenaActive = false;

            if (m_Boss != null)
            {
                m_Boss.CompleteBurstSuccess();
            }
        }

        public void ManualTick(float deltaTime)
        {
            if (!m_IsArenaActive) return;

            // Ràng buộc ranh giới buồng lái: giữ các Tank bị giam không thể dash/teleport ra ngoài bán kính arenaRadius
            for (int i = 0; i < m_TrappedTanks.Count; i++)
            {
                TankBrain t = m_TrappedTanks[i];
                if (t != null && t.IsAlive)
                {
                    Vector2 offset = (Vector2)t.transform.position - arenaCenter;
                    float maxR = Mathf.Max(1f, arenaRadius - boundaryMargin);
                    if (offset.sqrMagnitude > maxR * maxR)
                    {
                        IronWasteland.Net.NetTeleport.Move(t, arenaCenter + offset.normalized * maxR);
                    }
                }
            }

            // Xuất hiện thêm 1 quái vật thường ngẫu nhiên ở Vùng Đất Nguyên Tố bên trong buồng lái theo tần suất cấu hình
            m_MonsterSpawnTimer += deltaTime;
            if (m_MonsterSpawnTimer >= normalEnemySpawnInterval)
            {
                m_MonsterSpawnTimer = 0f;
                SpawnInsideMonster();
            }

            if (m_Heart != null && m_Heart.IsAlive)
            {
                m_Heart.ManualTick(deltaTime);
            }
        }

        public void SpawnInsideMonster()
        {
            if (normalEnemyPrefabs == null || normalEnemyPrefabs.Length == 0)
            {
                return;
            }

            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * (arenaRadius * monsterSpawnRadiusFactor);
            Vector2 spawnPos = arenaCenter + randomOffset;

            int rndIdx = UnityEngine.Random.Range(0, normalEnemyPrefabs.Length);
            GameObject prefab = normalEnemyPrefabs[rndIdx];
            if (prefab != null)
            {
                EnemyBrain monster = null;
                if (IronWasteland.Tanks.TankBrain.NetworkSessionActive)
                {
                    var manager = Unity.Netcode.NetworkManager.Singleton;
                    if (manager != null && manager.IsServer &&
                        prefab.TryGetComponent(out EnemyBrain enemyPrefab) &&
                        IronWasteland.Net.NetSpawnService.Instance != null)
                    {
                        monster = IronWasteland.Net.NetSpawnService.Instance.SpawnEnemy(
                            enemyPrefab, spawnPos, enemyPrefab.Stats);
                    }
                }
                else
                {
                    GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity, transform);
                    monster = go.GetComponent<EnemyBrain>();
                }

                if (monster != null)
                {
                    m_SpawnedMinionsInside.Add(monster);
                }
            }
        }

        /// <summary>
        /// Đưa toàn bộ Tank ở BÊN TRONG trở ra ngoài sân chính theo vị trí đã lưu.
        /// </summary>
        public void ReleaseTanks()
        {
            for (int i = 0; i < m_TrappedTanks.Count; i++)
            {
                TankBrain tank = m_TrappedTanks[i];
                if (tank != null && m_SavedTankPositions.TryGetValue(tank, out var pos))
                {
                    IronWasteland.Net.NetTeleport.SetTankAreaBounds(tank, false, default);
                    IronWasteland.Net.NetTeleport.Move(tank, pos);
                }
            }
            m_TrappedTanks.Clear();
            m_SavedTankPositions.Clear();
        }

        private static void DestroyOrDespawn(GameObject go)
        {
            if (go == null) return;
            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive &&
                go.TryGetComponent(out Unity.Netcode.NetworkObject netObj) && netObj.IsSpawned)
            {
                var manager = Unity.Netcode.NetworkManager.Singleton;
                if (manager != null && manager.IsServer) netObj.Despawn(true);
                return;
            }
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        public void CleanupArena()
        {
            m_IsArenaActive = false;

            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive && m_Boss != null)
            {
                m_Boss.GetComponent<IronWasteland.Net.EnemyNetState>()?.BroadcastVfx(
                    "MothershipCleanup", arenaCenter);
            }

            if (m_CoopChallenges != null)
            {
                m_CoopChallenges.OnThreeChallengesCompleted -= HandleThreeCoopChallengesCompleted;
            }

            ReleaseTanks();

            for (int i = 0; i < m_Pillars.Count; i++)
            {
                if (m_Pillars[i] != null) DestroyOrDespawn(m_Pillars[i].gameObject);
            }
            m_Pillars.Clear();

            for (int i = 0; i < m_SpawnedMinionsInside.Count; i++)
            {
                if (m_SpawnedMinionsInside[i] != null) DestroyOrDespawn(m_SpawnedMinionsInside[i].gameObject);
            }
            m_SpawnedMinionsInside.Clear();

            if (m_Heart != null)
            {
                DestroyOrDespawn(m_Heart.gameObject);
                m_Heart = null;
            }

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
