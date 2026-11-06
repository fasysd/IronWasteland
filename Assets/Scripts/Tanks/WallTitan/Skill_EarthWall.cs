using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.WallTitan
{
    /// <summary>
    /// Kỹ năng chuyên biệt của Wall Titan — Earth Wall:
    /// Tạo một bức tường đất tại vị trí chỉ định trong thời gian giới hạn.
    /// Bức tường chặn đường di chuyển và các đòn tấn công/đạn bay đi qua vị trí của nó.
    /// Khi hết thời gian, bức tường biến mất.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(ColliderOwnerRef))]
    public class Skill_EarthWall : MonoBehaviour
    {
        [Header("Lifetime")]
        [SerializeField, Min(0.1f)] private float duration = 5f;

        private BoxCollider2D m_WallCollider;
        private ColliderOwnerRef m_OwnerRef;

        private bool m_Initialized;
        private Coroutine m_LifetimeCoroutine;
        private Action<Skill_EarthWall> m_ReturnToPool;

        public ColliderOwnerRef ColliderRef => m_OwnerRef;

        private void Awake()
        {
            m_WallCollider = GetComponent<BoxCollider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();

            // Tường đất chặn di chuyển vật lý (không phải trigger).
            if (m_WallCollider != null)
                m_WallCollider.isTrigger = false;
        }

        public void Clear()
        {
            if (m_LifetimeCoroutine != null)
            {
                StopCoroutine(m_LifetimeCoroutine);
                m_LifetimeCoroutine = null;
            }

            m_Initialized = false;
            m_ReturnToPool = null;
        }

        public void Initialize(
            Vector2 position,
            Quaternion rotation,
            Action<Skill_EarthWall> returnToPool)
        {
            Clear();

            transform.SetPositionAndRotation(position, rotation);
            m_ReturnToPool = returnToPool;
            m_Initialized = true;
        }

        public void StartSkill()
        {
            if (!m_Initialized || !isActiveAndEnabled)
                return;

            if (m_LifetimeCoroutine != null)
                StopCoroutine(m_LifetimeCoroutine);

            m_LifetimeCoroutine = StartCoroutine(WallLifetimeRoutine());
        }

        private IEnumerator WallLifetimeRoutine()
        {
            yield return new WaitForSeconds(duration);
            m_LifetimeCoroutine = null;

            if (m_Initialized)
                FinishSkill();
        }

        private void FinishSkill()
        {
            Action<Skill_EarthWall> returnToPool = m_ReturnToPool;

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
