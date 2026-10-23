using IronWasteland;
using IronWasteland.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.Leviathan
{
    /// <summary>
    /// Kỹ năng thường của Leviathan: Water Pulse (Sóng Xung Kích Nước).
    /// Phóng ra sóng xung kích bằng nước ma thuật màu xanh lam lan rộng ra phía trước.
    /// Không có khả năng xuyên vật thể: khi chạm mục tiêu hoặc vật cản,
    /// đạn chờ 0.1s coroutine để quét toàn bộ collider trong vùng va chạm,
    /// gây sát thương lên tất cả kẻ địch trúng đòn rồi biến mất về Pool.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class NormalSkill_Leviathan : MonoBehaviour
    {
        public event Action<NormalSkill_Leviathan, EnemyBrain> TargetFound;

        [Header("Chỉ Số Bay (Movement)")]
        [Tooltip("Tốc độ bay của sóng xung kích nước (đơn vị/giây).")]
        [SerializeField, Min(0f)] private float speed = 12f;

        [Tooltip("Thời gian tồn tại tối đa của sóng nước nếu không chạm mục tiêu (giây).")]
        [SerializeField, Min(0.1f)] private float lifeTime = 5f;

        [Header("Cơ Chế Va Chạm (Hit Check)")]
        [Tooltip("Khoảng thời gian chờ coroutine để thu thập toàn bộ collider trong vùng sóng nước trước khi kết thúc đạn (giây).")]
        [SerializeField, Min(0.01f)] private float hitCheckDelay = 0.1f;

        private Rigidbody2D body;
        private ColliderOwnerRef colliderOwnerRef;
        private SpriteRenderer spriteRenderer;

        private Vector2 direction;
        private float elapseTime;
        private bool initialized;
        private bool isImpacting = false;
        private Coroutine hitCheckCoroutine;
        private Action<NormalSkill_Leviathan> returnToPool;

        private readonly HashSet<EnemyBrain> damagedEnemies = new HashSet<EnemyBrain>();

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            colliderOwnerRef = GetComponent<ColliderOwnerRef>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            if (colliderOwnerRef != null)
            {
                colliderOwnerRef.TriggerEnter += ColliderOwnerRef_TriggerEnter;
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        /// <summary>
        /// Khởi tạo hướng bay và đưa viên đạn vào trạng thái hoạt động.
        /// </summary>
        public void Initialize(Vector2 direction, Action<NormalSkill_Leviathan> returnToPool)
        {
            Clear();

            this.direction = direction.normalized;
            this.returnToPool = returnToPool;
            this.initialized = direction.sqrMagnitude > 0.001f;

            if (!initialized)
            {
                FinishProjectile();
                return;
            }

            elapseTime = 0f;
            isImpacting = false;
            transform.right = this.direction;

            if (body != null)
            {
                body.linearVelocity = this.direction * speed;
            }

            if (colliderOwnerRef != null)
            {
                colliderOwnerRef.TriggerEnter -= ColliderOwnerRef_TriggerEnter;
                colliderOwnerRef.TriggerEnter += ColliderOwnerRef_TriggerEnter;
            }
        }

        private void ColliderOwnerRef_TriggerEnter(Collider2D other)
        {
            if (!initialized || isImpacting || other == null)
                return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(other);
            if (targetRef == null) return;

            // Bỏ qua nếu va chạm với bất kỳ xe tank nào
            if (targetRef.GetOwner<TankBrain>() != null) return;

            // Khi chạm mục tiêu: bắt đầu đếm 0.1s coroutine để quét toàn bộ collider
            isImpacting = true;
            hitCheckCoroutine = StartCoroutine(CheckTargetsAfterDelay());
        }

        private IEnumerator CheckTargetsAfterDelay()
        {
            yield return new WaitForSeconds(hitCheckDelay);
            hitCheckCoroutine = null;

            if (!this || !gameObject.activeInHierarchy || !initialized)
                yield break;

            // Quét tất cả collider nằm trong Trigger
            if (colliderOwnerRef != null)
            {
                var enemies = new HashSet<EnemyBrain>();
                foreach (Collider2D collider in colliderOwnerRef.GetTriggerColliders())
                {
                    if (collider == null) continue;

                    ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collider);
                    if (targetRef != null && targetRef.TryGetOwner<EnemyBrain>(out var enemy) && enemy.IsAlive)
                    {
                        enemies.Add(enemy);
                    }
                }

                // Gây sát thương lên tất cả kẻ địch trúng đòn
                foreach (EnemyBrain enemy in enemies)
                {
                    if (enemy != null && damagedEnemies.Add(enemy))
                    {
                        TargetFound?.Invoke(this, enemy);
                    }
                }
            }

            // Đạn không có khả năng xuyên vật thể: kết thúc ngay sau khi hết 0.1s va chạm
            FinishProjectile();
        }

        public void Clear()
        {
            if (hitCheckCoroutine != null)
            {
                StopCoroutine(hitCheckCoroutine);
                hitCheckCoroutine = null;
            }

            if (colliderOwnerRef != null)
            {
                colliderOwnerRef.TriggerEnter -= ColliderOwnerRef_TriggerEnter;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            direction = Vector2.zero;
            elapseTime = 0f;
            initialized = false;
            isImpacting = false;
            returnToPool = null;
            TargetFound = null;
            damagedEnemies.Clear();
        }

        private void FinishProjectile()
        {
            Action<NormalSkill_Leviathan> callback = this.returnToPool;

            Clear();

            if (callback != null)
                callback(this);
            else if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!initialized) return;

            elapseTime += Time.deltaTime;
            if (elapseTime >= lifeTime)
            {
                FinishProjectile();
            }
        }

        private void FixedUpdate()
        {
            if (!initialized || body == null) return;

            if (isImpacting)
            {
                // Giảm tốc nhẹ trong 0.1s va chạm trước khi tan biến
                body.linearVelocity = direction * (speed * 0.3f);
            }
            else
            {
                body.linearVelocity = direction * speed;
            }
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
            lifeTime = Mathf.Max(0.1f, lifeTime);
            hitCheckDelay = Mathf.Max(0.01f, hitCheckDelay);
        }
    }
}
