using IronWasteland.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.RedNova
{
    /// <summary>
    /// Kỹ năng tùy chọn 1 (Nhánh 1) của Red Nova — Laser Cannon:
    /// Tia laser năng lượng xuyên qua các mục tiêu trên đường thẳng.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class Skill_LaserCannon : MonoBehaviour
    {
        public event Action<Skill_LaserCannon, EnemyBrain> TargetFound;

        [Header("Beam")]
        [SerializeField, Min(0.05f)] private float beamDuration = 0.25f;
        [SerializeField, Min(0f)] private float hitCheckDelay = 0.05f;

        private BoxCollider2D m_BeamCollider;
        private ColliderOwnerRef m_OwnerRef;
        private SpriteRenderer m_SpriteRenderer;
        private Color m_InitialColor = Color.white;

        private bool m_Initialized;
        private Coroutine m_BeamCoroutine;
        private Action<Skill_LaserCannon> m_ReturnToPool;

        public ColliderOwnerRef ColliderRef => m_OwnerRef;

        private void Awake()
        {
            m_BeamCollider = GetComponent<BoxCollider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();
            m_SpriteRenderer = GetComponent<SpriteRenderer>();

            if (m_SpriteRenderer != null)
                m_InitialColor = m_SpriteRenderer.color;

            if (m_BeamCollider != null)
                m_BeamCollider.isTrigger = true;
        }

        public void Clear()
        {
            if (m_BeamCoroutine != null)
            {
                StopCoroutine(m_BeamCoroutine);
                m_BeamCoroutine = null;
            }

            if (m_SpriteRenderer != null)
                m_SpriteRenderer.color = m_InitialColor;

            m_Initialized = false;
            m_ReturnToPool = null;
            TargetFound = null;
        }

        public void Initialize(
            Vector2 origin,
            Vector2 direction,
            Action<Skill_LaserCannon> returnToPool)
        {
            Clear();

            m_ReturnToPool = returnToPool;
            Vector2 dir = direction.normalized;
            m_Initialized = dir.sqrMagnitude > 0.0001f;

            if (!m_Initialized)
            {
                FinishSkill();
                return;
            }

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, angle));

            if (m_SpriteRenderer != null)
                m_SpriteRenderer.color = m_InitialColor;
        }

        public void StartSkill()
        {
            if (!m_Initialized || !isActiveAndEnabled)
                return;

            if (m_BeamCoroutine != null)
                StopCoroutine(m_BeamCoroutine);

            m_BeamCoroutine = StartCoroutine(BeamRoutine());
        }

        private IEnumerator BeamRoutine()
        {
            // Tia laser xuyên mục tiêu lập tức khi phát hỏa
            if (m_Initialized)
                PierceTargets();

            if (hitCheckDelay > 0f)
                yield return new WaitForSeconds(hitCheckDelay);

            // Kiểm tra quét lại trong trường hợp kẻ địch vừa đi vào tia
            if (m_Initialized)
                PierceTargets();

            float remain = Mathf.Max(0.01f, beamDuration - hitCheckDelay);
            float elapsed = 0f;

            while (elapsed < remain)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / remain);
                if (m_SpriteRenderer != null)
                {
                    Color c = m_InitialColor;
                    c.a = Mathf.Lerp(m_InitialColor.a, 0f, progress);
                    m_SpriteRenderer.color = c;
                }
                yield return null;
            }

            m_BeamCoroutine = null;

            if (m_Initialized)
                FinishSkill();
        }

        private void PierceTargets()
        {
            if (m_OwnerRef == null)
                return;

            var enemies = new HashSet<EnemyBrain>();

            // 1. Quét qua trigger tracking của ColliderOwnerRef
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

            // 2. Quét trực tiếp bằng BoxCollider2D Overlap để đảm bảo tức thời và chính xác
            if (m_BeamCollider != null)
            {
                ContactFilter2D filter = ContactFilter2D.noFilter;
                Collider2D[] results = new Collider2D[32];
                int count = m_BeamCollider.Overlap(filter, results);
                for (int i = 0; i < count; i++)
                {
                    if (results[i] == null)
                        continue;

                    ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(results[i]);
                    if (targetRef == null)
                        continue;

                    EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
                    if (enemy != null)
                        enemies.Add(enemy);
                }
            }

            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null)
                    TargetFound?.Invoke(this, enemy);
            }
        }

        private void FinishSkill()
        {
            Action<Skill_LaserCannon> returnToPool = m_ReturnToPool;

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
            beamDuration = Mathf.Max(0.05f, beamDuration);
            hitCheckDelay = Mathf.Max(0f, hitCheckDelay);
        }
    }
}
