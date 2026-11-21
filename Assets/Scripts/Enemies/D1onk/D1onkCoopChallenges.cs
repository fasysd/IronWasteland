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
        [Tooltip("Prefab Tinh Linh Sấm Sét (NormalEnemy_TinhLinhSamSetNho).")]
        [SerializeField] private GameObject tinhLinhSamSetPrefab;

        [Tooltip("Prefab Ký Sinh Thiên Thạch (NormalEnemy_KySinhThienThach).")]
        [SerializeField] private GameObject kySinhThienThachPrefab;

        [Tooltip("Danh sách pool quái thường từ VFX Controller.")]
        [SerializeField] private GameObject[] normalEnemyPrefabs;

        private D1onkBrain m_Boss;
        private List<D1onkTankClone> m_OutsideTankClones = new List<D1onkTankClone>();

        private int m_CompletedChallengesCount = 0;
        private readonly HashSet<D1onkPillarColor> m_CompletedColors = new HashSet<D1onkPillarColor>();

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
        public event Action OnThreeChallengesCompleted;

        public void Initialize(D1onkBrain boss, List<D1onkTankClone> tankClones)
        {
            m_Boss = boss;
            m_OutsideTankClones = tankClones ?? new List<D1onkTankClone>();
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

        public void RegisterTankClones(List<D1onkTankClone> clones)
        {
            m_OutsideTankClones = clones ?? new List<D1onkTankClone>();
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
                case D1onkPillarColor.Yellow:
                    StartYellowChallenge();
                    break;
                case D1onkPillarColor.Blue:
                    StartBlueChallenge();
                    break;
            }
        }

        #region 1. White Challenge (10 Tinh Linh Sấm Sét)

        private void StartWhiteChallenge()
        {
            if (m_WhiteActive || IsWhiteCompleted) return;
            m_WhiteActive = true;
            m_WhiteMinions.Clear();

            if (tinhLinhSamSetPrefab == null)
            {
                tinhLinhSamSetPrefab = FindNormalPrefab("TinhLinhSamSet");
            }

            Vector2 center = m_Boss != null ? (Vector2)m_Boss.transform.position : Vector2.zero;

            for (int i = 0; i < 10; i++)
            {
                float angle = (i * 36f) * Mathf.Deg2Rad;
                Vector2 spawnPos = center + new Vector2(Mathf.Cos(angle) * 5f, Mathf.Sin(angle) * 5f);

                EnemyBrain minion = null;
                if (tinhLinhSamSetPrefab != null)
                {
                    GameObject go = Instantiate(tinhLinhSamSetPrefab, spawnPos, Quaternion.identity);
                    minion = go.GetComponent<EnemyBrain>();
                }
                else
                {
                    // Fallback tạo lightweight minion cho unit test nếu không gắn prefab
                    GameObject go = new GameObject($"TinhLinhSamSet_{i + 1}");
                    go.transform.position = spawnPos;
                    minion = go.AddComponent<LightweightTestMinion>();
                    minion.Initialize(new EnemyStats { MaxHealth = 100f, Attack = 20f });
                }

                if (minion != null)
                {
                    minion.gameObject.layer = 7;
                    var col = minion.GetComponent<CircleCollider2D>();
                    if (col == null)
                    {
                        col = minion.gameObject.AddComponent<CircleCollider2D>();
                        col.radius = 0.5f;
                    }
                    var owner = minion.GetComponent<ColliderOwnerRef>();
                    if (owner == null)
                    {
                        owner = minion.gameObject.AddComponent<ColliderOwnerRef>();
                    }
                    owner.SetOwner(minion.gameObject);

                    var sr = minion.GetComponent<SpriteRenderer>();
                    if (sr == null)
                    {
                        sr = minion.gameObject.AddComponent<SpriteRenderer>();
                        sr.sprite = SpriteFactory.Circle;
                        sr.color = new Color(0.9f, 0.95f, 1f, 0.9f);
                        sr.transform.localScale = Vector3.one * 0.8f;
                        sr.sortingOrder = 5;
                    }

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

            Vector2 center = m_Boss != null ? (Vector2)m_Boss.transform.position : Vector2.zero;
            m_RedTransmitterPos = center + new Vector2(-6f, 0f);
            m_RedReceiverPos = center + new Vector2(6f, 0f);

            CleanupRedVisuals();
            GameObject tx = new GameObject("RedElectric_Transmitter");
            tx.transform.position = m_RedTransmitterPos;
            tx.transform.SetParent(transform);
            var txSr = tx.AddComponent<SpriteRenderer>();
            txSr.sprite = SpriteFactory.Circle;
            txSr.color = new Color(1f, 0.2f, 0.2f, 1f);
            txSr.transform.localScale = Vector3.one * 1.2f;
            txSr.sortingOrder = 6;
            m_RedObjects.Add(tx);

            GameObject rx = new GameObject("RedElectric_Receiver");
            rx.transform.position = m_RedReceiverPos;
            rx.transform.SetParent(transform);
            var rxSr = rx.AddComponent<SpriteRenderer>();
            rxSr.sprite = SpriteFactory.Circle;
            rxSr.color = new Color(0.8f, 0.1f, 0.3f, 1f);
            rxSr.transform.localScale = Vector3.one * 1.2f;
            rxSr.sortingOrder = 6;
            m_RedObjects.Add(rx);
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
        }

        public void ManualTickRed(float dt)
        {
            if (!m_RedActive || m_RedResolved) return;

            m_RedTimer += dt;
            m_RedTickTimer += dt;

            while (m_RedTickTimer >= 0.5f)
            {
                m_RedTickTimer -= 0.5f;
                TickRedTransmission();
            }

            if (m_RedTimer >= 10f)
            {
                m_RedResolved = true;
                m_RedActive = false;

                // Nếu trong 10s thu điện chưa tích được 5 điểm -> Hoàn thành
                if (m_RedEnergyPoints < 5)
                {
                    MarkChallengeComplete(D1onkPillarColor.Red);
                }
                else
                {
                    Debug.Log($"[D1onkCoopChallenges] Thử thách Cột Đỏ thất bại! Thu điện đã tích {m_RedEnergyPoints} điểm.");
                }
            }
        }

        private void TickRedTransmission()
        {
            bool isBlocked = false;

            // Kiểm tra Tank có đứng chặn đường truyền giữa Transmitter và Receiver không
            var allTanks = UnityEngine.Object.FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            float bossAtk = (m_Boss != null && m_Boss.Stats != null) ? m_Boss.Stats.Attack : 500f;
            float blockDamage = bossAtk * 0.2f;

            for (int i = 0; i < allTanks.Length; i++)
            {
                TankBrain tank = allTanks[i];
                if (tank != null && tank.IsAlive)
                {
                    // Chỉ xét Tank ngoài sân (không phải tank bị giam trong Sub-Arena x > 150)
                    if (tank.transform.position.x < 150f && IsPointNearSegment(tank.transform.position, m_RedTransmitterPos, m_RedReceiverPos, 1.2f))
                    {
                        isBlocked = true;
                        DamageInfo blockDmg = new DamageInfo(m_Boss, tank, blockDamage, 0f, tank.transform.position, sourceType: DamageSourceType.EnemyAttack);
                        tank.TakeDamage(blockDmg);
                        TextDamageManager.Instance?.Show(blockDmg.FinalDamage, tank.transform.position);
                    }
                }
            }

            // Kiểm tra vật cản hoặc tường (loại trừ chính Boss và các collider thuộc Boss)
            if (!isBlocked)
            {
                RaycastHit2D hit = Physics2D.Linecast(m_RedTransmitterPos, m_RedReceiverPos);
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    bool isBossCollider = (m_Boss != null && (hit.collider.gameObject == m_Boss.gameObject || hit.collider.transform.IsChildOf(m_Boss.transform)));
                    if (!isBossCollider)
                    {
                        isBlocked = true;
                    }
                }
            }

            if (!isBlocked)
            {
                m_RedEnergyPoints++;
            }
        }

        #endregion

        #region 3. Yellow Challenge (2 Ký Sinh Thiên Thạch nổ phá 2 cột điện trong 10s)

        private void StartYellowChallenge()
        {
            if (m_YellowActive || IsYellowCompleted) return;
            m_YellowActive = true;
            m_YellowTimer = 0f;
            m_YellowPillarsRemaining = 2;
            m_YellowResolved = false;
            m_YellowPillarPositions.Clear();
            m_YellowParasites.Clear();
            CleanupYellowPillars();

            if (kySinhThienThachPrefab == null)
            {
                kySinhThienThachPrefab = FindNormalPrefab("KySinhThienThach");
            }

            Vector2 center = m_Boss != null ? (Vector2)m_Boss.transform.position : Vector2.zero;
            m_YellowPillarPositions.Add(center + new Vector2(-4f, 3f));
            m_YellowPillarPositions.Add(center + new Vector2(4f, 3f));

            for (int i = 0; i < m_YellowPillarPositions.Count; i++)
            {
                GameObject yp = new GameObject($"YellowElectricPillar_{i + 1}");
                yp.transform.position = m_YellowPillarPositions[i];
                yp.transform.SetParent(transform);

                yp.layer = 7; // Enemy
                var col = yp.AddComponent<CircleCollider2D>();
                col.radius = 0.8f;
                col.isTrigger = true;

                var sr = yp.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Circle;
                sr.color = new Color(1f, 0.9f, 0.1f, 1f);
                sr.transform.localScale = Vector3.one * 1.5f;
                sr.sortingOrder = 5;

                m_YellowPillarObjects.Add(yp);
            }

            // Spawn 2 Ký sinh thiên thạch
            for (int i = 0; i < 2; i++)
            {
                Vector2 spawnPos = center + new Vector2(i == 0 ? -4f : 4f, 5f);
                if (kySinhThienThachPrefab != null)
                {
                    GameObject go = Instantiate(kySinhThienThachPrefab, spawnPos, Quaternion.identity);
                    var parasite = go.GetComponent<NormalEnemy_KySinhThienThach>();
                    if (parasite != null) m_YellowParasites.Add(parasite);
                }
            }
        }

        public void DestroyYellowElectricPillar(int index)
        {
            if (!m_YellowActive || m_YellowResolved) return;

            if (m_YellowPillarObjects.Count > 0)
            {
                int targetIdx = Mathf.Clamp(index, 0, m_YellowPillarObjects.Count - 1);
                var obj = m_YellowPillarObjects[targetIdx];
                m_YellowPillarObjects.RemoveAt(targetIdx);
                if (obj != null)
                {
                    if (Application.isPlaying) Destroy(obj);
                    else DestroyImmediate(obj);
                }
            }

            m_YellowPillarsRemaining = Mathf.Max(0, m_YellowPillarsRemaining - 1);
            if (m_YellowPillarsRemaining == 0 && m_YellowTimer <= 10f)
            {
                m_YellowResolved = true;
                m_YellowActive = false;
                MarkChallengeComplete(D1onkPillarColor.Yellow);
            }
        }

        private void CleanupYellowPillars()
        {
            for (int i = 0; i < m_YellowPillarObjects.Count; i++)
            {
                if (m_YellowPillarObjects[i] != null)
                {
                    if (Application.isPlaying) Destroy(m_YellowPillarObjects[i]);
                    else DestroyImmediate(m_YellowPillarObjects[i]);
                }
            }
            m_YellowPillarObjects.Clear();
        }

        public void ManualTickYellow(float dt)
        {
            if (!m_YellowActive || m_YellowResolved) return;

            m_YellowTimer += dt;

            // Kiểm tra Ký sinh thiên thạch phát nổ gần cột điện
            for (int p = m_YellowParasites.Count - 1; p >= 0; p--)
            {
                var parasite = m_YellowParasites[p];
                if (parasite == null || parasite.HasDetonated || !parasite.IsAlive)
                {
                    Vector2 expPos = parasite != null ? (Vector2)parasite.transform.position : Vector2.zero;
                    if (parasite != null) m_YellowParasites.RemoveAt(p);

                    for (int c = m_YellowPillarPositions.Count - 1; c >= 0; c--)
                    {
                        if (Vector2.Distance(expPos, m_YellowPillarPositions[c]) <= 3.5f)
                        {
                            m_YellowPillarPositions.RemoveAt(c);
                            DestroyYellowElectricPillar(c);
                            break;
                        }
                    }
                }
            }

            if (m_YellowTimer >= 10f)
            {
                m_YellowResolved = true;
                m_YellowActive = false;
                if (m_YellowPillarsRemaining == 0)
                {
                    MarkChallengeComplete(D1onkPillarColor.Yellow);
                }
            }
        }

        #endregion

        #region 4. Blue Challenge (Diệt sạch bản sao Tank trong 10s)

        private void StartBlueChallenge()
        {
            if (m_BlueActive || IsBlueCompleted) return;
            m_BlueActive = true;
            m_BlueTimer = 0f;
            m_BlueResolved = false;

            CheckBlueProgress();
        }

        public void ManualTickBlue(float dt)
        {
            if (!m_BlueActive || m_BlueResolved) return;

            m_BlueTimer += dt;

            CheckBlueProgress();

            if (m_BlueTimer >= 10f && !m_BlueResolved)
            {
                m_BlueResolved = true;
                m_BlueActive = false;
                CheckBlueProgress();
            }
        }

        private void CheckBlueProgress()
        {
            if (!m_BlueActive || m_BlueResolved) return;

            bool allClonesDefeated = true;
            for (int i = 0; i < m_OutsideTankClones.Count; i++)
            {
                if (m_OutsideTankClones[i] != null && !m_OutsideTankClones[i].IsDefeated)
                {
                    allClonesDefeated = false;
                    break;
                }
            }

            if (allClonesDefeated)
            {
                m_BlueResolved = true;
                m_BlueActive = false;
                MarkChallengeComplete(D1onkPillarColor.Blue);
            }
        }

        #endregion

        public void ManualTick(float dt)
        {
            if (m_WhiteActive) CheckWhiteChallengeProgress();
            if (m_RedActive) ManualTickRed(dt);
            if (m_YellowActive) ManualTickYellow(dt);
            if (m_BlueActive) ManualTickBlue(dt);
        }

        private void MarkChallengeComplete(D1onkPillarColor color)
        {
            if (m_CompletedColors.Contains(color)) return;

            m_CompletedColors.Add(color);
            m_CompletedChallengesCount++;

            Debug.Log($"[D1onkCoopChallenges] Thử thách {color} HOÀN THÀNH! ({m_CompletedChallengesCount}/3)");
            OnChallengeCompleted?.Invoke(color);

            if (m_CompletedChallengesCount >= 3)
            {
                Debug.Log("[D1onkCoopChallenges] ĐÃ HOÀN THÀNH 3 THỬ THÁCH! Kích hoạt xóa khiên Bất Hoại của Trái Tim Boss!");
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
            CleanupYellowPillars();

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
