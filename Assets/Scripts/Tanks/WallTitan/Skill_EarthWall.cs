using IronWasteland.Enemies.Navigation;
using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.WallTitan
{
    /// <summary>
    /// Kỹ năng chuyên biệt của Wall Titan — Earth Wall:
    /// Tạo một bức tường đất tại vị trí chỉ định trong thời gian giới hạn.
    /// Bức tường chặn đường di chuyển và các đòn tấn công/đạn bay đi qua vị trí của nó.
    /// Thông báo cho PathfindingGrid2D để Enemy tìm đường né khi tường xuất hiện,
    /// và thông báo hủy chặn khi bức tường biến mất.
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
        private bool m_IsRegisteredObstacle;
        private Coroutine m_LifetimeCoroutine;
        private Action<Skill_EarthWall> m_ReturnToPool;

        public ColliderOwnerRef ColliderRef => m_OwnerRef;
        public BoxCollider2D WallCollider => m_WallCollider;

        /// <summary>Thời gian tồn tại, cho phép Brain ghi đè theo GDD thay vì dùng giá trị trong prefab.</summary>
        public float Duration
        {
            get => duration;
            set => duration = Mathf.Max(0.05f, value);
        }

        private void Awake()
        {
            m_WallCollider = GetComponent<BoxCollider2D>();
            m_OwnerRef = GetComponent<ColliderOwnerRef>();

            int terrainLayerIndex = LayerMask.NameToLayer("Terrain");
            if (terrainLayerIndex != -1)
            {
                gameObject.layer = terrainLayerIndex;
            }

            // Tường đất chặn di chuyển vật lý (không phải trigger).
            if (m_WallCollider != null)
                m_WallCollider.isTrigger = false;
        }

        public void Clear()
        {
            UnregisterFromGrid();

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

            // Đăng ký bức tường làm chướng ngại vật với PathfindingGrid2D để Enemy tìm đường né
            RegisterWithGrid();

            if (m_LifetimeCoroutine != null)
                StopCoroutine(m_LifetimeCoroutine);

            m_LifetimeCoroutine = StartCoroutine(WallLifetimeRoutine());
        }

        private void RegisterWithGrid()
        {
            if (m_IsRegisteredObstacle) return;

            if (m_WallCollider != null && PathfindingGrid2D.Instance != null)
            {
                PathfindingGrid2D.Instance.AddObstacle(m_WallCollider);
                m_IsRegisteredObstacle = true;
            }
        }

        private void UnregisterFromGrid()
        {
            if (!m_IsRegisteredObstacle) return;

            if (m_WallCollider != null && PathfindingGrid2D.HasInstance)
            {
                PathfindingGrid2D.Instance.RemoveObstacle(m_WallCollider);
            }
            m_IsRegisteredObstacle = false;
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

        private void OnDestroy()
        {
            UnregisterFromGrid();
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0.1f, duration);
        }
    }
}
