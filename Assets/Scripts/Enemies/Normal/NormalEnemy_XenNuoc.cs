using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Xén Nước - Kẻ địch thường có di chuyển và tìm đường (kế thừa NormalEnemy_BaseChaser).
    /// Kỹ năng đặc trưng:
    /// - Khi mục tiêu trong tầm tiếp cận (bán kính A) và hết hồi chiêu: Khóa hướng và lao thẳng với tốc độ lớn theo một quãng đường cố định.
    /// - Nếu trúng Xe Tăng: Gây 100% sát thương tấn công, ghi nhận thông báo đẩy văng Xe Tăng, dừng lại và bản thân lùi lại một đoạn để giữ khoảng cách.
    /// - Nếu trúng Địa hình: Dừng lại và bản thân lùi lại một đoạn để giữ khoảng cách.
    /// - Nếu không trúng gì cả: Lao đến điểm dừng rồi thôi (không lùi lại).
    /// - Sử dụng 100% ColliderOwnerRef để phát hiện va chạm.
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemy_XenNuoc : NormalEnemy_BaseChaser
    {
        [Header("Charge Skill Settings")]
        [Tooltip("ColliderOwnerRef quản lý va chạm khi lao (nếu để trống sẽ tự động lấy từ Model).")]
        [SerializeField] private ColliderOwnerRef chargeColliderRef;

        [Tooltip("Tốc độ khi phóng người lao đi (m/s).")]
        [SerializeField, Min(5f)] private float chargeSpeed = 12.0f;

        [Tooltip("Quãng đường lao cố định theo hướng mục tiêu (m).")]
        [SerializeField, Min(1f)] private float maxChargeDistance = 7.0f;

        [Tooltip("Thời gian chuẩn bị khựng lại trước khi lao (giây).")]
        [SerializeField, Min(0f)] private float chargeWindup = 0.25f;

        [Tooltip("Khoảng cách bản thân lùi lại phía sau khi va chạm mục tiêu hoặc địa hình (m).")]
        [SerializeField, Min(0.2f)] private float recoilDistance = 1.2f;

        [Tooltip("Tốc độ khi giật lùi về phía sau (m/s).")]
        [SerializeField, Min(1f)] private float recoilSpeed = 5.0f;

        [Tooltip("Khoảng cách dự kiến đẩy lùi Xe Tăng (m).")]
        [SerializeField, Min(0.5f)] private float knockbackDistance = 2.5f;

        [Tooltip("Tỷ lệ sát thương gây ra khi đâm trúng Xe Tăng (1.0 = 100% Stats.Attack).")]
        [SerializeField, Min(0.1f)] private float damageMultiplier = 1.0f;

        [Tooltip("Thời gian hồi kỹ năng giữa các lần lao (giây).")]
        [SerializeField, Min(1f)] private float skillCooldown = 4.5f;

        [Header("Runtime Info")]
        [SerializeField, ReadOnly] private float currentCooldownTimer;
        [SerializeField, ReadOnly] private bool isCharging;
        [SerializeField, ReadOnly] private bool isRecoiling;

        private Coroutine m_ChargeCoroutine;
        private Rigidbody2D m_Rigidbody;
        private TankBrain m_HitTank;
        private bool m_HitTerrain;

        public ColliderOwnerRef ChargeColliderRef { get => chargeColliderRef; set => chargeColliderRef = value; }
        public float ChargeSpeed { get => chargeSpeed; set => chargeSpeed = value; }
        public float MaxChargeDistance { get => maxChargeDistance; set => maxChargeDistance = value; }
        public float ChargeWindup { get => chargeWindup; set => chargeWindup = value; }
        public float RecoilDistance { get => recoilDistance; set => recoilDistance = value; }
        public float RecoilSpeed { get => recoilSpeed; set => recoilSpeed = value; }
        public float KnockbackDistance { get => knockbackDistance; set => knockbackDistance = value; }
        public float DamageMultiplier { get => damageMultiplier; set => damageMultiplier = value; }
        public float SkillCooldown { get => skillCooldown; set => skillCooldown = value; }
        public float CurrentCooldownTimer => currentCooldownTimer;
        public bool IsCharging => isCharging;
        public bool IsRecoiling => isRecoiling;

        protected override void Awake()
        {
            base.Awake();

            m_Rigidbody = GetComponent<Rigidbody2D>();

            if (chargeColliderRef == null && Model != null)
            {
                chargeColliderRef = Model.OwnerRef;
            }

            if (chargeColliderRef != null && !chargeColliderRef.HasExplicitOwner)
            {
                chargeColliderRef.SetOwner(this);
            }
        }

        private void OnEnable()
        {
            RegisterColliderEvents();
        }

        private void OnDisable()
        {
            UnregisterColliderEvents();
            m_HitTank = null;
            m_HitTerrain = false;
        }

        private void RegisterColliderEvents()
        {
            ColliderOwnerRef colRef = GetActiveColliderRef();
            if (colRef != null)
            {
                colRef.TriggerEnter += HandleColliderTriggerEnter;
                colRef.CollisionEnter += HandleColliderCollisionEnter;
            }
        }

        private void UnregisterColliderEvents()
        {
            ColliderOwnerRef colRef = GetActiveColliderRef();
            if (colRef != null)
            {
                colRef.TriggerEnter -= HandleColliderTriggerEnter;
                colRef.CollisionEnter -= HandleColliderCollisionEnter;
            }
        }

        private ColliderOwnerRef GetActiveColliderRef()
        {
            return chargeColliderRef != null ? chargeColliderRef : (Model != null ? Model.OwnerRef : null);
        }

        private void HandleColliderTriggerEnter(Collider2D other)
        {
            if (!isCharging || other == null) return;
            ProcessHitCollider(other);
        }

        private void HandleColliderCollisionEnter(Collision2D collision)
        {
            if (!isCharging || collision == null || collision.collider == null) return;
            ProcessHitCollider(collision.collider);
        }

        private void ProcessHitCollider(Collider2D col)
        {
            if (!isCharging || col == null) return;

            // 1. Kiểm tra va chạm Xe Tăng
            if (m_HitTank == null)
            {
                TankBrain tank = ResolveTankBrain(col);
                if (tank != null && tank.IsAlive)
                {
                    m_HitTank = tank;
                    return;
                }
            }

            // 2. Kiểm tra va chạm Địa hình (Terrain)
            if (!m_HitTerrain)
            {
                if (terrainLayer.value != 0 && (terrainLayer.value & (1 << col.gameObject.layer)) != 0)
                {
                    m_HitTerrain = true;
                }
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            if (currentCooldownTimer > 0f)
            {
                currentCooldownTimer = Mathf.Max(0f, currentCooldownTimer - deltaTime);
            }

            // Nếu đang trong pha lao hoặc đang lùi: dừng logic di chuyển tìm đường thông thường
            if (isCharging || isRecoiling)
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

                    // Nếu hết hồi chiêu: bắt đầu phóng người lao vào mục tiêu
                    if (currentCooldownTimer <= 0f)
                    {
                        StartCharge(currentTarget.position);
                        return;
                    }

                    // Nếu đang trong hồi chiêu: đứng quan sát
                    return;
                }

                // Khi mục tiêu nằm ngoài tầm A: tiếp tục tìm đường tiếp cận
                HandleTargetTracking(currentPos, currentTarget.position);

                // Nếu vừa di chuyển vào tầm A và hết hồi chiêu -> lao ngay
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
        }

        public void StartCharge(Vector2 targetPosition)
        {
            if (isCharging || isRecoiling) return;

            if (m_ChargeCoroutine != null)
            {
                StopCoroutine(m_ChargeCoroutine);
            }
            m_ChargeCoroutine = StartCoroutine(ChargeSequenceRoutine(targetPosition));
        }

        private IEnumerator ChargeSequenceRoutine(Vector2 targetPosition)
        {
            isCharging = true;
            isRecoiling = false;
            m_HitTank = null;
            m_HitTerrain = false;

            movementAgent?.Stop();

            Vector2 startPos = transform.position;
            Vector2 chargeDirection = (targetPosition - startPos).normalized;
            if (chargeDirection.sqrMagnitude < 0.001f)
            {
                chargeDirection = transform.up; // Fallback nếu trùng vị trí
            }

            // 1. Giai đoạn chuẩn bị (Windup): Xoay mặt về phía mục tiêu
            RotateTowards(targetPosition);
            if (chargeWindup > 0f)
            {
                yield return new WaitForSeconds(chargeWindup);
            }

            // 2. Giai đoạn Lao thẳng theo quãng đường cố định maxChargeDistance
            Vector2 endPoint = startPos + chargeDirection * maxChargeDistance;
            float maxDuration = (maxChargeDistance / chargeSpeed) * 1.5f;
            float elapsed = 0f;

            bool hitObstacle = false;

            while (elapsed < maxDuration && isCharging)
            {
                elapsed += Time.deltaTime;
                Vector2 currentPos = transform.position;
                Vector2 nextPos = Vector2.MoveTowards(currentPos, endPoint, chargeSpeed * Time.deltaTime);

                if (m_Rigidbody != null)
                {
                    m_Rigidbody.MovePosition(nextPos);
                }
                transform.position = nextPos;

                // Quét va chạm liên tục qua ColliderOwnerRef
                CheckRealtimeColliders();

                // Trường hợp A: Trúng Xe Tăng
                if (m_HitTank != null && m_HitTank.IsAlive)
                {
                    hitObstacle = true;
                    HandleHitTank(m_HitTank, chargeDirection);
                    break;
                }

                // Trường hợp B: Trúng Địa hình
                if (m_HitTerrain)
                {
                    hitObstacle = true;
                    Debug.Log($"[{name}] Xén Nước va chạm địa hình khi lao -> dừng lại và giật lùi về sau.");
                    break;
                }

                // Trường hợp C: Đến điểm dừng mà không trúng gì
                if (Vector2.Distance(nextPos, endPoint) <= 0.15f)
                {
                    break;
                }

                yield return null;
            }

            isCharging = false;

            // 3. Giai đoạn Giật lùi (Recoil) nếu có va chạm với Xe Tăng hoặc Địa hình
            if (hitObstacle)
            {
                yield return StartCoroutine(RecoilRoutine(-chargeDirection));
            }

            // 4. Kết thúc toàn bộ cú lao và kích hoạt hồi chiêu
            currentCooldownTimer = skillCooldown;
            m_HitTank = null;
            m_HitTerrain = false;
            m_ChargeCoroutine = null;
        }

        /// <summary>
        /// Xử lý khi đâm trúng Xe Tăng: gây sát thương và ghi nhận log đẩy lùi.
        /// </summary>
        private void HandleHitTank(TankBrain tank, Vector2 chargeDirection)
        {
            float baseAtk = Stats != null ? Stats.Attack : 10f;
            float damage = baseAtk * damageMultiplier;

            tank.TakeDamage(damage);
            TextDamageManager.Instance?.Show(damage, tank.transform.position);

            // Ghi nhận thông báo đẩy văng mục tiêu theo yêu cầu
            Debug.Log($"[{name}] Xén Nước đâm trúng và đẩy văng Xe Tăng '{tank.name}' một đoạn {knockbackDistance:F1}m theo hướng {chargeDirection}. (Lưu ý: Tạm thời hiển thị thông báo chờ logic đẩy lùi trong TankBrain hoàn thiện).");
        }

        /// <summary>
        /// Giật lùi một đoạn ngắn về phía sau để giữ khoảng cách an toàn.
        /// </summary>
        private IEnumerator RecoilRoutine(Vector2 recoilDirection)
        {
            isRecoiling = true;

            float recoilDuration = recoilDistance / recoilSpeed;
            float elapsed = 0f;
            Vector2 recoilStartPos = transform.position;
            Vector2 targetRecoilPos = recoilStartPos + recoilDirection * recoilDistance;

            while (elapsed < recoilDuration)
            {
                elapsed += Time.deltaTime;
                Vector2 currentPos = transform.position;
                Vector2 nextPos = Vector2.MoveTowards(currentPos, targetRecoilPos, recoilSpeed * Time.deltaTime);

                // Kiểm tra va chạm an toàn phía sau: nếu chạm terrain thì dừng lùi
                if (CheckTerrainBehind())
                {
                    break;
                }

                if (m_Rigidbody != null)
                {
                    m_Rigidbody.MovePosition(nextPos);
                }
                transform.position = nextPos;

                yield return null;
            }

            isRecoiling = false;
        }

        private bool CheckTerrainBehind()
        {
            ColliderOwnerRef colRef = GetActiveColliderRef();
            if (colRef == null) return false;

            foreach (Collider2D col in colRef.GetTriggerColliders())
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                if (terrainLayer.value != 0 && (terrainLayer.value & (1 << col.gameObject.layer)) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void CheckRealtimeColliders()
        {
            ColliderOwnerRef colRef = GetActiveColliderRef();
            if (colRef == null) return;

            foreach (Collider2D col in colRef.GetTriggerColliders())
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                ProcessHitCollider(col);
                if (m_HitTank != null || m_HitTerrain) return;
            }

            foreach (Collider2D col in colRef.GetCollisionColliders())
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                ProcessHitCollider(col);
                if (m_HitTank != null || m_HitTerrain) return;
            }
        }

        private void RotateTowards(Vector2 targetPos)
        {
            Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private TankBrain ResolveTankBrain(Collider2D col)
        {
            if (col == null) return null;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
            if (targetRef != null)
            {
                if (targetRef.TryGetOwner<TankBrain>(out TankBrain directBrain) && directBrain != null)
                {
                    return directBrain;
                }

                if (targetRef.Owner is TankBrain brain)
                {
                    return brain;
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
            }

            return col.GetComponentInParent<TankBrain>();
        }

        #region Debug / Context Menu

        [ContextMenu("Test/Start Charge At Target")]
        public void TestStartCharge()
        {
            Vector2 targetPos = currentTarget != null ? (Vector2)currentTarget.position : (Vector2)transform.position + Vector2.up * 4f;
            StartCharge(targetPos);
        }

        #endregion

        protected override void OnDeath()
        {
            if (m_ChargeCoroutine != null)
            {
                StopCoroutine(m_ChargeCoroutine);
                m_ChargeCoroutine = null;
            }

            isCharging = false;
            isRecoiling = false;
            m_HitTank = null;
            m_HitTerrain = false;

            base.OnDeath();
        }
    }
}
