using System.Collections;
using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Quái Vật Đầm Lầy - Kẻ địch thường có di chuyển và tìm đường.
    /// Kỹ năng đặc trưng:
    /// - Khi mục tiêu trong phạm vi A và hết hồi chiêu: Lao vào mục tiêu với tốc độ cao.
    /// - Va chạm được kiểm soát hoàn toàn thông qua ColliderOwnerRef (Model hoặc Hitbox riêng).
    /// - Khi va chạm mục tiêu: gây 100% sát thương tấn công và tạo một vùng đầm lầy.
    /// - Vùng đầm lầy làm giảm 50% tốc độ xe tăng khi ở bên trong (hết làm chậm khi ra ngoài)
    ///   và tự động phát nổ sau một khoảng thời gian gây 100% sát thương tấn công.
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_QuaiVatDamLay : NormalEnemy_BaseChaser
    {
        [Header("Skill & Charge Settings")]
        [Tooltip("Template vùng đầm lầy (nằm trong Children của QuaiVatDamLay Prefab). Cấu hình bán kính, thời gian nổ và tỷ lệ làm chậm trực tiếp trên template này.")]
        [SerializeField] private SwampZone_QuaiVatDamLay zoneTemplate;

        [Tooltip("ColliderOwnerRef quản lý va chạm khi lao (nếu để trống sẽ tự động dùng ColliderOwnerRef của Model).")]
        [SerializeField] private ColliderOwnerRef chargeColliderRef;

        [Tooltip("Tốc độ khi lao vào mục tiêu (m/s).")]
        [SerializeField, Min(5f)] private float chargeSpeed = 10f;

        [Tooltip("Thời gian chuẩn bị trước khi phóng người lao đi (giây).")]
        [SerializeField, Min(0f)] private float chargeWindup = 0.25f;

        [Tooltip("Thời gian hồi kỹ năng giữa các lần lao (giây).")]
        [SerializeField, Min(1f)] private float skillCooldown = 5f;

        [Header("Runtime Info")]
        [SerializeField, ReadOnly] private float currentCooldownTimer;
        [SerializeField, ReadOnly] private bool isCharging;
        [SerializeField, ReadOnly] private int activeZoneCount;
        [SerializeField, ReadOnly] private int pooledZoneCount;

        private Coroutine m_ChargeCoroutine;
        private TankBrain m_HitTankInCharge;
        private readonly Queue<SwampZone_QuaiVatDamLay> m_ZonePool = new Queue<SwampZone_QuaiVatDamLay>();
        private readonly List<SwampZone_QuaiVatDamLay> m_ActiveZones = new List<SwampZone_QuaiVatDamLay>();

        public SwampZone_QuaiVatDamLay ZoneTemplate { get => zoneTemplate; set => zoneTemplate = value; }
        public ColliderOwnerRef ChargeColliderRef { get => chargeColliderRef; set => chargeColliderRef = value; }
        public float ChargeSpeed { get => chargeSpeed; set => chargeSpeed = value; }
        public float ChargeWindup { get => chargeWindup; set => chargeWindup = value; }
        public float SkillCooldown { get => skillCooldown; set => skillCooldown = value; }
        public float CurrentCooldownTimer => currentCooldownTimer;
        public bool IsCharging => isCharging;
        public int ActiveZoneCount => m_ActiveZones.Count;
        public int PooledZoneCount => m_ZonePool.Count;

        protected override void Awake()
        {
            base.Awake();

            if (zoneTemplate == null)
            {
                zoneTemplate = GetComponentInChildren<SwampZone_QuaiVatDamLay>(true);
            }

            if (zoneTemplate != null)
            {
                zoneTemplate.gameObject.SetActive(false);
            }

            if (chargeColliderRef == null && Model != null)
            {
                chargeColliderRef = Model.OwnerRef;
            }
        }

        private void OnEnable()
        {
            if (chargeColliderRef != null && (Model == null || chargeColliderRef != Model.OwnerRef))
            {
                chargeColliderRef.Contact += HandleChargeContact;
            }
        }

        private void OnDisable()
        {
            if (chargeColliderRef != null && (Model == null || chargeColliderRef != Model.OwnerRef))
            {
                chargeColliderRef.Contact -= HandleChargeContact;
            }

            m_HitTankInCharge = null;
        }

        protected override void OnContact(ColliderOwnerRef other)
        {
            base.OnContact(other);
            HandleChargeContact(other);
        }

        private void HandleChargeContact(ColliderOwnerRef other)
        {
            if (isCharging && m_HitTankInCharge == null && other != null)
            {
                TankBrain tank = ResolveTankBrain(other);
                if (tank != null && tank.IsAlive)
                {
                    m_HitTankInCharge = tank;
                }
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            if (currentCooldownTimer > 0f)
            {
                currentCooldownTimer = Mathf.Max(0f, currentCooldownTimer - deltaTime);
            }

            // Nếu đang trong quá trình lao: không chạy logic tìm đường thông thường
            if (isCharging)
            {
                movementAgent?.Stop();
                return;
            }

            repathTimer -= deltaTime;
            if (movementAgent != null && Stats != null)
            {
                movementAgent.SetSpeed(Stats.MoveSpeed);
            }

            // 1. Quét tìm và kiểm tra mục tiêu
            currentTarget = ScanAndValidateTarget();

            Vector2 currentPos = transform.position;
            float distToHome = Vector2.Distance(currentPos, anchorPointD);

            if (currentTarget != null)
            {
                float distToTarget = Vector2.Distance(currentPos, currentTarget.position);

                // Khi mục tiêu nằm trong tầm A:
                if (distToTarget <= radiusA)
                {
                    movementAgent?.Stop();

                    // Nếu hết hồi chiêu: bắt đầu pha lao vào mục tiêu
                    if (currentCooldownTimer <= 0f)
                    {
                        StartCharge(currentTarget.position);
                        return;
                    }

                    // Nếu đang hồi chiêu: đứng yên quan sát
                    return;
                }

                // Khi mục tiêu nằm ngoài tầm A: tiếp tục tìm đường tiếp cận
                HandleTargetTracking(currentPos, currentTarget.position);

                // Nếu vừa vào tầm A và hết hồi chiêu -> lao ngay
                if (distToTarget <= radiusA && currentCooldownTimer <= 0f)
                {
                    movementAgent?.Stop();
                    StartCharge(currentTarget.position);
                }
            }
            else
            {
                HandleReturnHomeLogic(currentPos, distToHome, deltaTime);
            }

            activeZoneCount = m_ActiveZones.Count;
            pooledZoneCount = m_ZonePool.Count;
        }

        public void StartCharge(Vector2 targetPosition)
        {
            if (isCharging) return;

            if (m_ChargeCoroutine != null)
            {
                StopCoroutine(m_ChargeCoroutine);
            }

            m_ChargeCoroutine = StartCoroutine(ChargeRoutine(targetPosition));
        }

        private IEnumerator ChargeRoutine(Vector2 targetPosition)
        {
            isCharging = true;
            m_HitTankInCharge = null;
            movementAgent?.Stop();

            // 1. Xoay người về phía mục tiêu
            RotateTowards(targetPosition);

            // 2. Chờ chuẩn bị lao (windup)
            if (chargeWindup > 0f)
            {
                yield return new WaitForSeconds(chargeWindup);
            }

            // 3. Thực hiện pha lao nhanh tới vị trí mục tiêu
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            float maxChargeDuration = 1.2f;
            float elapsed = 0f;

            while (elapsed < maxChargeDuration)
            {
                elapsed += Time.deltaTime;
                Vector2 currentPos = transform.position;
                Vector2 nextPos = Vector2.MoveTowards(currentPos, targetPosition, chargeSpeed * Time.deltaTime);

                if (body != null)
                {
                    body.MovePosition(nextPos);
                }
                transform.position = nextPos;

                // Kiểm tra va chạm xe tăng thông qua ColliderOwnerRef
                TankBrain hitTank = m_HitTankInCharge ?? CheckTankFromOwnerRef();
                if (hitTank != null && hitTank.IsAlive)
                {
                    // Dừng pha lao ngay khi va chạm mục tiêu
                    break;
                }

                if (Vector2.Distance(nextPos, targetPosition) <= 0.25f)
                {
                    break;
                }

                yield return null;
            }

            // 4. Tạo vùng đầm lầy tại vị trí kết thúc pha lao
            SpawnSwampZone(transform.position);

            // 5. Kết thúc pha lao và kích hoạt hồi chiêu
            isCharging = false;
            m_HitTankInCharge = null;
            m_ChargeCoroutine = null;
            currentCooldownTimer = skillCooldown;
        }

        private TankBrain CheckTankFromOwnerRef()
        {
            ColliderOwnerRef targetOwnerRef = chargeColliderRef != null ? chargeColliderRef : (Model != null ? Model.OwnerRef : null);
            if (targetOwnerRef == null) return null;

            foreach (Collider2D col in targetOwnerRef.GetTriggerColliders())
            {
                if (col == null) continue;
                TankBrain tank = ResolveTankBrain(col);
                if (tank != null && tank.IsAlive)
                {
                    return tank;
                }
            }

            return null;
        }

        private void SpawnSwampZone(Vector2 position)
        {
            if (zoneTemplate == null) return;

            SwampZone_QuaiVatDamLay zone = null;

            while (m_ZonePool.Count > 0)
            {
                SwampZone_QuaiVatDamLay candidate = m_ZonePool.Dequeue();
                if (candidate != null)
                {
                    zone = candidate;
                    break;
                }
            }

            if (zone == null)
            {
                zone = Instantiate(zoneTemplate, null);
                zone.OnTargetsDetected += HandleZoneTargetsDetected;
            }

            zone.SpawnAt(position, ReturnZoneToPool);
            m_ActiveZones.Add(zone);
        }

        private void HandleZoneTargetsDetected(SwampZone_QuaiVatDamLay zone, HashSet<TankBrain> targets)
        {
            if (targets == null || targets.Count == 0) return;

            float damage = Stats != null ? Stats.Attack : 10f;
            foreach (TankBrain tank in targets)
            {
                if (tank != null && tank.IsAlive)
                {
                    tank.TakeDamage(damage);
                    TextDamageManager.Instance?.Show(damage, tank.transform.position);
                }
            }
        }

        private void ReturnZoneToPool(SwampZone_QuaiVatDamLay zone)
        {
            if (zone == null) return;

            m_ActiveZones.Remove(zone);
            m_ZonePool.Enqueue(zone);
        }

        public void RotateTowards(Vector2 targetPosition)
        {
            Vector2 dir = targetPosition - (Vector2)transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float offset = movementAgent != null ? movementAgent.RotationOffset : 90f;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + offset;
                Transform targetTransform = Model != null ? Model.transform : transform;
                targetTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private TankBrain ResolveTankBrain(ColliderOwnerRef targetRef)
        {
            if (targetRef == null) return null;

            if (targetRef.TryGetOwner<TankBrain>(out TankBrain brain) && brain != null)
            {
                return brain;
            }

            if (targetRef.Owner is TankBrain directBrain)
            {
                return directBrain;
            }

            if (targetRef.Owner is Component comp)
            {
                TankBrain compBrain = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                if (compBrain != null) return compBrain;
            }
            else if (targetRef.Owner is GameObject go)
            {
                TankBrain goBrain = go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                if (goBrain != null) return goBrain;
            }

            return null;
        }

        private TankBrain ResolveTankBrain(Collider2D col)
        {
            if (col == null) return null;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
            if (targetRef != null)
            {
                TankBrain fromRef = ResolveTankBrain(targetRef);
                if (fromRef != null) return fromRef;
            }

            return col.GetComponentInParent<TankBrain>();
        }

        protected override void Die()
        {
            if (m_ChargeCoroutine != null)
            {
                StopCoroutine(m_ChargeCoroutine);
                m_ChargeCoroutine = null;
            }
            isCharging = false;
            m_HitTankInCharge = null;

            base.Die();
        }

        protected virtual void OnDestroy()
        {
            while (m_ZonePool.Count > 0)
            {
                SwampZone_QuaiVatDamLay pooled = m_ZonePool.Dequeue();
                if (pooled != null && pooled != zoneTemplate)
                {
                    pooled.OnTargetsDetected -= HandleZoneTargetsDetected;
                    Destroy(pooled.gameObject);
                }
            }

            for (int i = 0; i < m_ActiveZones.Count; i++)
            {
                if (m_ActiveZones[i] != null && m_ActiveZones[i] != zoneTemplate)
                {
                    m_ActiveZones[i].OnTargetsDetected -= HandleZoneTargetsDetected;
                    Destroy(m_ActiveZones[i].gameObject);
                }
            }
            m_ActiveZones.Clear();
        }
    }
}
