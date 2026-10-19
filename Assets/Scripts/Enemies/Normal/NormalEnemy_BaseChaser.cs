using System;
using System.Collections.Generic;
using IronWasteland.Enemies.Navigation;
using IronWasteland.Tanks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IronWasteland.Enemies.Normal
{
    public enum NormalEnemyState
    {
        IdleAtHome,        // Đang ở tâm D, không có mục tiêu
        WaitingToReturn,   // Không có mục tiêu, đang rời xa D, chờ đủ X giây
        ReturningHome,     // Không có mục tiêu, quá X giây, đang tìm đường đi bộ về D
        TeleportingHome,   // Không có mục tiêu, quá Y giây, dịch chuyển tức thì về D
        Chasing,           // Đang bám đuổi để mục tiêu nằm trong bán kính A
        StandStillInB      // Mục tiêu nằm trong B nhưng không tìm thấy đường vào A (hoặc đã nằm trong A)
    }

    /// <summary>
    /// Lớp cơ sở cho các Normal Enemy có logic di chuyển và tìm đường A* theo bán kính A, B, C, D, X, Y.
    /// </summary>
    [RequireComponent(typeof(EnemyMovementAgent))]
    public abstract class NormalEnemy_BaseChaser : NormalEnemyBrain
    {
        [Header("Territory Settings (Điểm D và Vùng C)")]
        [Tooltip("Điểm cố định D trên World Space (tâm vùng lãnh thổ).")]
        [ContextMenuItem("Lấy tọa độ hiện tại làm điểm D", nameof(SetCurrentPositionAsAnchorD))]
        [SerializeField] protected Vector2 anchorPointD;

        [Tooltip("Nếu bật, tự động lấy tọa độ vị trí Transform khi Awake làm điểm D. Nếu tắt, sử dụng chính xác tọa độ anchorPointD cấu hình trong Inspector.")]
        [SerializeField] protected bool autoSetAnchorDFromSpawnPosition = false;

        [Tooltip("Bán kính C quanh điểm cố định D. Mục tiêu hoặc đường đi vượt quá vùng này sẽ coi như không phát hiện.")]
        [SerializeField, Min(0.5f)] protected float radiusC = 10f;

        [Header("Engagement Ranges (Bán kính A và B)")]
        [Tooltip("Bán kính A: Phạm vi tiếp cận mong muốn (khi mục tiêu vào tầm A thì dừng lại).")]
        [SerializeField, Min(0.1f)] protected float radiusA = 2f;

        [Tooltip("Bán kính B (B >= A): Khi mục tiêu vượt ra ngoài B thì tìm đường để đưa mục tiêu vào trong tầm A.")]
        [SerializeField, Min(0.1f)] protected float radiusB = 5f;

        [Header("Return Home Settings (Thời gian X, Y và khoảng cách)")]
        [Tooltip("Thời gian X (giây): Khi không có mục tiêu và rời xa điểm D quá X giây thì bắt đầu tìm đường đi bộ về.")]
        [SerializeField, Min(0f)] protected float returnHomeDelayX = 3f;

        [Tooltip("Thời gian Y (giây, Y >= X): Rời xa điểm D quá Y giây thì dịch chuyển tức thì về D.")]
        [SerializeField, Min(0f)] protected float teleportHomeDelayY = 8f;

        [Tooltip("Khoảng cách được coi là đã về đến điểm D.")]
        [SerializeField, Min(0.05f)] protected float homeArrivalTolerance = 0.35f;

        [Tooltip("Khoảng cách dừng trước điểm D khi tìm đường về.")]
        [SerializeField, Min(0.05f)] protected float homeStopDistance = 0.2f;

        [Header("Layers")]
        [SerializeField] protected LayerMask terrainLayer;
        [SerializeField] protected LayerMask enemyLayer;
        [SerializeField] protected LayerMask tankLayer;

        [Header("Navigation Settings")]
        [SerializeField, Range(0.2f, 1f)] protected float pathNodeSize = 0.5f;
        [SerializeField, Range(0.1f, 1f)] protected float repathInterval = 0.25f;

        [Tooltip("Độ ưu tiên né kẻ địch khác (penalty) trên lưới A*. 0 = không né, đi xuyên qua.")]
        [SerializeField, Min(0f)] protected float enemyAvoidancePenalty = 1.5f;

        [Tooltip("Bán kính cảm quan của agent khi làm mịn đường đi.")]
        [SerializeField, Min(0.05f)] protected float agentPathRadius = 0.3f;

        [Header("Debug & Runtime State")]
        [SerializeField, ReadOnly] protected NormalEnemyState currentState = NormalEnemyState.IdleAtHome;
        [SerializeField, ReadOnly] protected float awayFromHomeTimer = 0f;
        [SerializeField, ReadOnly] protected Transform currentTarget;

        protected EnemyMovementAgent movementAgent;
        protected EnemyPathfinder2D pathfinder;
        protected float repathTimer;

        public Vector2 AnchorPointD { get => anchorPointD; set => anchorPointD = value; }
        public float RadiusA => radiusA;
        public float RadiusB => radiusB;
        public float RadiusC => radiusC;
        public NormalEnemyState CurrentState => currentState;
        public Transform CurrentTarget => currentTarget;

        [ContextMenu("Lấy tọa độ hiện tại làm điểm D")]
        [ContextMenu("Set Current Position As Anchor D")]
        public void SetCurrentPositionAsAnchorD()
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, "Set Anchor Point D to Current Position");
#endif
            anchorPointD = transform.position;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            Debug.Log($"[{name}] Đã cập nhật điểm D thành tọa độ hiện tại: {anchorPointD}", this);
#endif
        }

        public override void Initialize(EnemyStats overrideStats = null)
        {
            base.Initialize(overrideStats);

            if (movementAgent == null) movementAgent = GetComponent<EnemyMovementAgent>();
            if (movementAgent == null) movementAgent = gameObject.AddComponent<EnemyMovementAgent>();
            if (movementAgent != null)
            {
                movementAgent.Initialize(Model, enemyLayer);
                if (Stats != null)
                {
                    movementAgent.SetSpeed(Stats.MoveSpeed);
                }
            }

            if (pathfinder == null)
            {
                pathfinder = new EnemyPathfinder2D(pathNodeSize, terrainLayer, enemyLayer, enemyAvoidancePenalty);
            }
        }

        protected override void Awake()
        {
            // Khởi tạo layer mặc định nếu chưa cấu hình
            if (terrainLayer.value == 0) terrainLayer = LayerMask.GetMask("Terrain");
            if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");
            if (tankLayer.value == 0) tankLayer = LayerMask.GetMask("Tank");

            // Tự động lấy vị trí xuất phát làm tâm D nếu được bật
            if (autoSetAnchorDFromSpawnPosition)
            {
                anchorPointD = transform.position;
            }

            if (movementAgent == null) movementAgent = GetComponent<EnemyMovementAgent>();
            if (movementAgent == null) movementAgent = gameObject.AddComponent<EnemyMovementAgent>();

            pathfinder = new EnemyPathfinder2D(pathNodeSize, terrainLayer, enemyLayer, enemyAvoidancePenalty);

            base.Awake();
        }

        protected virtual void Start()
        {
            if (movementAgent != null && Stats != null)
            {
                movementAgent.SetSpeed(Stats.MoveSpeed);
            }
        }

        protected override void OnBrainTick(float deltaTime)
        {
            if (movementAgent != null && Stats != null)
            {
                movementAgent.SetSpeed(Stats.MoveSpeed);
            }

            repathTimer -= deltaTime;

            // 1. Quét tìm và kiểm tra tính hợp lệ của mục tiêu
            currentTarget = ScanAndValidateTarget();

            Vector2 currentPos = transform.position;
            float distToHome = Vector2.Distance(currentPos, anchorPointD);

            // 2. Phân nhánh xử lý theo việc có mục tiêu hay không
            if (currentTarget != null)
            {
                HandleTargetTracking(currentPos, currentTarget.position);
            }
            else
            {
                HandleReturnHomeLogic(currentPos, distToHome, deltaTime);
            }
        }

        protected virtual void HandleTargetTracking(Vector2 currentPos, Vector2 targetPos)
        {
            awayFromHomeTimer = 0f;
            float distToTarget = Vector2.Distance(currentPos, targetPos);

            // TH1: Mục tiêu đã nằm trong bán kính A
            if (distToTarget <= radiusA)
            {
                currentState = NormalEnemyState.StandStillInB;
                movementAgent.Stop();
                return;
            }

            // TH2: Mục tiêu nằm ngoài bán kính B -> Tìm đường đưa mục tiêu vào trong bán kính A
            if (distToTarget > radiusB)
            {
                if (repathTimer <= 0f)
                {
                    repathTimer = repathInterval;
                    var path = pathfinder.FindPathToRadiusA(
                        currentPos,
                        targetPos,
                        radiusA,
                        anchorPointD,
                        radiusC,
                        Model != null ? Model.BodyCollider : null,
                        agentRadius: agentPathRadius
                    );

                    if (path != null)
                    {
                        currentState = NormalEnemyState.Chasing;
                        movementAgent.SetPath(path);
                    }
                    else
                    {
                        // Không tìm thấy đường đi hoàn toàn trong C -> Mất dấu mục tiêu
                        currentTarget = null;
                        movementAgent.Stop();
                    }
                }
                return;
            }

            // TH3: Mục tiêu nằm trong khoảng (A, B]
            // "Khi mục tiêu nằm trong phạm vi B nhưng không tìm thấy đường đi để mục tiêu nằm trong phạm vi A thì có thể dừng tìm đường và đứng yên."
            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                var path = pathfinder.FindPathToRadiusA(
                    currentPos,
                    targetPos,
                    radiusA,
                    anchorPointD,
                    radiusC,
                    Model != null ? Model.BodyCollider : null,
                    agentRadius: agentPathRadius
                );

                if (path != null)
                {
                    currentState = NormalEnemyState.Chasing;
                    movementAgent.SetPath(path);
                }
                else
                {
                    // Dừng tìm đường và đứng yên
                    currentState = NormalEnemyState.StandStillInB;
                    movementAgent.Stop();
                }
            }
        }

        protected virtual void HandleReturnHomeLogic(Vector2 currentPos, float distToHome, float deltaTime)
        {
            if (distToHome <= homeArrivalTolerance)
            {
                currentState = NormalEnemyState.IdleAtHome;
                awayFromHomeTimer = 0f;
                movementAgent.Stop();
                return;
            }

            awayFromHomeTimer += deltaTime;

            // Đã quá Y giây -> Dịch chuyển tức thời về D
            if (awayFromHomeTimer >= teleportHomeDelayY)
            {
                currentState = NormalEnemyState.TeleportingHome;
                movementAgent.Teleport(anchorPointD);
                awayFromHomeTimer = 0f;
                return;
            }

            // Đã quá X giây -> Tìm đường đi bộ quay về D
            if (awayFromHomeTimer >= returnHomeDelayX)
            {
                currentState = NormalEnemyState.ReturningHome;

                // Chỉ tính toán lại đường đi khi chưa di chuyển hoặc hết thời gian repath
                if (!movementAgent.IsMoving || repathTimer <= 0f)
                {
                    repathTimer = repathInterval;
                    var path = pathfinder.FindPathToPoint(
                        currentPos,
                        anchorPointD,
                        anchorPointD,
                        radiusC,
                        Model != null ? Model.BodyCollider : null,
                        stopDistance: homeStopDistance,
                        agentRadius: agentPathRadius
                    );

                    if (path != null && path.Count > 0)
                    {
                        movementAgent.SetPath(path);
                    }
                    else if (!movementAgent.IsMoving)
                    {
                        // Fallback đi thẳng nếu không có vật cản địa hình
                        movementAgent.SetPath(new List<Vector2> { currentPos, anchorPointD });
                    }
                }
                return;
            }

            // Chưa quá X giây -> Đứng yên chờ
            currentState = NormalEnemyState.WaitingToReturn;
            movementAgent.Stop();
        }

        /// <summary>
        /// Tìm mục tiêu thuộc layer "Tank", có ColliderOwnerRef và Owner là TankBrain (hoặc subclass),
        /// bảo đảm nằm trong đường tròn (D, C).
        /// </summary>
        protected virtual Transform ScanAndValidateTarget()
        {
            int mask = tankLayer.value != 0 ? tankLayer.value : LayerMask.GetMask("Tank");

            Collider2D[] hits = Physics2D.OverlapCircleAll(anchorPointD, radiusC, mask);
            if (hits == null || hits.Length == 0) return null;

            Vector2 currentPos = transform.position;
            Transform bestTarget = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;

                ColliderOwnerRef ownerRef = hit.GetComponent<ColliderOwnerRef>() ?? hit.GetComponentInParent<ColliderOwnerRef>();
                if (ownerRef == null) continue;

                TankBrain tankBrain = null;
                if (ownerRef.Owner is TankBrain tb)
                {
                    tankBrain = tb;
                }
                else if (ownerRef.Owner is GameObject go)
                {
                    tankBrain = go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                }
                else if (ownerRef.Owner is Component comp)
                {
                    tankBrain = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                }

                if (tankBrain == null)
                {
                    tankBrain = hit.GetComponentInParent<TankBrain>();
                }

                if (tankBrain == null) continue;
                if (!tankBrain.IsAlive) continue;

                Vector2 tankPos = tankBrain.transform.position;
                Vector2 closestPoint = hit.bounds.ClosestPoint(anchorPointD);
                if (Vector2.Distance(tankPos, anchorPointD) > radiusC && Vector2.Distance(closestPoint, anchorPointD) > radiusC)
                    continue;

                float dist = Vector2.Distance(currentPos, tankPos);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    bestTarget = tankBrain.transform;
                }
            }

            return bestTarget;
        }

        protected override void OnDeath()
        {
            if (movementAgent != null)
            {
                movementAgent.Stop();
            }
            Destroy(gameObject);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            radiusB = Mathf.Max(radiusA, radiusB);
            teleportHomeDelayY = Mathf.Max(returnHomeDelayX, teleportHomeDelayY);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            // Vùng lãnh thổ C quanh D (màu xanh Cyan)
            Gizmos.color = Color.cyan;
            Vector2 center = Application.isPlaying ? anchorPointD : (anchorPointD != Vector2.zero ? anchorPointD : (Vector2)transform.position);
            Gizmos.DrawWireSphere(center, radiusC);

            // Điểm cố định D
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, 0.3f);

            // Bán kính A và B quanh vị trí quái vật hiện tại
            Vector2 currentPos = transform.position;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentPos, radiusA);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentPos, radiusB);
        }
    }
}

