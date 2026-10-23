using IronWasteland;
using IronWasteland.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.Leviathan
{
    /// <summary>
    /// Kỹ năng chuyên biệt của Leviathan: Water Bubble (Bong Bóng Nước).
    /// Bắn ra một bong bóng nước khổng lồ nhốt cố định kẻ địch trong thời gian ngắn (nâng kẻ địch lên khỏi mặt đất).
    /// Khi hết thời gian hoặc vỡ tung, bong bóng phát nổ gây sát thương diện rộng và đẩy lùi toàn bộ kẻ địch xung quanh.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class SpecializedSkill_Leviathan : MonoBehaviour
    {
        public event Action<SpecializedSkill_Leviathan, EnemyBrain> OnEnemyTrapped;
        public event Action<SpecializedSkill_Leviathan, Vector2> OnExploded;

        public enum BubbleState
        {
            Idle,
            Flying,
            Trapping,
            Exploding
        }

        [Header("Hình Ảnh & Hiệu Ứng (Visuals)")]
        [Tooltip("Sprite hiển thị bong bóng nước trong suốt màu xanh lam.")]
        [SerializeField] private Sprite bubbleSprite;

        [Tooltip("Sprite hiển thị vụ nổ sóng nước lan tỏa khi bong bóng vỡ.")]
        [SerializeField] private Sprite burstSprite;

        [Tooltip("Thời gian hiển thị hiệu ứng nổ nước trước khi thu hồi về Pool (giây).")]
        [SerializeField, Min(0.05f)] private float burstDuration = 0.35f;

        [Header("Chỉ Số Bay (Flight)")]
        [Tooltip("Tốc độ bay của bong bóng nước (đơn vị/giây).")]
        [SerializeField, Min(0f)] private float speed = 8f;

        [Tooltip("Thời gian bay tối đa trước khi tự phát nổ nếu không trúng mục tiêu (giây).")]
        [SerializeField, Min(0.1f)] private float maxFlightTime = 2.5f;

        [Header("Cơ Chế Nhốt & Nâng Lên (Trapping)")]
        [Tooltip("Thời gian nhốt giữ cố định kẻ địch trong bong bóng (giây).")]
        [SerializeField, Min(0.1f)] private float trapDuration = 2.0f;

        [Tooltip("Độ cao nâng kẻ địch lên khỏi mặt đất khi lơ lửng trong bong bóng.")]
        [SerializeField, Min(0f)] private float liftHeight = 0.6f;

        [Header("Sát Thương Diện Rộng & Đẩy Lùi (Explosion)")]
        [Tooltip("Bán kính nổ diện rộng khi bong bóng vỡ tung.")]
        [SerializeField, Min(0.5f)] private float explosionRadius = 3.5f;

        [Tooltip("Khoảng cách đẩy lùi kẻ địch xung quanh khi bong bóng phát nổ.")]
        [SerializeField, Min(0f)] private float knockbackDistance = 2.5f;

        [Tooltip("Thời gian thực hiện cú đẩy lùi (giây).")]
        [SerializeField, Min(0.05f)] private float knockbackDuration = 0.2f;

        private Rigidbody2D body;
        private ColliderOwnerRef colliderOwnerRef;
        private SpriteRenderer spriteRenderer;

        private BubbleState currentState = BubbleState.Idle;
        private TankBrain ownerTank;
        private Vector2 direction;
        private float timer;
        private float damage = 70f;
        private EnemyBrain trappedEnemy;
        private Vector2 trapAnchorPosition;
        private Coroutine explodeCoroutine;
        private Action<SpecializedSkill_Leviathan> returnToPool;

        private Vector3 initialScale = new Vector3(0.35f, 0.35f, 1f);
        private Color initialColor = Color.white;

        public BubbleState CurrentState => currentState;
        public EnemyBrain TrappedEnemy => trappedEnemy;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            colliderOwnerRef = GetComponent<ColliderOwnerRef>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            initialScale = transform.localScale;
            if (spriteRenderer != null)
            {
                initialColor = spriteRenderer.color;
            }
        }

        private void OnEnable()
        {
            if (colliderOwnerRef != null)
            {
                colliderOwnerRef.TriggerEnter -= OnColliderTriggerEnter;
                colliderOwnerRef.TriggerEnter += OnColliderTriggerEnter;
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        /// <summary>
        /// Khởi tạo và bắn bong bóng nước với tham chiếu xe tank sở hữu.
        /// </summary>
        public void Initialize(TankBrain owner, Vector2 direction, float damageAmount, Action<SpecializedSkill_Leviathan> returnToPool)
        {
            Clear();

            this.ownerTank = owner;
            this.direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            this.damage = damageAmount;
            this.returnToPool = returnToPool;
            this.currentState = BubbleState.Flying;
            this.timer = 0f;

            transform.right = this.direction;
            transform.localScale = initialScale;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = initialColor;
                if (bubbleSprite != null)
                {
                    spriteRenderer.sprite = bubbleSprite;
                }
            }

            if (body != null)
            {
                body.linearVelocity = this.direction * speed;
            }

            if (colliderOwnerRef != null)
            {
                colliderOwnerRef.TriggerEnter -= OnColliderTriggerEnter;
                colliderOwnerRef.TriggerEnter += OnColliderTriggerEnter;
            }
        }

        /// <summary>
        /// Khởi tạo không truyền owner tank (hỗ trợ gọi tương thích ngược).
        /// </summary>
        public void Initialize(Vector2 direction, float damageAmount, Action<SpecializedSkill_Leviathan> returnToPool)
        {
            Initialize(null, direction, damageAmount, returnToPool);
        }

        private void FixedUpdate()
        {
            if (currentState != BubbleState.Flying || body == null)
                return;

            body.linearVelocity = direction * speed;
        }

        private void OnColliderTriggerEnter(Collider2D other)
        {
            if (currentState != BubbleState.Flying || other == null)
                return;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(other);
            if (targetRef != null && targetRef.TryGetOwner<EnemyBrain>(out var enemy) && enemy.IsAlive)
            {
                TrapEnemy(enemy);
            }
        }

        private void TrapEnemy(EnemyBrain enemy)
        {
            currentState = BubbleState.Trapping;
            trappedEnemy = enemy;
            trapAnchorPosition = enemy.transform.position;
            transform.position = trapAnchorPosition;
            timer = trapDuration;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            // Vô hiệu hóa vận tốc di chuyển hiện tại của kẻ địch
            Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
            if (enemyRb != null)
            {
                enemyRb.linearVelocity = Vector2.zero;
            }

            OnEnemyTrapped?.Invoke(this, enemy);
        }

        private void Update()
        {
            if (currentState == BubbleState.Flying)
            {
                timer += Time.deltaTime;
                if (timer >= maxFlightTime)
                {
                    Explode();
                }
            }
            else if (currentState == BubbleState.Trapping)
            {
                // Nếu kẻ địch tử trận trong lúc đang bị nhốt -> Phát nổ sớm
                if (trappedEnemy == null || !trappedEnemy.IsAlive)
                {
                    Explode();
                    return;
                }

                timer -= Time.deltaTime;

                // Hiệu ứng nâng bổng và nhấp nhô lơ lửng trong bong bóng nước
                float progress = 1f - Mathf.Clamp01(timer / trapDuration);
                float lift = Mathf.Sin(progress * Mathf.PI) * liftHeight;

                Vector3 floatPos = new Vector3(trapAnchorPosition.x, trapAnchorPosition.y + lift, 0f);
                transform.position = floatPos;
                trappedEnemy.transform.position = floatPos;

                Rigidbody2D enemyRb = trappedEnemy.GetComponent<Rigidbody2D>();
                if (enemyRb != null)
                {
                    enemyRb.position = floatPos;
                    enemyRb.linearVelocity = Vector2.zero;
                }

                if (timer <= 0f)
                {
                    Explode();
                }
            }
        }

        /// <summary>
        /// Kích nổ bong bóng nước: gây sát thương diện rộng và đẩy lùi mục tiêu xung quanh.
        /// </summary>
        public void Explode()
        {
            if (currentState == BubbleState.Exploding)
                return;

            currentState = BubbleState.Exploding;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            // Đổi hình ảnh sang vụ nổ nước
            if (spriteRenderer != null && burstSprite != null)
            {
                spriteRenderer.sprite = burstSprite;
            }

            Vector2 explosionCenter = transform.position;

            // Quét tìm tất cả kẻ địch trong bán kính nổ
            Collider2D[] hits = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius);
            var affectedEnemies = new HashSet<EnemyBrain>();

            for (int i = 0; i < hits.Length; i++)
            {
                ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(hits[i]);
                if (targetRef != null && targetRef.TryGetOwner<EnemyBrain>(out var enemy) && enemy.IsAlive)
                {
                    affectedEnemies.Add(enemy);
                }
            }

            // Gây sát thương và đẩy lùi kẻ địch
            foreach (EnemyBrain enemy in affectedEnemies)
            {
                enemy.TakeDamage(damage);
                TextDamageManager.Instance?.Show(damage, enemy.transform.position);

                Vector2 enemyPos = enemy.transform.position;
                Vector2 knockDir = enemyPos - explosionCenter;
                if (knockDir.sqrMagnitude < 0.001f)
                {
                    knockDir = Vector2.up;
                }
                knockDir.Normalize();

                StartCoroutine(ApplyKnockback(enemy, knockDir));
            }

            trappedEnemy = null;
            OnExploded?.Invoke(this, explosionCenter);

            explodeCoroutine = StartCoroutine(FinishAfterBurstDelay());
        }

        private IEnumerator ApplyKnockback(EnemyBrain enemy, Vector2 knockDir)
        {
            if (enemy == null) yield break;

            Vector2 startPos = enemy.transform.position;
            Vector2 targetPos = startPos + knockDir * knockbackDistance;
            Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();

            float elapsed = 0f;
            while (elapsed < knockbackDuration)
            {
                if (enemy == null || !enemy.IsAlive) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / knockbackDuration);
                // Ease out cubic
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);

                Vector2 currentPos = Vector2.Lerp(startPos, targetPos, easeOut);
                enemy.transform.position = currentPos;

                if (enemyRb != null)
                {
                    enemyRb.position = currentPos;
                    enemyRb.linearVelocity = Vector2.zero;
                }

                yield return null;
            }
        }

        private IEnumerator FinishAfterBurstDelay()
        {
            Vector3 originalScale = transform.localScale;
            Vector3 targetScale = originalScale * 1.35f;
            Color originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

            float elapsed = 0f;
            while (elapsed < burstDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / burstDuration);

                // Nở to sóng nước và mờ dần
                transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
                if (spriteRenderer != null)
                {
                    Color c = originalColor;
                    c.a = Mathf.Lerp(originalColor.a, 0f, t);
                    spriteRenderer.color = c;
                }

                yield return null;
            }

            transform.localScale = originalScale;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }

            explodeCoroutine = null;
            FinishBubble();
        }

        public void Clear()
        {
            if (explodeCoroutine != null)
            {
                StopCoroutine(explodeCoroutine);
                explodeCoroutine = null;
            }

            StopAllCoroutines();

            if (colliderOwnerRef != null)
            {
                colliderOwnerRef.TriggerEnter -= OnColliderTriggerEnter;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            transform.localScale = initialScale;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = initialColor;
            }

            currentState = BubbleState.Idle;
            trappedEnemy = null;
            timer = 0f;
            returnToPool = null;
            OnEnemyTrapped = null;
            OnExploded = null;
        }

        private void FinishBubble()
        {
            Action<SpecializedSkill_Leviathan> callback = this.returnToPool;

            Clear();

            if (callback != null)
            {
                callback(this);
            }
            else if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
            trapDuration = Mathf.Max(0.1f, trapDuration);
            explosionRadius = Mathf.Max(0.5f, explosionRadius);
            knockbackDistance = Mathf.Max(0f, knockbackDistance);
            burstDuration = Mathf.Max(0.05f, burstDuration);
        }
    }
}
