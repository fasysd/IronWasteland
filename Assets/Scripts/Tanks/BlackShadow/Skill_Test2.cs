
using IronWasteland.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class Skill_Test2 : MonoBehaviour
    {
        public event Action<Skill_Test2, EnemyBrain> TargetFound;

        [Header("Lifetime")]
        [SerializeField, Min(0.1f)]
        private float duration = 3f;

        [SerializeField, Min(0.01f)]
        private float interval = 0.5f;

        private CircleCollider2D m_AreaCollider;
        private ColliderOwnerRef m_OwnerRef;

        private float m_ElapsedTime;
        private bool m_Initialized;
        private Coroutine m_LoopCoroutine;

        private Action<Skill_Test2> m_ReturnToPool;

        private void Awake()
        {
            m_AreaCollider = GetComponent<CircleCollider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();

            m_AreaCollider.isTrigger = true;
        }

        /// <summary>
        /// Xóa trạng thái sử dụng hiện tại, bao gồm toàn bộ subscriber.
        /// Giữ lại component để tái sử dụng qua pooling.
        /// </summary>
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

        /// <summary>
        /// position là vị trí Look trên thế giới.
        /// </summary>
        public void Initialize(
            Vector2 position,
            Action<Skill_Test2> returnToPool)
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
                // Kiểm tra mục tiêu trong vùng ở mỗi nhịp.
                FindTargets();

                float waitTime = Mathf.Min(
                    interval,
                    duration - elapsed);

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

                ColliderOwnerRef targetRef =
                    ColliderOwnerRef.FromCollider(collider);

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
            Action<Skill_Test2> returnToPool = m_ReturnToPool;

            Clear();

            if (returnToPool != null)
                returnToPool(this);
            else
                gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0.1f, duration);
            interval = Mathf.Max(0.01f, interval);
        }
    }
}