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
            return EnemyTargetUtility.ScanNearestTarget(transform.position, anchorPointD, radiusC, tankLayer.value);
        }

        protected override void OnDeath()
        {
            IronWasteland.Net.NetEntity.Despawn(gameObject);
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

