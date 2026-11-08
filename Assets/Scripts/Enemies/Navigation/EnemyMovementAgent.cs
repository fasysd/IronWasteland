using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Enemies.Navigation
{
    /// <summary>
    /// Điều khiển việc di chuyển vật lý của quái vật theo danh sách waypoint.
    /// Tích hợp cơ chế:
    /// 1. Bám theo đường đi từ A*.
    /// 2. Lực đẩy né tránh nhẹ giữa các quái vật (Separation steering).
    /// 3. Triệt tiêu chồng chéo tức thì giữa các Collider của EnemyModel (Penetration Resolution).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyMovementAgent : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private EnemyModel model;

        [Header("Layers")]
        [SerializeField] private LayerMask enemyLayer;

        [Header("Separation Settings")]
        [Tooltip("Bán kính cảm nhận các kẻ địch khác để tạo lực tách né nhau.")]
        [SerializeField] private float separationRadius = 1.2f;

        [Tooltip("Cường độ lực tách quái vật.")]
        [SerializeField] private float separationForce = 2.5f;

        [Tooltip("Giới hạn ảnh hưởng tối đa của lực tách so với hướng di chuyển chính (0 = tắt né hoàn toàn, 1 = né tối đa).")]
        [SerializeField, Range(0f, 1f)] private float maxSeparationInfluence = 0.35f;

        [Tooltip("Khoảng cách được coi là đã chạm tới waypoint.")]
        [SerializeField] private float waypointTolerance = 0.25f;

        [Header("Rotation Settings")]
        [Tooltip("Có tự động xoay theo hướng di chuyển hay không.")]
        [SerializeField] private bool rotateTowardsMovement = false;

        [Tooltip("Góc bù độ lệch ban đầu của Sprite (vd: -90 nếu Sprite hướng lên trên (+Y), +90 nếu Sprite hướng xuống dưới (-Y)).")]
        [SerializeField] private float rotationOffset = -90f;

        [Tooltip("Tốc độ xoay (độ/giây). Giá trị <= 0 sẽ xoay tức thời.")]
        [SerializeField] private float rotationSpeed = 720f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2f;

        private readonly List<Vector2> m_Path = new List<Vector2>();
        private int m_CurrentWaypointIndex;
        private bool m_IsMoving;
        private Vector2Int m_CurrentGridCoord = new Vector2Int(-1, -1);

        public bool IsMoving => m_IsMoving;
        public IReadOnlyList<Vector2> CurrentPath => m_Path;
        public int CurrentWaypointIndex => m_CurrentWaypointIndex;
        public Vector2 Destination => m_Path.Count > 0 ? m_Path[m_Path.Count - 1] : (Vector2)transform.position;
        public float MoveSpeed => moveSpeed;

        public float MaxSeparationInfluence
        {
            get => maxSeparationInfluence;
            set => maxSeparationInfluence = Mathf.Clamp01(value);
        }

        public bool RotateTowardsMovement
        {
            get => rotateTowardsMovement;
            set => rotateTowardsMovement = value;
        }

        public float RotationOffset
        {
            get => rotationOffset;
            set => rotationOffset = value;
        }

        public float RotationSpeed
        {
            get => rotationSpeed;
            set => rotationSpeed = value;
        }

        public void SetRotateTowardsMovement(bool enable, float offset = -90f, float speed = 720f)
        {
            rotateTowardsMovement = enable;
            rotationOffset = offset;
            rotationSpeed = speed;
        }

        public void Initialize(EnemyModel enemyModel, LayerMask enemyLayerMask)
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (model == null) model = enemyModel ?? GetComponentInChildren<EnemyModel>();
            if (enemyLayerMask.value != 0) enemyLayer = enemyLayerMask;
            if (body != null)
            {
                body.freezeRotation = true;
            }
        }

        public void SetSpeed(float speed)
        {
            moveSpeed = Mathf.Max(0f, speed);
        }

        public void SetPath(List<Vector2> newPath)
        {
            m_Path.Clear();
            if (newPath != null && newPath.Count > 0)
            {
                m_Path.AddRange(newPath);
                m_CurrentWaypointIndex = 0;
                m_IsMoving = true;
            }
            else
            {
                Stop();
            }
        }

        public void Stop()
        {
            m_Path.Clear();
            m_CurrentWaypointIndex = 0;
            m_IsMoving = false;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
            model?.SetMoveSpeed(0f);
        }

        public void Teleport(Vector2 destination)
        {
            Stop();
            transform.position = new Vector3(destination.x, destination.y, transform.position.z);
            if (body != null)
            {
                body.position = destination;
                body.linearVelocity = Vector2.zero;
            }
            PathfindingGrid2D.Instance?.UpdateEnemyPosition(ref m_CurrentGridCoord, destination);
        }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (model == null) model = GetComponentInChildren<EnemyModel>();
            if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");
            if (body != null)
            {
                body.freezeRotation = true;
            }
        }

        private void OnEnable()
        {
            Vector2 currentPos = body != null ? body.position : (Vector2)transform.position;
            m_CurrentGridCoord = PathfindingGrid2D.Instance?.RegisterEnemy(currentPos) ?? new Vector2Int(-1, -1);
        }

        private void OnDisable()
        {
            PathfindingGrid2D.Instance?.UnregisterEnemy(m_CurrentGridCoord);
            m_CurrentGridCoord = new Vector2Int(-1, -1);
        }

        private void FixedUpdate()
        {
            Vector2 currentPos = body != null ? body.position : (Vector2)transform.position;

            // Di chuyển theo đường đi nếu đang có waypoint
            if (!m_IsMoving || m_Path.Count == 0)
            {
                if (body != null)
                {
                    body.linearVelocity = Vector2.zero;
                }
                return;
            }

            // Cập nhật vị trí ô lưới cho EnemyPresence chỉ khi di chuyển
            PathfindingGrid2D.Instance?.UpdateEnemyPosition(ref m_CurrentGridCoord, currentPos);

            Vector2 targetWaypoint = m_Path[m_CurrentWaypointIndex];

            float distToWaypoint = Vector2.Distance(currentPos, targetWaypoint);
            if (distToWaypoint <= waypointTolerance)
            {
                m_CurrentWaypointIndex++;
                if (m_CurrentWaypointIndex >= m_Path.Count)
                {
                    // Đã tới điểm cuối
                    Stop();
                    return;
                }
                targetWaypoint = m_Path[m_CurrentWaypointIndex];
            }

            Vector2 moveDir = (targetWaypoint - currentPos).normalized;

            // Cộng thêm lực né tránh nhẹ (Soft Separation) nếu có quái khác gần kề.
            // Tránh đè nhau chỉ là tiêu chí phụ nên hướng di chuyển theo waypoint luôn là ưu tiên chính.
            Vector2 sepDir = CalculateSeparationDirection(currentPos);
            float separationWeight = Mathf.Clamp(separationForce * 0.15f, 0f, maxSeparationInfluence);
            Vector2 combinedDir = moveDir + sepDir * separationWeight;
            if (combinedDir.sqrMagnitude < 0.001f)
            {
                combinedDir = moveDir;
            }

            Vector2 finalVelocity = combinedDir.normalized * moveSpeed;

            if (body != null)
            {
                body.linearVelocity = finalVelocity;
            }
            else
            {
                transform.position += (Vector3)(finalVelocity * Time.fixedDeltaTime);
            }

            model?.SetMoveSpeed(finalVelocity.magnitude);

            // Xoay theo hướng di chuyển nếu được bật
            if (rotateTowardsMovement && finalVelocity.sqrMagnitude > 0.001f)
            {
                float targetAngle = Mathf.Atan2(finalVelocity.y, finalVelocity.x) * Mathf.Rad2Deg + rotationOffset;
                Transform targetTransform = model != null ? model.transform : transform;
                if (rotationSpeed > 0f)
                {
                    float currentAngle = targetTransform.eulerAngles.z;
                    float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.fixedDeltaTime);
                    targetTransform.rotation = Quaternion.Euler(0f, 0f, newAngle);
                }
                else
                {
                    targetTransform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
                }
            }
        }

        /// <summary>
        /// Tính vector đẩy ra xa các quái vật lân cận một cách mềm mại (linear falloff).
        /// </summary>
        private Vector2 CalculateSeparationDirection(Vector2 currentPos)
        {
            Collider2D[] neighbors = Physics2D.OverlapCircleAll(currentPos, separationRadius, enemyLayer);
            if (neighbors == null || neighbors.Length <= 1) return Vector2.zero;

            Vector2 separation = Vector2.zero;
            int count = 0;
            Transform myRoot = transform.root;

            for (int i = 0; i < neighbors.Length; i++)
            {
                Collider2D other = neighbors[i];
                if (other == null || other.transform.root == myRoot) continue;

                Vector2 otherPos = other.transform.position;
                Vector2 diff = currentPos - otherPos;
                float distance = diff.magnitude;

                if (distance < separationRadius)
                {
                    Vector2 push;
                    if (distance > 0.001f)
                    {
                        float factor = 1f - (distance / separationRadius);
                        push = diff.normalized * factor;
                    }
                    else
                    {
                        // Khi trùng khít tọa độ, tạo lực tách nhẹ dựa theo hash code
                        int id = GetHashCode();
                        float angle = (id & 7) * 45f * Mathf.Deg2Rad;
                        push = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    }

                    separation += push;
                    count++;
                }
            }

            return count > 0 ? (separation / count).normalized : Vector2.zero;
        }

        private void OnDrawGizmosSelected()
        {
            if (m_Path != null && m_Path.Count > 0)
            {
                Gizmos.color = Color.magenta;
                for (int i = 0; i < m_Path.Count - 1; i++)
                {
                    Gizmos.DrawLine(m_Path[i], m_Path[i + 1]);
                }

                if (m_CurrentWaypointIndex < m_Path.Count)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(m_Path[m_CurrentWaypointIndex], waypointTolerance);
                }
            }
        }
    }
}

