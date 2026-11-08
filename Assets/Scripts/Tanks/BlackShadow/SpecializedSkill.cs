using IronWasteland;
using IronWasteland.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class SpecializedSkill : MonoBehaviour
    {
        public event Action<SpecializedSkill, EnemyBrain> TargetFound;

        [Header("Visuals (Assets)")]
        [Tooltip("Sprite cho Giai đoạn 1: Đặt ảo ảnh.")]
        [SerializeField] private Sprite illusionSprite;

        [Tooltip("Sprite cho Giai đoạn 2: Vụ nổ.")]
        [SerializeField] private Sprite explosionSprite;

        [Tooltip("Thời gian hiển thị hình ảnh vụ nổ trước khi thu hồi về Pool (giây).")]
        [SerializeField, Min(0.05f)] private float explosionDuration = 0.25f;

        [Header("Design Settings")]
        [Tooltip("Thời gian tồn tại tối đa của ảo ảnh nếu không bấm đổi chỗ (giây).")]
        [SerializeField, Min(0.1f)] private float illusionDuration = 5f;

        private SpriteRenderer spriteRenderer;
        private ColliderOwnerRef colliderOwnerRef;
        private Rigidbody2D body;

        private Vector2 copyPosition = Vector2.zero;
        private int skillState = 0; // 0: Đang đặt ảo ảnh | 1: Đã kích hoạt đổi chỗ / nổ
        private float elapseTime = 0f;
        private bool initialized = false;
        private Coroutine explosionCoroutine;
        private Action<SpecializedSkill> returnToPool;

        public Vector2 CopyPosition => copyPosition;
        public int SkillState => skillState;

        private void Awake()
        {
            colliderOwnerRef = GetComponent<ColliderOwnerRef>();
            body = GetComponent<Rigidbody2D>();

            // Tự động tìm SpriteRenderer trên GameObject này hoặc con của nó
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void Update()
        {
            if (!initialized || skillState != 0)
                return;

            // Nếu ở State 0 quá thời gian mà người chơi không đổi chỗ -> tự huỷ về Pool
            elapseTime += Time.deltaTime;
            if (elapseTime >= illusionDuration)
            {
                FinishSkill();
            }
        }

        /// <summary>
        /// Giai đoạn 1: Đặt ảo ảnh tại vị trí chỉ định và gán Sprite ảo ảnh.
        /// </summary>
        public void Initialize(Vector2 spawnPosition, Action<SpecializedSkill> returnToPool)
        {
            Clear();

            this.copyPosition = spawnPosition;
            transform.position = spawnPosition;
            this.returnToPool = returnToPool;
            this.skillState = 0;
            this.elapseTime = 0f;
            this.initialized = true;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            // Đổi hình ảnh hiển thị sang ảo ảnh (Giai đoạn 1)
            if (spriteRenderer != null && illusionSprite != null)
            {
                spriteRenderer.sprite = illusionSprite;
            }
        }

        /// <summary>
        /// Giai đoạn 2: Đổi sprite sang vụ nổ, gây sát thương qua GetTriggerColliders() và chờ hiển thị nổ.
        /// </summary>
        public void Explode()
        {
            if (!initialized || skillState != 0)
                return;

            skillState = 1;

            // 1. Đổi hình ảnh sang vụ nổ (Giai đoạn 2)
            if (spriteRenderer != null && explosionSprite != null)
            {
                spriteRenderer.sprite = explosionSprite;
            }

            // 2. Gây sát thương ngay lập tức cho toàn bộ mục tiêu trong Trigger
            if (colliderOwnerRef != null)
            {
                var enemies = new HashSet<EnemyBrain>();

                foreach (Collider2D collider in colliderOwnerRef.GetTriggerColliders())
                {
                    if (collider == null) continue;

                    ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collider);
                    if (targetRef == null) continue;

                    EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
                    if (enemy != null)
                    {
                        enemies.Add(enemy);
                    }
                }

                foreach (EnemyBrain enemy in enemies)
                {
                    TargetFound?.Invoke(this, enemy);
                }
            }

            // 3. Đợi một khoảng thời gian ngắn để người chơi thấy hình ảnh vụ nổ rồi mới thu hồi về Pool
            explosionCoroutine = StartCoroutine(WaitAndFinishExplosion());
        }

        private IEnumerator WaitAndFinishExplosion()
        {
            yield return new WaitForSeconds(explosionDuration);
            explosionCoroutine = null;
            FinishSkill();
        }

        /// <summary>
        /// Xóa sạch toàn bộ trạng thái để tái sử dụng trong Object Pool.
        /// </summary>
        public void Clear()
        {
            if (explosionCoroutine != null)
            {
                StopCoroutine(explosionCoroutine);
                explosionCoroutine = null;
            }

            StopAllCoroutines();

            skillState = 0;
            copyPosition = Vector2.zero;
            elapseTime = 0f;
            initialized = false;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            TargetFound = null;
            returnToPool = null;
        }

        private void OnDisable()
        {
            Clear();
        }

        private void FinishSkill()
        {
            Action<SpecializedSkill> callback = this.returnToPool;

            Clear();

            if (callback != null)
                callback(this);
            else if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
