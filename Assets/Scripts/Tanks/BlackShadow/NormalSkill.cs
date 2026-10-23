using IronWasteland;
using IronWasteland.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class NormalSkill : MonoBehaviour
    {
        public event Action<NormalSkill, EnemyBrain> TargetFound;
        [SerializeField, Min(0f)] private float speed = 10f;

        [SerializeField, Min(0f)] private float hitCheckDelay = 0.1f;

        [SerializeField, Min(0f)] private float lifeTime = 10f;

        [SerializeField] private Sprite defaultSprite;
        [SerializeField] private Sprite enhancedSprite;


        [SerializeField] private float enhancedLifeTime = 15f;
        private Rigidbody2D body;

        private ColliderOwnerRef colliderOwnerRef;

        private Vector2 direction;

        private float elapseTime;

        private bool initialized;

        private SpriteRenderer spriteRenderer;

        private Coroutine hitCheckCoroutine;
        private Action<NormalSkill> returnToPool;

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
        /// <summary>
        /// Đăng ký nhận sự kiện va chạm khi đạn được bật lên.
        /// Luôn gỡ trước rồi gắn lại để tránh đăng ký trùng khi đạn được
        /// lấy ra khỏi pool nhiều lần.
        /// </summary>
        private void OnEnable()
        {
            if (colliderOwnerRef == null)
                return;

            colliderOwnerRef.TriggerEnter -= ColliderOwnerRef_TriggerEnter;
            colliderOwnerRef.TriggerEnter += ColliderOwnerRef_TriggerEnter;
        }
        private IEnumerator CheckTargetsAfterDelay()
        {
            yield return new WaitForSeconds(hitCheckDelay);
            hitCheckCoroutine = null;
            if (!this || !gameObject.activeInHierarchy || !initialized)
                yield break;
            var enemies = new HashSet<EnemyBrain>();
            foreach (Collider2D collider in colliderOwnerRef.GetTriggerColliders())
            {
                if (collider == null) continue;
                ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collider);
                if (targetRef == null) continue;
                EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
                if (enemy != null) enemies.Add(enemy);
            }
            if (enemies.Count == 0)
            {
                yield break;
            }

            foreach (EnemyBrain enemy in enemies)
            {
                // Chỉ gây sát thương nếu kẻ địch chưa từng trúng đạn của lần bắn này
                if (enemy != null && damagedEnemies.Add(enemy))
                {
                    TargetFound?.Invoke(this, enemy);
                }
            }
        }
        private void ColliderOwnerRef_TriggerEnter(Collider2D obj)
        {
            if (!initialized || obj == null)
                return;

            // Lọc bỏ xe tank của mình hoặc các vật thể không phải Enemy
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(obj);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy == null) return;

            // Bỏ qua nếu kẻ địch này đã bị viên đạn này gây sát thương rồi
            if (damagedEnemies.Contains(enemy))
                return;

            hitCheckCoroutine = StartCoroutine(CheckTargetsAfterDelay());
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
            returnToPool = null;
            TargetFound = null;
            damagedEnemies.Clear();
        }
        private void OnDisable()
        {
            Clear();
        }
        private void FinishProjectile()
        {
            Action<NormalSkill> returnToPool = this.returnToPool;

            Clear();

            if (returnToPool != null)
                returnToPool(this);
            else if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
        public void Initialize(Vector2 direction, Action<NormalSkill> returnToPool, bool isEnhanced = false)
        {
            Clear();
            spriteRenderer.sprite = isEnhanced && enhancedSprite != null ? enhancedSprite : defaultSprite;
            this.lifeTime = isEnhanced ? enhancedLifeTime : lifeTime;
            this.direction = direction.normalized;
            this.returnToPool = returnToPool;
            this.initialized = direction.sqrMagnitude > 0.001f;
            if (!initialized)
            {
                FinishProjectile();
                return;
            }
            elapseTime = 0f;
            transform.right = direction;
            if(body!= null)
            {
                body.linearVelocity = direction * speed;
            }

            // Đảm bảo đã đăng ký va chạm kể cả khi GameObject vốn đang active
            // (trường hợp đó OnEnable không chạy lại). Gỡ trước để không đăng ký trùng.
            if(colliderOwnerRef != null && isActiveAndEnabled)
            {
                colliderOwnerRef.TriggerEnter -= ColliderOwnerRef_TriggerEnter;
                colliderOwnerRef.TriggerEnter += ColliderOwnerRef_TriggerEnter;
            }


        }
        private void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
            lifeTime = Mathf.Max(0.1f, lifeTime);
            hitCheckDelay = Mathf.Max(0f, hitCheckDelay);
        }

        void Start()
        {


        }

        // Update is called once per frame
        void Update()
        {
            if(!initialized)
            {
                return;
            }
            elapseTime += Time.deltaTime;
            if(elapseTime >= lifeTime)
            {
                FinishProjectile();
            }
        }
        private void FixedUpdate()
        {
            if (!initialized || body == null)
                return;

            body.linearVelocity = direction * speed;

        }
    }
}
