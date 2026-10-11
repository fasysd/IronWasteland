
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
    public class Skill_Test1 : MonoBehaviour
    {
        public event Action<Skill_Test1, EnemyBrain> TargetFound;

        [Header("Projectile")]
        [SerializeField, Min(0f)] private float speed = 10f;
        [SerializeField, Min(0.1f)] private float lifetime = 5f;

        [Header("Hit Detection")]
        [SerializeField, Min(0f)] private float hitCheckDelay = 0.1f;

        private Rigidbody2D m_Body;
        private ColliderOwnerRef m_OwnerRef;

        private Vector2 m_Direction;
        private float m_ElapsedTime;

        private bool m_Initialized;
        private bool m_HasHit;

        private Coroutine m_HitCheckCoroutine;
        private Action<Skill_Test1> m_ReturnToPool;

        private void Awake()
        {
            m_Body = GetComponent<Rigidbody2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
        }

        private void OnEnable()
        {
            if (m_OwnerRef != null)
                m_OwnerRef.TriggerEnter += OnTriggerEntered;
        }

        private void OnDisable()
        {
            Clear();
        }

        /// <summary>
        /// Xóa trạng thái của lần sử dụng hiện tại.
        /// Giữ lại các component để GameObject có thể được tái sử dụng.
        /// </summary>
        public void Clear()
        {
            if (m_HitCheckCoroutine != null)
            {
                StopCoroutine(m_HitCheckCoroutine);
                m_HitCheckCoroutine = null;
            }

            if (m_OwnerRef != null)
                m_OwnerRef.TriggerEnter -= OnTriggerEntered;

            if (m_Body != null)
                m_Body.linearVelocity = Vector2.zero;

            m_Direction = Vector2.zero;
            m_ElapsedTime = 0f;

            m_Initialized = false;
            m_HasHit = false;

            m_ReturnToPool = null;

            // Xóa toàn bộ subscriber của lần sử dụng trước.
            TargetFound = null;
        }

        public void Initialize(
            Vector2 direction,
            Action<Skill_Test1> returnToPool)
        {
            Clear();

            m_Direction = direction.normalized;
            m_ReturnToPool = returnToPool;
            m_Initialized = m_Direction.sqrMagnitude > 0.0001f;

            if (!m_Initialized)
            {
                FinishProjectile();
                return;
            }

            m_ElapsedTime = 0f;

            transform.right = m_Direction;

            // Gán vận tốc ngay khi khởi tạo.
            if (m_Body != null)
                m_Body.linearVelocity = m_Direction * speed;

            if (m_OwnerRef != null && isActiveAndEnabled)
                m_OwnerRef.TriggerEnter += OnTriggerEntered;
        }

        private void Update()
        {
            if (!m_Initialized || m_HasHit)
                return;

            m_ElapsedTime += Time.deltaTime;

            if (m_ElapsedTime >= lifetime)
                FinishProjectile();
        }

        private void FixedUpdate()
        {
            if (!m_Initialized || m_Body == null || m_HasHit)
                return;

            m_Body.linearVelocity = m_Direction * speed;

        }

        private void OnTriggerEntered(Collider2D other)
        {
            if (!m_Initialized || m_HasHit)
                return;

            m_HitCheckCoroutine = StartCoroutine(CheckTargetsAfterDelay());
        }

        private IEnumerator CheckTargetsAfterDelay()
        {
            yield return new WaitForSeconds(hitCheckDelay);
            m_HitCheckCoroutine = null;

            if (!this || !gameObject.activeInHierarchy || !m_Initialized)
                yield break;

            var enemies = new HashSet<EnemyBrain>();

            foreach (Collider2D collider in m_OwnerRef.GetTriggerColliders())
            {
                if (collider == null)
                    continue;

                ColliderOwnerRef targetRef =
                    ColliderOwnerRef.FromCollider(collider);

                if (targetRef == null)
                    continue;

                EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();

                if (enemy != null)
                    enemies.Add(enemy);
            }

            // Không tìm thấy EnemyBrain: tiếp tục bay.
            if (enemies.Count == 0)
            {
                yield break;
            }

            m_HasHit = true;

            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null)
                    TargetFound?.Invoke(this, enemy);
            }

            FinishProjectile();
        }

        private void FinishProjectile()
        {
            // Lưu callback trước khi Clear() xóa mọi trạng thái.
            Action<Skill_Test1> returnToPool = m_ReturnToPool;

            Clear();

            if (returnToPool != null)
                returnToPool(this);
            else if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
            lifetime = Mathf.Max(0.1f, lifetime);
            hitCheckDelay = Mathf.Max(0f, hitCheckDelay);
        }
    }
}