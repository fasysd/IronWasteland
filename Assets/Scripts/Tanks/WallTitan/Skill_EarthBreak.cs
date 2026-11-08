using IronWasteland.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.WallTitan
{
    /// <summary>
    /// Kỹ năng thường của Wall Titan — Earth Break:
    /// Phóng một khối đá bằng ma thuật đất thẳng về phía mục tiêu, gây sát thương khi va chạm.
    /// Sát thương giảm dần khi khoảng cách giữa Wall Titan và mục tiêu tăng lên.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class Skill_EarthBreak : MonoBehaviour
    {
        public event Action<Skill_EarthBreak, EnemyBrain, float> TargetFound;

        [Header("Projectile")]
        [SerializeField, Min(0f)] private float speed = 9f;
        [SerializeField, Min(0.1f)] private float lifetime = 4f;

        [Header("Hit Detection")]
        [SerializeField, Min(0f)] private float hitCheckDelay = 0.05f;

        private Rigidbody2D m_Body;
        private ColliderOwnerRef m_OwnerRef;

        private Vector2 m_SpawnPosition;
        private Vector2 m_Direction;
        private float m_ElapsedTime;

        private bool m_Initialized;
        private bool m_HasHit;

        private Coroutine m_HitCheckCoroutine;
        private Action<Skill_EarthBreak> m_ReturnToPool;

        public ColliderOwnerRef ColliderRef => m_OwnerRef;

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

            m_SpawnPosition = Vector2.zero;
            m_Direction = Vector2.zero;
            m_ElapsedTime = 0f;

            m_Initialized = false;
            m_HasHit = false;

            m_ReturnToPool = null;
            TargetFound = null;
        }

        public void Initialize(
            Vector2 spawnPosition,
            Vector2 direction,
            Action<Skill_EarthBreak> returnToPool)
        {
            Clear();

            m_SpawnPosition = spawnPosition;
            m_Direction = direction.normalized;
            m_ReturnToPool = returnToPool;
            m_Initialized = m_Direction.sqrMagnitude > 0.0001f;

            if (!m_Initialized)
            {
                FinishProjectile();
                return;
            }

            m_ElapsedTime = 0f;
            transform.position = spawnPosition;
            transform.right = m_Direction;

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

            // Chạm đúng collider kẻ địch: xử lý ngay thay vì luôn đợi 0.05s.
            EnemyBrain direct = ResolveEnemy(other);
            if (direct != null)
            {
                Hit(direct);
                return;
            }

            // Collider vừa chạm không phải kẻ địch: quét bù để bắt mục tiêu đã
            // nằm sẵn trong vùng trigger ngay khi đạn xuất hiện.
            if (m_HitCheckCoroutine == null)
                m_HitCheckCoroutine = StartCoroutine(CheckTargetsAfterDelay());
        }

        private IEnumerator CheckTargetsAfterDelay()
        {
            if (hitCheckDelay > 0f)
                yield return new WaitForSeconds(hitCheckDelay);

            m_HitCheckCoroutine = null;

            if (!this || !gameObject.activeInHierarchy || !m_Initialized || m_HasHit)
                yield break;

            // Earth Break là đòn đơn mục tiêu: chọn enemy gần nhất thay vì đánh
            // tất cả enemy đang overlap (trước đây một phát có thể trúng nhiều con).
            EnemyBrain nearest = FindNearestEnemy();
            if (nearest != null)
                Hit(nearest);
        }

        private void Hit(EnemyBrain enemy)
        {
            if (enemy == null || m_HasHit)
                return;

            m_HasHit = true;

            float travelDistance = Vector2.Distance(m_SpawnPosition, transform.position);
            TargetFound?.Invoke(this, enemy, travelDistance);

            FinishProjectile();
        }

        private EnemyBrain FindNearestEnemy()
        {
            if (m_OwnerRef == null)
                return null;

            EnemyBrain nearest = null;
            float bestSqrDistance = float.MaxValue;
            Vector2 selfPosition = transform.position;

            foreach (Collider2D collider in m_OwnerRef.GetTriggerColliders())
            {
                EnemyBrain enemy = ResolveEnemy(collider);
                if (enemy == null)
                    continue;

                float sqrDistance = ((Vector2)enemy.transform.position - selfPosition).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        private static EnemyBrain ResolveEnemy(Collider2D collider)
        {
            if (collider == null)
                return null;

            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collider);
            return targetRef != null ? targetRef.GetOwner<EnemyBrain>() : null;
        }

        private void FinishProjectile()
        {
            Action<Skill_EarthBreak> returnToPool = m_ReturnToPool;

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
