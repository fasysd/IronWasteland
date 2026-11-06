using IronWasteland.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.WallTitan
{
    /// <summary>
    /// Kỹ năng tùy chọn 2 (Nhánh 2) của Wall Titan — Earthquake:
    /// Gây chấn động trên một vùng lớn, làm chậm kẻ địch và định kỳ gây sát thương trong thời gian hiệu lực.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class Skill_Earthquake : MonoBehaviour
    {
        public event Action<Skill_Earthquake, EnemyBrain> TargetFound;

        [Header("Lifetime")]
        [SerializeField, Min(0.1f)] private float duration = 4f;
        [SerializeField, Min(0.05f)] private float interval = 0.5f;

        private CircleCollider2D m_AreaCollider;
        private ColliderOwnerRef m_OwnerRef;

        private float m_ElapsedTime;
        private bool m_Initialized;
        private Coroutine m_LoopCoroutine;
        private Action<Skill_Earthquake> m_ReturnToPool;

        public ColliderOwnerRef ColliderRef => m_OwnerRef;

        private void Awake()
        {
            m_AreaCollider = GetComponent<CircleCollider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();

            if (m_AreaCollider != null)
                m_AreaCollider.isTrigger = true;
        }

        public void Clear()
        {
            if (m_LoopCoroutine != null)
            {
                StopCoroutine(m_LoopCoroutine);
                m_LoopCoroutine = null;
            }

            m_ElapsedTime = 0f;
            m_Initialized = false;
            m_ReturnToPool = null;
            TargetFound = null;
        }

        public void Initialize(
            Vector2 position,
            Action<Skill_Earthquake> returnToPool)
        {
            Clear();

            transform.position = position;
            m_ReturnToPool = returnToPool;
            m_ElapsedTime = 0f;
            m_Initialized = true;
        }

        public void StartSkill()
        {
            if (!m_Initialized || !isActiveAndEnabled)
                return;

            if (m_LoopCoroutine != null)
                StopCoroutine(m_LoopCoroutine);

            m_LoopCoroutine = StartCoroutine(AreaLoop());
        }

        private IEnumerator AreaLoop()
        {
            float elapsed = 0f;

            while (elapsed < duration && m_Initialized)
            {
                FindTargets();

                float waitTime = Mathf.Min(interval, duration - elapsed);
                if (waitTime <= 0f)
                    break;

                yield return new WaitForSeconds(waitTime);
                elapsed += waitTime;
                m_ElapsedTime = elapsed;
            }

            m_LoopCoroutine = null;

            if (m_Initialized)
                FinishSkill();
        }

        private void FindTargets()
        {
            if (m_OwnerRef == null)
                return;

            var enemies = new HashSet<EnemyBrain>();

            foreach (Collider2D collider in m_OwnerRef.GetTriggerColliders())
            {
                if (collider == null)
                    continue;

                ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collider);
                if (targetRef == null)
                    continue;

                EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
                if (enemy != null)
                    enemies.Add(enemy);
            }

            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null)
                    TargetFound?.Invoke(this, enemy);
            }
        }

        private void FinishSkill()
        {
            Action<Skill_Earthquake> returnToPool = m_ReturnToPool;

            Clear();

            if (returnToPool != null)
                returnToPool(this);
            else if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0.1f, duration);
            interval = Mathf.Max(0.05f, interval);
        }
    }
}
