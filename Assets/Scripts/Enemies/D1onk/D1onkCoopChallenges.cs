using System;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies.Normal;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk
{
    /// <summary>
    /// Quản lý 4 thử thách BÊN NGOÀI pháo hạm khi chơi Co-op:
    /// Khi bên trong phá hủy 1 cột năng lượng, kích hoạt thử thách tương ứng bên ngoài:
    /// 1. Cột Trắng: Xuất hiện 10 Tinh Linh Sấm Sét. Diệt hết -> Hoàn thành.
    /// 2. Cột Đỏ: Xuất hiện cột phát & thu điện. Mỗi 0.5s truyền 1 điểm điện. Tank/vật cản chặn đường truyền.
    ///    Tank chặn nhận 20% ATK sát thương mỗi 0.5s. Trong 10s, nếu thu điện chưa tích đủ 5 điểm -> Hoàn thành.
    /// 3. Cột Vàng: Xuất hiện 2 Ký Sinh Thiên Thạch và 2 cột điện. Ký sinh nổ phá cột. Trong 10s phá 2 cột -> Hoàn thành.
    /// 4. Cột Xanh: Trong 10s tiêu diệt toàn bộ bản sao Tank bên ngoài -> Hoàn thành.
    /// 
    /// Khi hoàn thành 3 thử thách bất kỳ -> Xóa khiên Bất Hoại của Trái Tim Boss bên trong.
    /// 
    /// RÀNG BUỘC COLLIDER:
    /// Tuyệt đối không gắn ColliderOwnerRef trỏ về Boss Brain trên bất kỳ đối tượng thử thách nào!
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkCoopChallenges : MonoBehaviour
    {
        [Header("Outside Challenge Prefabs")]

        [Tooltip("Prefab Tinh Linh Sấm Sét (thử thách Trắng).")]
        [SerializeField] private GameObject tinhLinhSamSetPrefab;

        [Tooltip("Prefab Cột phát điện (thử thách Đỏ).")]
        [SerializeField] private GameObject electricTransmitterPrefab;

        [Tooltip("Prefab Cột thu điện (thử thách Đỏ).")]
        [SerializeField] private GameObject electricReceiverPrefab;

        [Tooltip("Prefab tia điện giữa cột phát và cột thu (thử thách Đỏ).")]
        [SerializeField] private GameObject electricBeamPrefab;

        [Tooltip("Danh sách pool quái thường từ VFX Controller.")]
        [SerializeField] private GameObject[] normalEnemyPrefabs;

        [Header("White Challenge Configuration")]
        [Tooltip("Số lượng Tinh Linh Sấm Sét (thử thách Trắng).")]
        [SerializeField] private int enemyCount = 10;

        [Tooltip("Bán kính vòng tròn triệu hồi Tinh Linh Sấm Sét ở chế độ Co-op (m). Mặc định 5.0m.")]
        [SerializeField, Min(1f)] private float whiteSpawnRadiusCoop = 5.0f;

        [Tooltip("Bán kính vòng tròn triệu hồi Tinh Linh Sấm Sét ở chế độ Solo (m). Mặc định 3.5m.")]
        [SerializeField, Min(1f)] private float whiteSpawnRadiusSolo = 3.5f;

        [Header("Red Challenge Configuration (Điện Năng)")]
        [Tooltip("Khoảng cách từ tâm đến mỗi cột điện trong chế độ Co-op (Khoảng cách giữa 2 cột điện = 2 * khoảng cách này). Mặc định 6.0m (khoảng cách 12m).")]
        [SerializeField, Min(1f)] private float redPillarDistanceCoop = 6.0f;

        [Tooltip("Khoảng cách từ tâm đến mỗi cột điện trong chế độ Solo (Khoảng cách giữa 2 cột điện = 2 * khoảng cách này). Mặc định 4.5m (khoảng cách 9m).")]
        [SerializeField, Min(1f)] private float redPillarDistanceSolo = 4.5f;

        [Tooltip("Thời gian duy trì thử thách truyền điện (giây). Mặc định 10s.")]
        [SerializeField, Min(1f)] private float redChallengeDuration = 10.0f;

        [Tooltip("Tần suất truyền điện giữa cột phát và thu (giây). Mặc định 0.5s.")]
        [SerializeField, Min(0.05f)] private float redTransmissionInterval = 0.5f;

        [Tooltip("Ngưỡng điểm điện năng tối đa của cột thu để hoàn thành thử thách (Điểm < ngưỡng này sau khi hết giờ -> Thành công). Mặc định 5 điểm.")]
        [SerializeField, Min(1)] private int redMaxAllowedPoints = 5;

        [Tooltip("Tỷ lệ sát thương theo ATK Boss gây lên Tank đứng chặn tia điện mỗi nhịp truyền (0.2 = 20%).")]
        [SerializeField, Range(0.01f, 1f)] private float redBlockDamageRatio = 0.20f;

        [Tooltip("Bán kính quét / phát hiện Tank đứng chặn đường điện (m). Mặc định 1.2m.")]
        [SerializeField, Min(0.1f)] private float redBlockDetectionRadius = 1.2f;

        [Tooltip("Độ dày của tia điện khi truyền bình thường.")]
        [SerializeField, Min(0.01f)] private float beamNormalThickness = 0.35f;

        [Tooltip("Độ dày của tia điện khi bị Tank chặn ngắt.")]
        [SerializeField, Min(0.01f)] private float beamBlockedThickness = 0.20f;

        [Tooltip("Màu tia điện khi truyền bình thường.")]
        [SerializeField] private Color beamNormalColor = new Color(0.2f, 0.85f, 1f, 0.95f);

        [Tooltip("Màu tia điện khi bị chặn.")]
        [SerializeField] private Color beamBlockedColor = new Color(1f, 0.3f, 0.2f, 0.85f);

        private D1onkBrain m_Boss;

        private int m_CompletedChallengesCount = 0;
        private readonly HashSet<D1onkPillarColor> m_CompletedColors = new HashSet<D1onkPillarColor>();

        private bool m_IsSolo;
        private Vector2 m_ChallengeCenter;

        // White Challenge Runtime
        private bool m_WhiteActive;
        private readonly List<EnemyBrain> m_WhiteMinions = new List<EnemyBrain>();

        // Red Challenge Runtime
        private bool m_RedActive;
        private float m_RedTimer;
        private float m_RedTickTimer;
        private int m_RedEnergyPoints;
        private Vector2 m_RedTransmitterPos;
        private Vector2 m_RedReceiverPos;
        private bool m_RedResolved;
        private readonly List<GameObject> m_RedObjects = new List<GameObject>();
        private GameObject m_RedBeamObject;
        private SpriteRenderer m_RedBeamRenderer;

        // Yellow Challenge Runtime
        private bool m_YellowActive;
        private float m_YellowTimer;
        private int m_YellowPillarsRemaining;
        private readonly List<Vector2> m_YellowPillarPositions = new List<Vector2>();
        private readonly List<NormalEnemy_KySinhThienThach> m_YellowParasites = new List<NormalEnemy_KySinhThienThach>();
        private readonly List<GameObject> m_YellowPillarObjects = new List<GameObject>();
        private bool m_YellowResolved;

        // Blue Challenge Runtime
        private bool m_BlueActive;
        private float m_BlueTimer;
        private bool m_BlueResolved;

        public GameObject TinhLinhSamSetPrefab { get => tinhLinhSamSetPrefab; set => tinhLinhSamSetPrefab = value; }
        public GameObject ElectricTransmitterPrefab { get => electricTransmitterPrefab; set => electricTransmitterPrefab = value; }
        public GameObject ElectricReceiverPrefab { get => electricReceiverPrefab; set => electricReceiverPrefab = value; }
        public GameObject ElectricBeamPrefab { get => electricBeamPrefab; set => electricBeamPrefab = value; }

        public int EnemyCount { get => enemyCount; set => enemyCount = value; }
        public float WhiteSpawnRadiusCoop { get => whiteSpawnRadiusCoop; set => whiteSpawnRadiusCoop = value; }
        public float WhiteSpawnRadiusSolo { get => whiteSpawnRadiusSolo; set => whiteSpawnRadiusSolo = value; }
        public float RedPillarDistanceCoop { get => redPillarDistanceCoop; set => redPillarDistanceCoop = value; }
        public float RedPillarDistanceSolo { get => redPillarDistanceSolo; set => redPillarDistanceSolo = value; }
        public float RedChallengeDuration { get => redChallengeDuration; set => redChallengeDuration = value; }
        public float RedTransmissionInterval { get => redTransmissionInterval; set => redTransmissionInterval = value; }
        public int RedMaxAllowedPoints { get => redMaxAllowedPoints; set => redMaxAllowedPoints = value; }
        public float RedBlockDamageRatio { get => redBlockDamageRatio; set => redBlockDamageRatio = value; }
        public float RedBlockDetectionRadius { get => redBlockDetectionRadius; set => redBlockDetectionRadius = value; }
        public float BeamNormalThickness { get => beamNormalThickness; set => beamNormalThickness = value; }
        public float BeamBlockedThickness { get => beamBlockedThickness; set => beamBlockedThickness = value; }
        public Color BeamNormalColor { get => beamNormalColor; set => beamNormalColor = value; }
        public Color BeamBlockedColor { get => beamBlockedColor; set => beamBlockedColor = value; }

        public bool IsSolo => m_IsSolo;
        public Vector2 ChallengeCenter => m_ChallengeCenter;
        public int CompletedChallengesCount => m_CompletedChallengesCount;
        public bool IsWhiteCompleted => m_CompletedColors.Contains(D1onkPillarColor.White);
        public bool IsRedCompleted => m_CompletedColors.Contains(D1onkPillarColor.Red);
        public bool IsYellowCompleted => m_CompletedColors.Contains(D1onkPillarColor.Yellow);
        public bool IsBlueCompleted => m_CompletedColors.Contains(D1onkPillarColor.Blue);

        public int RedEnergyPoints => m_RedEnergyPoints;
        public float RedTimer => m_RedTimer;
        public float YellowTimer => m_YellowTimer;
        public float BlueTimer => m_BlueTimer;
        public int YellowPillarsRemaining => m_YellowPillarsRemaining;
        public IReadOnlyList<EnemyBrain> WhiteMinions => m_WhiteMinions;

        public event Action<D1onkPillarColor> OnChallengeCompleted;
        public event Action OnTwoChallengesCompleted;
        public event Action OnThreeChallengesCompleted;

        public void SetChallengeCenter(Vector2 center)
        {
            m_ChallengeCenter = center;
        }

        public void Initialize(D1onkBrain boss, Vector2 defaultCenter, bool isSolo = false)
        {
            m_Boss = boss;
            m_IsSolo = isSolo;
            m_ChallengeCenter = defaultCenter;

            m_CompletedChallengesCount = 0;
            m_CompletedColors.Clear();

            m_WhiteActive = false;
            m_RedActive = false;
            m_YellowActive = false;
            m_BlueActive = false;

            m_RedResolved = false;
            m_YellowResolved = false;
            m_BlueResolved = false;
        }

        public void Initialize(D1onkBrain boss)
        {
            Vector2 defaultCenter = boss != null ? (Vector2)boss.transform.position : Vector2.zero;
            Initialize(boss, defaultCenter, isSolo: false);
        }

        public void SetNormalEnemyPrefabs(GameObject[] prefabs)
        {
            if (prefabs != null && prefabs.Length > 0)
            {
                normalEnemyPrefabs = prefabs;
            }
        }

        private GameObject FindNormalPrefab(string partialName)
        {
            if (normalEnemyPrefabs == null) return null;
            for (int i = 0; i < normalEnemyPrefabs.Length; i++)
            {
                if (normalEnemyPrefabs[i] != null && normalEnemyPrefabs[i].name.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return normalEnemyPrefabs[i];
                }
            }
            return null;
        }

        public void ActivateChallenge(D1onkPillarColor color)
        {
            switch (color)
            {
                case D1onkPillarColor.White:
                    StartWhiteChallenge();
                    break;
                case D1onkPillarColor.Red:
                    StartRedChallenge();
                    break;
            }
        }

        #region 1. White Challenge (10 Tinh Linh Sấm Sét)

        private void StartWhiteChallenge()
        {
            if (m_WhiteActive || IsWhiteCompleted) return;
            m_WhiteActive = true;
            m_WhiteMinions.Clear();

#if UNITY_EDITOR
            if (tinhLinhSamSetPrefab == null)
            {
                tinhLinhSamSetPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Normal/TinhLinhSamSetNho.prefab");
            }
#endif
            if (tinhLinhSamSetPrefab == null)
            {
                tinhLinhSamSetPrefab = FindNormalPrefab("TinhLinhSamSet");
            }

            if (tinhLinhSamSetPrefab == null)
            {
                Debug.LogError("[D1onkCoopChallenges] Không tìm thấy Prefab TinhLinhSamSetNho cho thử thách Trắng!", this);
                return;
            }

            Vector2 center = m_ChallengeCenter != Vector2.zero ? m_ChallengeCenter : (m_Boss != null ? (Vector2)m_Boss.transform.position : Vector2.zero);

            for (int i = 0; i < enemyCount; i++)
            {
                float angle = (i * (360f / Mathf.Max(1, enemyCount))) * Mathf.Deg2Rad;
                float radius = m_IsSolo ? whiteSpawnRadiusSolo : whiteSpawnRadiusCoop;
                Vector2 spawnPos = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);

                EnemyBrain minion = null;
                if (IronWasteland.Tanks.TankBrain.NetworkSessionActive)
                {
                    var manager = Unity.Netcode.NetworkManager.Singleton;
                    if (manager == null || !manager.IsServer) continue;

                    if (tinhLinhSamSetPrefab.TryGetComponent(out EnemyBrain enemyPrefab) &&
                        IronWasteland.Net.NetSpawnService.Instance != null)
                    {
                        minion = IronWasteland.Net.NetSpawnService.Instance.SpawnEnemy(
                            enemyPrefab, spawnPos, enemyPrefab.Stats);
                    }
                }
                else
                {
                    GameObject go = Instantiate(tinhLinhSamSetPrefab, spawnPos, Quaternion.identity);
                    minion = go.GetComponent<EnemyBrain>();
                }

                if (minion != null)
                {
                    m_WhiteMinions.Add(minion);
                    minion.OnDeathEvent += () => CheckWhiteChallengeProgress();
                }
            }
        }

        private void CheckWhiteChallengeProgress()
        {
            if (!m_WhiteActive || IsWhiteCompleted) return;

            int aliveCount = 0;
            for (int i = 0; i < m_WhiteMinions.Count; i++)
            {
                if (m_WhiteMinions[i] != null && m_WhiteMinions[i].IsAlive)
                {
                    aliveCount++;
                }
            }

            if (aliveCount == 0)
            {
                m_WhiteActive = false;
                MarkChallengeComplete(D1onkPillarColor.White);
            }
        }

        #endregion

        #region 2. Red Challenge (Truyền Điện 10s, < 5 điểm)

        private void StartRedChallenge()
        {
            if (m_RedActive || IsRedCompleted) return;
            m_RedActive = true;
            m_RedTimer = 0f;
            m_RedTickTimer = 0f;
            m_RedEnergyPoints = 0;
            m_RedResolved = false;

#if UNITY_EDITOR
            if (electricTransmitterPrefab == null)
            {
                electricTransmitterPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/D1onk/D1onkElectricPillar_Transmitter.prefab");
            }
            if (electricReceiverPrefab == null)
            {
                electricReceiverPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/D1onk/D1onkElectricPillar_Receiver.prefab");
            }
            if (electricBeamPrefab == null)
            {
                electricBeamPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/D1onk/D1onkElectricPillar_Beam.prefab");
            }
#endif
            if (electricTransmitterPrefab == null || electricReceiverPrefab == null)
            {
                Debug.LogError("[D1onkCoopChallenges] electricTransmitterPrefab hoặc electricReceiverPrefab chưa được cấu hình!", this);
                return;
            }

            Vector2 center = m_ChallengeCenter != Vector2.zero ? m_ChallengeCenter : (m_Boss != null ? (Vector2)m_Boss.transform.position : Vector2.zero);
            float spread = m_IsSolo ? redPillarDistanceSolo : redPillarDistanceCoop;
            m_RedTransmitterPos = center + new Vector2(-spread, 0f);
            m_RedReceiverPos = center + new Vector2(spread, 0f);

            CleanupRedVisuals();
            bool isNetServer = IronWasteland.Tanks.TankBrain.NetworkSessionActive &&
                Unity.Netcode.NetworkManager.Singleton != null &&
                Unity.Netcode.NetworkManager.Singleton.IsServer;

            GameObject tx = Instantiate(electricTransmitterPrefab, m_RedTransmitterPos, Quaternion.identity, isNetServer ? null : transform);
            if (isNetServer && tx.TryGetComponent(out Unity.Netcode.NetworkObject txNet) && !txNet.IsSpawned) txNet.Spawn(true);
            m_RedObjects.Add(tx);

            GameObject rx = Instantiate(electricReceiverPrefab, m_RedReceiverPos, Quaternion.identity, isNetServer ? null : transform);
            if (isNetServer && rx.TryGetComponent(out Unity.Netcode.NetworkObject rxNet) && !rxNet.IsSpawned) rxNet.Spawn(true);
            m_RedObjects.Add(rx);

            // Bổ sung tia điện kết nối giữa cột phát và cột thu
            if (electricBeamPrefab != null)
            {
                m_RedBeamObject = Instantiate(electricBeamPrefab, transform);
            }
            else
            {
                m_RedBeamObject = new GameObject("RedElectric_Beam");
                m_RedBeamObject.transform.SetParent(transform);
                var sr = m_RedBeamObject.AddComponent<SpriteRenderer>();
                sr.color = beamNormalColor;
                sr.sortingOrder = 7;
            }

            if (m_RedBeamObject != null)
            {
                m_RedBeamRenderer = m_RedBeamObject.GetComponent<SpriteRenderer>();
                m_RedObjects.Add(m_RedBeamObject);
                UpdateRedBeam(false, m_RedReceiverPos);
            }
        }

        private void CleanupRedVisuals()
        {
            for (int i = 0; i < m_RedObjects.Count; i++)
            {
                if (m_RedObjects[i] != null)
                {
                    if (Application.isPlaying) Destroy(m_RedObjects[i]);
                    else DestroyImmediate(m_RedObjects[i]);
                }
            }
            m_RedObjects.Clear();
            m_RedBeamObject = null;
            m_RedBeamRenderer = null;
        }

        private void UpdateRedBeam(bool isBlocked, Vector2 endPos)
        {
            if (m_RedBeamObject == null) return;

            Vector2 startPos = m_RedTransmitterPos;
            Vector2 dir = endPos - startPos;
            float dist = dir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            m_RedBeamObject.transform.position = (startPos + endPos) * 0.5f;
            m_RedBeamObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            m_RedBeamObject.transform.localScale = new Vector3(dist, isBlocked ? beamBlockedThickness : beamNormalThickness, 1f);

            if (m_RedBeamRenderer != null)
            {
                m_RedBeamRenderer.color = isBlocked ? beamBlockedColor : beamNormalColor;
            }
        }

        private TankBrain DetectBlockingTank(out Vector2 blockPoint)
        {
            blockPoint = m_RedReceiverPos;

            // 1. Dùng Physics2D LinecastAll phát hiện TankBrain hoặc Tường/Địa hình chặn tia điện
            RaycastHit2D[] hits = Physics2D.LinecastAll(m_RedTransmitterPos, m_RedReceiverPos);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null) continue;

                var ownerRef = hit.collider.GetComponent<ColliderOwnerRef>() ?? hit.collider.GetComponentInParent<ColliderOwnerRef>();
                if (ownerRef != null && ownerRef.Owner is TankBrain tank && tank.IsAlive)
                {
                    bool isEligible = m_IsSolo || tank.transform.position.x < 150f;
                    if (isEligible)
                    {
                        blockPoint = hit.point;
                        return tank;
                    }
                }
                else if (!hit.collider.isTrigger && (ownerRef == null || ownerRef.Owner == null))
                {
                    // Địa hình hoặc Tường vật lý (non-trigger, không thuộc Unit nào)
                    blockPoint = hit.point;
                    return null;
                }
            }

            // 2. Fallback cho môi trường test khi không chạy physics loop liên tục
            var allTanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain tank = allTanks[i];
                if (tank != null && tank.IsAlive)
                {
                    bool isEligible = m_IsSolo || tank.transform.position.x < 150f;
                    if (isEligible)
                    {
                        var ownerRef = tank.GetComponentInChildren<ColliderOwnerRef>();
                        if (ownerRef != null && ownerRef.Owner is TankBrain)
                        {
                            if (IsPointNearSegment(tank.transform.position, m_RedTransmitterPos, m_RedReceiverPos, redBlockDetectionRadius))
                            {
                                blockPoint = tank.transform.position;
                                return tank;
                            }
                        }
                    }
                }
            }

            return null;
        }

        public void ManualTickRed(float dt)
        {
            if (!m_RedActive || m_RedResolved) return;

            m_RedTimer += dt;
            m_RedTickTimer += dt;

            // Cập nhật hiển thị tia điện liên tục theo vị trí Tank chặn
            TankBrain currentBlocker = DetectBlockingTank(out Vector2 currentBlockPoint);
            bool isBlockedNow = (currentBlocker != null);
            UpdateRedBeam(isBlockedNow, isBlockedNow ? currentBlockPoint : m_RedReceiverPos);

            while (m_RedTickTimer >= redTransmissionInterval)
            {
                m_RedTickTimer -= redTransmissionInterval;
                TickRedTransmission(currentBlocker);
            }

            if (m_RedTimer >= redChallengeDuration)
            {
                m_RedResolved = true;
                m_RedActive = false;
                CleanupRedVisuals();

                // Nếu trong 10s thu điện chưa tích được 5 điểm -> Hoàn thành
                if (m_RedEnergyPoints < redMaxAllowedPoints)
                {
                    MarkChallengeComplete(D1onkPillarColor.Red);
                }
                else
                {
                    Debug.Log($"[D1onkCoopChallenges] Thử thách Cột Đỏ thất bại! Thu điện đã tích {m_RedEnergyPoints} điểm (ngưỡng < {redMaxAllowedPoints}).");
                }
            }
        }

        private void TickRedTransmission(TankBrain blockingTank)
        {
            if (blockingTank != null)
            {
                // Tank chặn đường điện -> nhận sát thương bằng 20% Tấn công mỗi 0.5s và đường điện bị ngắt
                float bossAtk = (m_Boss != null && m_Boss.Stats != null) ? m_Boss.Stats.Attack : 500f;
                float blockDamage = bossAtk * redBlockDamageRatio;
                DamageInfo blockDmg = new DamageInfo(m_Boss, blockingTank, blockDamage, 0f, blockingTank.transform.position, sourceType: DamageSourceType.EnemyAttack);
                blockingTank.TakeDamage(blockDmg);

                // Đường điện bị tạm ngắt -> không tích lũy điện
            }
            else
            {
                // Không bị chặn -> tích lũy 1 điểm điện năng
                m_RedEnergyPoints++;
            }
        }

        #endregion

        public void ManualTick(float dt)
        {
            if (m_WhiteActive) CheckWhiteChallengeProgress();
            if (m_RedActive) ManualTickRed(dt);
        }

        private void MarkChallengeComplete(D1onkPillarColor color)
        {
            if (m_CompletedColors.Contains(color)) return;

            m_CompletedColors.Add(color);
            m_CompletedChallengesCount++;

            Debug.Log($"[D1onkCoopChallenges] Thử thách {color} HOÀN THÀNH! ({m_CompletedChallengesCount}/2)");
            OnChallengeCompleted?.Invoke(color);

            if (IsWhiteCompleted && IsRedCompleted)
            {
                Debug.Log("[D1onkCoopChallenges] ĐÃ HOÀN THÀNH 2 THỬ THÁCH (Trắng & Đỏ)! Kích hoạt xóa khiên Bất Hoại của Trái Tim Boss!");
                OnTwoChallengesCompleted?.Invoke();
                OnThreeChallengesCompleted?.Invoke();
            }
        }

        private static bool IsPointNearSegment(Vector2 point, Vector2 a, Vector2 b, float threshold)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f) return Vector2.Distance(point, a) <= threshold;

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lenSq);
            Vector2 projection = a + t * ab;
            return Vector2.Distance(point, projection) <= threshold;
        }

        public void Cleanup()
        {
            CleanupRedVisuals();

            for (int i = 0; i < m_WhiteMinions.Count; i++)
            {
                if (m_WhiteMinions[i] != null)
                {
                    if (Application.isPlaying) Destroy(m_WhiteMinions[i].gameObject);
                    else DestroyImmediate(m_WhiteMinions[i].gameObject);
                }
            }
            m_WhiteMinions.Clear();

            for (int i = 0; i < m_YellowParasites.Count; i++)
            {
                if (m_YellowParasites[i] != null)
                {
                    if (Application.isPlaying) Destroy(m_YellowParasites[i].gameObject);
                    else DestroyImmediate(m_YellowParasites[i].gameObject);
                }
            }
            m_YellowParasites.Clear();
        }
    }

    /// <summary>
    /// Lightweight minion fallback phục vụ unit test khi không có prefab.
    /// </summary>
    public class LightweightTestMinion : EnemyBrain
    {
        protected override void OnBrainTick(float deltaTime) { }

        protected override void OnDeath()
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
