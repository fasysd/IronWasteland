using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Tinh Linh Sấm Sét Nhỏ - Kẻ địch thường có di chuyển và tìm đường (kế thừa NormalEnemy_BaseChaser).
    /// Kỹ năng đặc trưng:
    /// - Tấn công tầm xa giật sét gây 100% sức tấn công (Stats.Attack) lên mục tiêu.
    /// - Sét lan truyền (Chain Lightning) sang các mục tiêu khác nếu đứng cạnh trong bán kính chainJumpDistance.
    /// - Lan truyền KHÔNG bị giới hạn bởi phạm vi A và B của quái vật.
    /// - Số lần truyền và số lượng mục tiêu lan là KHÔNG GIỚI HẠN (1 -> 2 -> 3 -> ... -> n).
    /// - Mỗi mục tiêu chỉ bị giật sét duy nhất một lần trong một lần thi triển kỹ năng.
    /// - Sử dụng hình chữ nhật dài (Rectangle) biểu diễn tia sét giữa các mục tiêu.
    /// - Quản lý va chạm và quét mục tiêu 100% thông qua ColliderOwnerRef.
    /// - Phân tách trách nhiệm: EnemyBrain trực tiếp kích hoạt sát thương và hiển thị TextDamageManager.
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_TinhLinhSamSetNho : NormalEnemy_BaseChaser
    {
        [Header("Lightning Skill Settings")]
        [Tooltip("Template tia sét hình chữ nhật (nằm trong Children của Prefab hoặc gán thủ công).")]
        [SerializeField] private LightningBeam_TinhLinhSamSetNho beamTemplate;

        [Tooltip("Template scanner quét mục tiêu lân cận (nằm trong Children của Prefab hoặc gán thủ công).")]
        [SerializeField] private LightningChainScanner_TinhLinhSamSetNho scannerTemplate;

        [Tooltip("Khoảng cách tối đa để bắt đầu tấn công mục tiêu đầu tiên (m). Mặc định theo bán kính A.")]
        [SerializeField, Min(0.5f)] private float attackRange = 5.0f;

        [Tooltip("Thời gian hồi kỹ năng giữa các lần giật sét (giây).")]
        [SerializeField, Min(1f)] private float skillCooldown = 4.0f;

        [Tooltip("Tỷ lệ sát thương gây ra dựa trên Stats.Attack (1.0 = 100% Attack).")]
        [SerializeField, Min(0.1f)] private float damageMultiplier = 1.0f;

        [Tooltip("Khoảng cách tối đa giữa 2 mục tiêu để sét có thể nhảy lan sang nhau (m). Không phụ thuộc phạm vi A, B.")]
        [SerializeField, Min(0.5f)] private float chainJumpDistance = 4.0f;

        [Tooltip("Khoảng nghỉ nhỏ giữa các bước nhảy sét để người chơi quan sát rõ tia sét truyền lan (giây).")]
        [SerializeField, Min(0f)] private float chainJumpDelay = 0.08f;

        [Header("Beam Visual Appearance")]
        [Tooltip("Độ dày hình chữ nhật tia sét.")]
        [SerializeField, Min(0.05f)] private float beamThickness = 0.2f;

        [Tooltip("Thời gian hiển thị của tia sét trước khi mờ hẳn (giây).")]
        [SerializeField, Min(0.05f)] private float beamDuration = 0.25f;

        [Tooltip("Màu sắc tia sét.")]
        [SerializeField] private Color beamColor = new Color(0.35f, 0.85f, 1.0f, 0.95f);

        [Header("Runtime Info")]
        [SerializeField, ReadOnly] private float currentCooldownTimer;
        [SerializeField, ReadOnly] private bool isCasting;
        [SerializeField, ReadOnly] private int lastChainHitCount;

        private Coroutine m_CastCoroutine;
        private LightningChainScanner_TinhLinhSamSetNho m_ActiveScanner;
        private readonly Queue<LightningBeam_TinhLinhSamSetNho> m_BeamPool = new Queue<LightningBeam_TinhLinhSamSetNho>();
        private readonly List<LightningBeam_TinhLinhSamSetNho> m_ActiveBeams = new List<LightningBeam_TinhLinhSamSetNho>();

        public LightningBeam_TinhLinhSamSetNho BeamTemplate { get => beamTemplate; set => beamTemplate = value; }
        public LightningChainScanner_TinhLinhSamSetNho ScannerTemplate { get => scannerTemplate; set => scannerTemplate = value; }
        public float AttackRange { get => attackRange; set => attackRange = value; }
        public float SkillCooldown { get => skillCooldown; set => skillCooldown = value; }
        public float DamageMultiplier { get => damageMultiplier; set => damageMultiplier = value; }
        public float ChainJumpDistance { get => chainJumpDistance; set => chainJumpDistance = value; }
        public float ChainJumpDelay { get => chainJumpDelay; set => chainJumpDelay = value; }
        public float CurrentCooldownTimer => currentCooldownTimer;
        public bool IsCasting => isCasting;
        public int LastChainHitCount => lastChainHitCount;

        protected override void Awake()
        {
            base.Awake();

            EnsureTemplates();
        }

        private void EnsureTemplates()
        {
            if (beamTemplate == null)
            {
                beamTemplate = GetComponentInChildren<LightningBeam_TinhLinhSamSetNho>(true);
            }

            if (beamTemplate != null)
            {
                beamTemplate.gameObject.SetActive(false);
            }

            if (scannerTemplate == null)
            {
                scannerTemplate = GetComponentInChildren<LightningChainScanner_TinhLinhSamSetNho>(true);
            }

            if (scannerTemplate != null)
            {
                scannerTemplate.gameObject.SetActive(false);
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            base.OnBrainTick(deltaTime);

            if (currentCooldownTimer > 0f)
            {
                currentCooldownTimer = Mathf.Max(0f, currentCooldownTimer - deltaTime);
            }

            // Kiểm tra điều kiện thi triển kỹ năng khi hết hồi chiêu và có mục tiêu trong tầm
            if (!isCasting && currentCooldownTimer <= 0f && CanCastSkillAtCurrentTarget())
            {
                StartCastSkill();
            }
        }

        private bool CanCastSkillAtCurrentTarget()
        {
            if (currentTarget == null) return false;

            float dist = Vector2.Distance(transform.position, currentTarget.position);
            // Có thể tấn công nếu mục tiêu nằm trong tầm attackRange (hoặc bán kính radiusA)
            return dist <= Mathf.Max(attackRange, radiusA);
        }

        public void StartCastSkill()
        {
            if (isCasting) return;

            if (m_CastCoroutine != null)
            {
                StopCoroutine(m_CastCoroutine);
            }
            m_CastCoroutine = StartCoroutine(ChainLightningRoutine());
        }

        private IEnumerator ChainLightningRoutine()
        {
            isCasting = true;
            movementAgent?.Stop();

            TankBrain initialTarget = ResolveTargetTank(currentTarget);
            if (initialTarget == null || !initialTarget.IsAlive)
            {
                isCasting = false;
                yield break;
            }

            EnsureScannerInstance();

            float damage = (Stats != null ? Stats.Attack : 10f) * damageMultiplier;
            var hitTanks = new HashSet<TankBrain>();
            var chainQueue = new Queue<TankBrain>();

            // 1. Tấn công mục tiêu ban đầu
            Vector2 enemyPos = transform.position;
            Vector2 targetPos = initialTarget.transform.position;

            SpawnBeam(enemyPos, targetPos);
            DealDamageToTarget(initialTarget, damage);

            hitTanks.Add(initialTarget);
            chainQueue.Enqueue(initialTarget);

            // 2. Lan truyền sấm sét vô hạn (1 -> 2 -> 3 -> ... -> n)
            var nearbyBuffer = new HashSet<TankBrain>();
            var sortedNeighbors = new List<TankBrain>();

            while (chainQueue.Count > 0)
            {
                TankBrain currentSource = chainQueue.Dequeue();
                if (currentSource == null) continue;

                Vector2 sourcePos = currentSource.transform.position;

                // Định vị scanner tại tâm của mục tiêu đang truyền sét và quét qua 100% ColliderOwnerRef
                if (m_ActiveScanner != null)
                {
                    m_ActiveScanner.PositionAt(sourcePos, chainJumpDistance);

                    // Chờ một khoảng rất ngắn nếu cần đồng bộ physics
                    yield return new WaitForFixedUpdate();

                    nearbyBuffer.Clear();
                    m_ActiveScanner.CollectNearbyTanks(nearbyBuffer);

                    // Lọc các mục tiêu còn sống và chưa từng bị giật sét
                    sortedNeighbors.Clear();
                    foreach (TankBrain neighbor in nearbyBuffer)
                    {
                        if (neighbor != null && neighbor.IsAlive && !hitTanks.Contains(neighbor))
                        {
                            sortedNeighbors.Add(neighbor);
                        }
                    }

                    // Sắp xếp ưu tiên các mục tiêu ở gần nguồn sét trước
                    sortedNeighbors.Sort((a, b) =>
                    {
                        float d1 = Vector2.Distance(sourcePos, a.transform.position);
                        float d2 = Vector2.Distance(sourcePos, b.transform.position);
                        return d1.CompareTo(d2);
                    });

                    // Truyền sét tới từng mục tiêu lân cận
                    for (int i = 0; i < sortedNeighbors.Count; i++)
                    {
                        TankBrain nextTarget = sortedNeighbors[i];
                        if (nextTarget == null || !nextTarget.IsAlive || hitTanks.Contains(nextTarget))
                        {
                            continue;
                        }

                        hitTanks.Add(nextTarget);

                        Vector2 nextPos = nextTarget.transform.position;
                        SpawnBeam(sourcePos, nextPos);
                        DealDamageToTarget(nextTarget, damage);

                        chainQueue.Enqueue(nextTarget);

                        if (chainJumpDelay > 0f)
                        {
                            yield return new WaitForSeconds(chainJumpDelay);
                        }
                    }
                }
            }

            if (m_ActiveScanner != null)
            {
                m_ActiveScanner.Deactivate();
            }

            lastChainHitCount = hitTanks.Count;
            currentCooldownTimer = skillCooldown;
            isCasting = false;
        }

        private void DealDamageToTarget(TankBrain tank, float damage)
        {
            if (tank == null || !tank.IsAlive) return;

            tank.TakeDamage(damage);

            Vector2 hitPos = tank.transform.position;
            TextDamageManager.Instance?.Show(damage, hitPos);
        }

        private void EnsureScannerInstance()
        {
            if (m_ActiveScanner != null) return;

            if (scannerTemplate != null)
            {
                m_ActiveScanner = Instantiate(scannerTemplate, transform);
                m_ActiveScanner.gameObject.name = "ChainScanner_Instance";
                m_ActiveScanner.gameObject.SetActive(false);
            }
            else
            {
                // Fallback tạo scanner nếu chưa có template
                var scannerGo = new GameObject("ChainScanner_RuntimeFallback");
                scannerGo.transform.SetParent(transform);
                var circleCol = scannerGo.AddComponent<CircleCollider2D>();
                circleCol.isTrigger = true;
                circleCol.radius = 0.5f;

                var ownerRef = scannerGo.AddComponent<ColliderOwnerRef>();
                ownerRef.SetOwner(this);

                var rb = scannerGo.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.simulated = true;
                rb.useFullKinematicContacts = true;

                m_ActiveScanner = scannerGo.AddComponent<LightningChainScanner_TinhLinhSamSetNho>();
                m_ActiveScanner.gameObject.SetActive(false);
            }
        }

        private LightningBeam_TinhLinhSamSetNho SpawnBeam(Vector2 startPos, Vector2 endPos)
        {
            LightningBeam_TinhLinhSamSetNho beam = null;

            while (m_BeamPool.Count > 0 && beam == null)
            {
                beam = m_BeamPool.Dequeue();
            }

            if (beam == null)
            {
                if (beamTemplate != null)
                {
                    beam = Instantiate(beamTemplate, transform.parent);
                }
                else
                {
                    var beamGo = new GameObject("LightningBeam_RuntimeFallback");
                    beamGo.transform.SetParent(transform.parent);
                    beam = beamGo.AddComponent<LightningBeam_TinhLinhSamSetNho>();
                }
            }

            m_ActiveBeams.Add(beam);
            beam.ShowBeam(
                startPos,
                endPos,
                thickness: beamThickness,
                duration: beamDuration,
                color: beamColor,
                onDespawn: OnBeamDespawned
            );

            return beam;
        }

        private void OnBeamDespawned(LightningBeam_TinhLinhSamSetNho beam)
        {
            if (beam == null) return;
            m_ActiveBeams.Remove(beam);
            m_BeamPool.Enqueue(beam);
        }

        private TankBrain ResolveTargetTank(Transform target)
        {
            if (target == null) return null;

            TankBrain tank = target.GetComponent<TankBrain>() ?? target.GetComponentInParent<TankBrain>();
            if (tank != null) return tank;

            ColliderOwnerRef ownerRef = target.GetComponent<ColliderOwnerRef>() ?? target.GetComponentInParent<ColliderOwnerRef>();
            if (ownerRef != null)
            {
                if (ownerRef.Owner is TankBrain tb) return tb;
                if (ownerRef.Owner is GameObject go) return go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                if (ownerRef.Owner is Component comp) return comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
            }

            return null;
        }

        protected override void OnDeath()
        {
            if (m_CastCoroutine != null)
            {
                StopCoroutine(m_CastCoroutine);
                m_CastCoroutine = null;
            }

            if (m_ActiveScanner != null)
            {
                m_ActiveScanner.Deactivate();
            }

            base.OnDeath();
        }

        private void OnDisable()
        {
            if (m_CastCoroutine != null)
            {
                StopCoroutine(m_CastCoroutine);
                m_CastCoroutine = null;
            }

            isCasting = false;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // Hiển thị tầm tấn công ban đầu (màu xanh Cyan)
            Gizmos.color = new Color(0f, 0.9f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Hiển thị bán kính nhảy sét tối đa giữa các mục tiêu (màu tím nhạt)
            Gizmos.color = new Color(0.7f, 0.4f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, chainJumpDistance);
        }
    }
}
