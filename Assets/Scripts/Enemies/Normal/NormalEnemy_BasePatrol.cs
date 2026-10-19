using System;
using System.Collections.Generic;
using IronWasteland.Enemies.Navigation;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IronWasteland.Enemies.Normal
{
    public enum PatrolLoopMode
    {
        [InspectorName("Lặp lại vòng tròn (Loop)")]
        Loop,
        [InspectorName("Lặp lại quay lui (PingPong)")]
        PingPong
    }

    /// <summary>
    /// Lớp cơ sở cho các Normal Enemy di chuyển theo quỹ đạo định sẵn (Waypoint Patrol).
    /// Không sử dụng logic tìm đường A*.
    /// Hỗ trợ 2 chế độ quỹ đạo:
    /// 1. Loop: Khi tới điểm cuối cùng, điểm tiếp theo là điểm đầu tiên (0 -> 1 -> ... -> N-1 -> 0 -> 1 ...).
    /// 2. PingPong: Khi tới điểm cuối cùng, duyệt ngược lại danh sách; khi về tới điểm đầu tiên, duyệt xuôi lại (0 -> 1 -> ... -> N-1 -> N-2 -> ... -> 0 -> 1 ...).
    /// </summary>
    [RequireComponent(typeof(EnemyMovementAgent))]
    public abstract class NormalEnemy_BasePatrol : NormalEnemyBrain
    {
        [Header("Patrol Settings (Quỹ đạo di chuyển)")]
        [Tooltip("Chế độ lặp lại quỹ đạo:\n- Loop: Vòng tròn (đi tới điểm cuối rồi quay lại điểm đầu tiên).\n- PingPong: Quay lui (đi tới điểm cuối thì đi ngược chiều, tới điểm đầu thì đi xuôi lại).")]
        [SerializeField] protected PatrolLoopMode loopMode = PatrolLoopMode.Loop;

        [Tooltip("Danh sách các điểm tọa độ World Space tạo thành quỹ đạo di chuyển.")]
        [ContextMenuItem("Thêm tọa độ hiện tại của đối tượng", nameof(AddWaypointAtCurrentPosition))]
        [SerializeField] protected List<Vector2> waypoints = new List<Vector2>();

        [Tooltip("Khoảng cách tiếp cận được tính là đã chạm tới điểm quỹ đạo để chuyển sang điểm tiếp theo.")]
        [SerializeField, Min(0.05f)] protected float arrivalDistance = 0.3f;

        [Tooltip("Thời gian dừng nghỉ tại mỗi điểm quỹ đạo (giây). 0 = di chuyển liên tục không dừng.")]
        [SerializeField, Min(0f)] protected float waitTimeAtWaypoint = 0f;

        [Header("Layers")]
        [SerializeField] protected LayerMask enemyLayer;

        [Header("Runtime Info")]
        [SerializeField, ReadOnly] protected int currentWaypointIndex = 0;
        [SerializeField, ReadOnly] protected int patrolDirection = 1;
        [SerializeField, ReadOnly] protected bool isWaiting = false;
        [SerializeField, ReadOnly] protected float waitTimer = 0f;

        protected EnemyMovementAgent movementAgent;

        public PatrolLoopMode LoopMode { get => loopMode; set => loopMode = value; }
        public List<Vector2> Waypoints => waypoints;
        public int CurrentWaypointIndex => currentWaypointIndex;
        public int PatrolDirection => patrolDirection;

        public override void Initialize(EnemyStats overrideStats = null)
        {
            base.Initialize(overrideStats);

            if (movementAgent == null) movementAgent = GetComponent<EnemyMovementAgent>();
            if (movementAgent == null) movementAgent = gameObject.AddComponent<EnemyMovementAgent>();
            if (movementAgent != null)
            {
                movementAgent.Initialize(Model, enemyLayer);
                if (Stats != null && Stats.MoveSpeed > 0f)
                {
                    movementAgent.SetSpeed(Stats.MoveSpeed);
                }
            }
        }

        protected override void Awake()
        {
            if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");
            if (movementAgent == null) movementAgent = GetComponent<EnemyMovementAgent>();
            if (movementAgent == null) movementAgent = gameObject.AddComponent<EnemyMovementAgent>();

            base.Awake();
        }

        protected virtual void Start()
        {
            if (movementAgent != null && Stats != null)
            {
                if (Stats.MoveSpeed > 0f)
                {
                    movementAgent.SetSpeed(Stats.MoveSpeed);
                }
                else
                {
                    Stats.MoveSpeed = movementAgent.MoveSpeed;
                }
            }

            // Khởi đầu di chuyển tới điểm đầu tiên nếu có danh sách
            if (waypoints.Count > 0)
            {
                currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, waypoints.Count - 1);
                patrolDirection = 1;
                movementAgent?.SetPath(new List<Vector2> { waypoints[currentWaypointIndex] });
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            if (movementAgent != null && Stats != null && Stats.MoveSpeed > 0f)
            {
                movementAgent.SetSpeed(Stats.MoveSpeed);
            }

            if (waypoints == null || waypoints.Count == 0)
            {
                movementAgent?.Stop();
                return;
            }

            // Trường hợp chỉ có 1 điểm duy nhất
            if (waypoints.Count == 1)
            {
                Vector2 targetOne = waypoints[0];
                if (Vector2.Distance(transform.position, targetOne) > arrivalDistance)
                {
                    if (movementAgent != null && !movementAgent.IsMoving)
                    {
                        movementAgent.SetPath(new List<Vector2> { targetOne });
                    }
                }
                else
                {
                    movementAgent?.Stop();
                }
                return;
            }

            currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, waypoints.Count - 1);
            Vector2 currentWaypoint = waypoints[currentWaypointIndex];
            float distToWaypoint = Vector2.Distance(transform.position, currentWaypoint);

            // Kiểm tra trạng thái chờ tại waypoint
            if (isWaiting)
            {
                waitTimer += deltaTime;
                if (waitTimer >= waitTimeAtWaypoint)
                {
                    isWaiting = false;
                    waitTimer = 0f;
                    AdvanceToNextWaypoint();
                }
                return;
            }

            // Đã chạm tới waypoint hiện tại
            if (distToWaypoint <= arrivalDistance || (movementAgent != null && !movementAgent.IsMoving && distToWaypoint <= arrivalDistance * 1.5f))
            {
                if (waitTimeAtWaypoint > 0f)
                {
                    isWaiting = true;
                    waitTimer = 0f;
                    movementAgent?.Stop();
                    return;
                }

                AdvanceToNextWaypoint();
            }
            else
            {
                // Tiếp tục hướng tới waypoint hiện tại nếu agent vô tình bị dừng
                if (movementAgent != null && !movementAgent.IsMoving)
                {
                    movementAgent.SetPath(new List<Vector2> { currentWaypoint });
                }
            }
        }

        /// <summary>
        /// Chuyển sang điểm quỹ đạo tiếp theo dựa trên chế độ Loop hoặc PingPong.
        /// </summary>
        protected virtual void AdvanceToNextWaypoint()
        {
            if (waypoints.Count < 2) return;

            if (loopMode == PatrolLoopMode.Loop)
            {
                // Lặp lại vòng tròn: điểm cuối -> điểm đầu tiên
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
            }
            else // PingPong
            {
                // Lặp lại quay lui: chạm cuối thì duyệt ngược lại, chạm đầu thì duyệt xuôi lại
                if (patrolDirection > 0)
                {
                    if (currentWaypointIndex + 1 >= waypoints.Count)
                    {
                        patrolDirection = -1;
                        currentWaypointIndex = waypoints.Count - 2;
                    }
                    else
                    {
                        currentWaypointIndex++;
                    }
                }
                else // patrolDirection < 0
                {
                    if (currentWaypointIndex - 1 < 0)
                    {
                        patrolDirection = 1;
                        currentWaypointIndex = 1;
                    }
                    else
                    {
                        currentWaypointIndex--;
                    }
                }
            }

            currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, waypoints.Count - 1);
            movementAgent?.SetPath(new List<Vector2> { waypoints[currentWaypointIndex] });
        }

        protected override void OnDeath()
        {
            if (movementAgent != null)
            {
                movementAgent.Stop();
            }
            Destroy(gameObject);
        }

        #region Context Menu Utilities

        [ContextMenu("Thêm điểm quỹ đạo tại vị trí hiện tại")]
        public void AddWaypointAtCurrentPosition()
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, "Thêm điểm quỹ đạo tại vị trí hiện tại của đối tượng");
#endif
            waypoints.Add(transform.position);
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            Debug.Log($"[{name}] Đã thêm điểm quỹ đạo #{waypoints.Count - 1} tại vị trí hiện tại: {transform.position}", this);
#endif
        }

        [ContextMenu("Xóa toàn bộ điểm quỹ đạo")]
        public void ClearWaypoints()
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, "Clear Waypoints");
#endif
            waypoints.Clear();
            currentWaypointIndex = 0;
            patrolDirection = 1;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            Debug.Log($"[{name}] Đã xóa toàn bộ điểm quỹ đạo.", this);
