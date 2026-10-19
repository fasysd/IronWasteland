using IronWasteland.Tanks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Lớp cơ sở cho các Normal Enemy luôn đứng yên (như Nấm Độc Gai Thạch, Sao Đen).
    /// KHÔNG có logic tìm đường hay di chuyển, CHỈ CÓ logic phát hiện mục tiêu trong bán kính C quanh điểm D.
    /// </summary>
    public abstract class NormalEnemy_BaseStationary : NormalEnemyBrain
    {
        [Header("Territory Settings (Điểm D và Vùng C)")]
        [Tooltip("Điểm cố định D trên World Space.")]
        [ContextMenuItem("Lấy tọa độ hiện tại làm điểm D", nameof(SetCurrentPositionAsAnchorD))]
        [SerializeField] protected Vector2 anchorPointD;

        [Tooltip("Nếu bật, tự động lấy tọa độ vị trí Transform khi Awake làm điểm D. Nếu tắt, sử dụng chính xác tọa độ anchorPointD cấu hình trong Inspector.")]
        [SerializeField] protected bool autoSetAnchorDFromSpawnPosition = false;

        [Tooltip("Bán kính C quanh điểm cố định D để phát hiện mục tiêu.")]
        [SerializeField, Min(0.5f)] protected float radiusC = 10f;

        [Header("Layers")]
        [SerializeField] protected LayerMask tankLayer;

        [Header("Runtime State")]
        [SerializeField, ReadOnly] protected Transform currentTarget;

        public Vector2 AnchorPointD { get => anchorPointD; set => anchorPointD = value; }
        public float RadiusC => radiusC;
        public Transform CurrentTarget => currentTarget;
        public bool HasTarget => currentTarget != null;

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

        protected override void Awake()
        {
            if (tankLayer.value == 0) tankLayer = LayerMask.GetMask("Tank");

            if (autoSetAnchorDFromSpawnPosition)
            {
                anchorPointD = transform.position;
            }

            base.Awake();
        }

        protected override void OnBrainTick(float deltaTime)
        {
            // Quái đứng yên chỉ quét và phát hiện mục tiêu trong bán kính C quanh điểm D
            currentTarget = ScanAndValidateTarget();

            // Luôn đảm bảo đứng yên (Idle)
            Model?.SetMoveSpeed(0f);
        }

        /// <summary>
        /// Quét mục tiêu thuộc layer Tank có ColliderOwnerRef là TankBrain (hoặc subclass) trong bán kính C quanh D.
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
            Destroy(gameObject);
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
        }
    }
}

