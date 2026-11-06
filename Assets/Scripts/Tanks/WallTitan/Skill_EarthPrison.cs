using IronWasteland.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.WallTitan
{
    /// <summary>
    /// Kỹ năng tùy chọn 1 (Nhánh 2) của Wall Titan — Earth Prison:
    /// Dựng các vách đất bao quanh một khu vực nhỏ, nhốt kẻ địch bên trong.
    /// Sau một khoảng thời gian, các vách sụp xuống gây sát thương.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class Skill_EarthPrison : MonoBehaviour
    {
        public event Action<Skill_EarthPrison, EnemyBrain> TargetFound;

        [Header("Lifetime")]
        [Tooltip("Thời gian nhốt kẻ địch trước khi vách đất sụp xuống gây sát thương.")]
        [SerializeField, Min(0.1f)] private float duration = 2.5f;

        private CircleCollider2D m_AreaCollider;
        private ColliderOwnerRef m_OwnerRef;

        private bool m_Initialized;
        private Coroutine m_CollapseCoroutine;
        private Action<Skill_EarthPrison> m_ReturnToPool;

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
            if (m_CollapseCoroutine != null)
            {
                StopCoroutine(m_CollapseCoroutine);
                m_CollapseCoroutine = null;
            }

            m_Initialized = false;
            m_ReturnToPool = null;
            TargetFound = null;
        }

        public void Initialize(
            Vector2 position,
            Action<Skill_EarthPrison> returnToPool)
        {
            Clear();

            transform.position = position;
            m_ReturnToPool = returnToPool;
            m_Initialized = true;
        }

        public void StartSkill()
        {
            if (!m_Initialized || !isActiveAndEnabled)
                return;

            if (m_CollapseCoroutine != null)
                StopCoroutine(m_CollapseCoroutine);

            m_CollapseCoroutine = StartCoroutine(CollapseRoutine());
        }

        private IEnumerator CollapseRoutine()
        {
            yield return new WaitForSeconds(duration);
            m_CollapseCoroutine = null;

            if (!m_Initialized)
                yield break;

            // Khi vách đất sụp xuống -> gây sát thương toàn bộ địch trong vùng.
            FindAndDamageTargets();
            FinishSkill();
        }

        private void FindAndDamageTargets()
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
            Action<Skill_EarthPrison> returnToPool = m_ReturnToPool;

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
        }
    }
}