#endif
        }

        [ContextMenu("Tạo mẫu quỹ đạo hình vuông quanh vị trí hiện tại")]
        public void CreateSampleSquareWaypoints()
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, "Create Sample Square Waypoints");
#endif
            Vector2 pos = transform.position;
            float r = 3f;
            waypoints = new List<Vector2>
            {
                pos + new Vector2(-r, -r),
                pos + new Vector2(r, -r),
                pos + new Vector2(r, r),
                pos + new Vector2(-r, r)
            };
            currentWaypointIndex = 0;
            patrolDirection = 1;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            Debug.Log($"[{name}] Đã tạo 4 điểm quỹ đạo mẫu hình vuông quanh {pos}.", this);
#endif
        }

        #endregion

        #region Editor Gizmos

        protected virtual void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Count == 0) return;

            Gizmos.color = Color.magenta;
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 p = waypoints[i];
                Gizmos.DrawWireSphere(p, 0.25f);
                if (i == currentWaypointIndex && Application.isPlaying)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(p, 0.15f);
                    Gizmos.color = Color.magenta;
                }
#if UNITY_EDITOR
                Handles.Label(p + Vector3.up * 0.35f, $"#{i}");
#endif
            }

            // Đường nối các waypoint
            Gizmos.color = loopMode == PatrolLoopMode.Loop ? Color.cyan : new Color(1f, 0.6f, 0f);
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
            }

            if (waypoints.Count > 1 && loopMode == PatrolLoopMode.Loop)
            {
                Gizmos.DrawLine(waypoints[waypoints.Count - 1], waypoints[0]);
            }
        }

        #endregion
    }
}

