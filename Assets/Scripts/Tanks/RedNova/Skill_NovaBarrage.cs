using IronWasteland.Enemies;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.RedNova
{
    /// <summary>
    /// Kỹ năng tùy chọn 2 (Nhánh 0) của Red Nova — Nova Barrage:
    /// Gọi một loạt đạn năng lượng oanh kích một khu vực, gây sát thương nhiều lần.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class Skill_NovaBarrage : MonoBehaviour
    {
        public event Action<Skill_NovaBarrage, EnemyBrain> TargetFound;

        [Header("Lifetime")]
        [SerializeField, Min(0.1f)] private float duration = 3f;
        [SerializeField, Min(0.05f)] private float interval = 0.4f;

        [Header("Hiển thị (Assets/Art/Tank/VFX/RedNova)")]
        [Tooltip("SpriteRenderer dùng để vẽ vòng cảnh báo / đạn rơi / vụ nổ.")]
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Sprite warningSprite;
        [SerializeField] private Sprite fallingSprite;
        [SerializeField] private Sprite impactSprite;
        [Tooltip("Thời gian hiện vòng cảnh báo trước khi đạn bắt đầu rơi.")]
        [SerializeField, Min(0.05f)] private float warningDuration = 0.5f;
        [SerializeField, Min(0.02f)] private float fallingDuration = 0.08f;
        [SerializeField, Min(0.02f)] private float impactFlashDuration = 0.12f;

        private CircleCollider2D m_AreaCollider;
        private ColliderOwnerRef m_OwnerRef;

        private float m_ElapsedTime;
        private bool m_Initialized;
        private Coroutine m_LoopCoroutine;
        private Action<Skill_NovaBarrage> m_ReturnToPool;

        public ColliderOwnerRef ColliderRef => m_OwnerRef;
        /// <summary>Cho phép Brain ghi đè thời gian tồn tại / giãn cách theo GDD.</summary>
        public float Duration
        {
            get => duration;
            set => duration = Mathf.Max(0.05f, value);
        }

        public float Interval
        {
            get => interval;
            set => interval = Mathf.Max(0.02f, value);
        }

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
            SetVisual(null);
        }

        public void Initialize(
            Vector2 position,
            Action<Skill_NovaBarrage> returnToPool)
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

            // --- Giai đoạn 1: vòng cảnh báo ---
            SetVisual(warningSprite);

            float warn = Mathf.Min(warningDuration, duration);
            if (warn > 0f)
            {
                yield return new WaitForSeconds(warn);
                elapsed += warn;
                m_ElapsedTime = elapsed;
            }

            // --- Giai đoạn 2: đạn rơi + nổ từng đợt ---
            while (elapsed < duration && m_Initialized)
            {
                SetVisual(fallingSprite);
                yield return new WaitForSeconds(fallingDuration);
                elapsed += fallingDuration;

                if (!m_Initialized)
                    break;

                SetVisual(impactSprite);
                FindTargets();

                yield return new WaitForSeconds(impactFlashDuration);
                elapsed += impactFlashDuration;
                m_ElapsedTime = elapsed;

                // Nghỉ cho tới nhịp kế tiếp để tổng chu kỳ xấp xỉ interval.
                float rest = interval - fallingDuration - impactFlashDuration;
                if (rest > 0f && elapsed < duration)
                {
                    SetVisual(null);
                    float wait = Mathf.Min(rest, duration - elapsed);
                    yield return new WaitForSeconds(wait);
                    elapsed += wait;
                    m_ElapsedTime = elapsed;
                }
            }

            SetVisual(null);
            m_LoopCoroutine = null;

            if (m_Initialized)
                FinishSkill();
        }

        private void SetVisual(Sprite sprite)
        {
            if (visual == null)
                return;

            visual.sprite = sprite;
            visual.enabled = sprite != null;
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
            Action<Skill_NovaBarrage> returnToPool = m_ReturnToPool;

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
