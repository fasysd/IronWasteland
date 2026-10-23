using IronWasteland;
using IronWasteland.Tanks;
using System;
using UnityEngine;

namespace IronWasteland.Tanks.BlackShadow
{
    /// <summary>
    /// Kỹ năng Tùy Chọn 1 - Biến thể 2: Shadow Link (Dải Liên Kết Bóng Tối).
    /// Tạo liên kết bóng tối với một đồng minh (TankBrain khác bản thân), giúp tăng tốc độ di chuyển
    /// cho cả hai đồng thời giảm thời gian hồi kỹ năng thường cho đồng minh đó.
    /// Hiển thị dải xích nối 2 xe và khói đen dưới thân mỗi xe.
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionalSkill1_2 : MonoBehaviour
    {
        public event Action<TankBrain> OnLinkStarted;
        public event Action<TankBrain> OnLinkEnded;

        [Header("Mục Tiêu & Thời Gian")]
        [Tooltip("Bán kính tìm kiếm đồng minh xung quanh vị trí con trỏ chuột.")]
        [SerializeField, Min(0.1f)] private float selectRadius = 1.0f;

        [Tooltip("Thời gian duy trì dải liên kết bóng tối (giây).")]
        [SerializeField, Min(0.1f)] private float duration = 8f;

        [Header("Chỉ Số Buff (Stats)")]
        [Tooltip("Lượng tốc độ di chuyển tăng thêm cho cả Black Shadow và đồng minh.")]
        [SerializeField, Min(0f)] private float moveSpeedBonus = 2.0f;

        [Tooltip("Phần trăm giảm thời gian hồi chiêu kỹ năng thường cho đồng minh (0.3 = giảm 30%).")]
        [SerializeField, Range(0f, 1f)] private float cooldownReductionBonus = 0.3f;

        [Header("Hình Ảnh (Visuals)")]
        [Tooltip("Kéo thả GameObject hiển thị dải xích bóng tối vào đây (VisualChain).")]
        [SerializeField] private GameObject visualChain;

        [Tooltip("Kéo thả GameObject hiển thị khói bóng tối dưới thân xe Black Shadow vào đây (VisualBurst).")]
        [SerializeField] private GameObject visualBurst;

        [Tooltip("Kéo thả GameObject hiển thị khói bóng tối dưới thân xe đồng minh (để trống sẽ tự nhân bản từ visualBurst).")]
        [SerializeField] private GameObject visualBurstAlly;

        // Trạng thái nội bộ (Runtime State)
        private TankBrain ownerTank;
        private TankBrain allyTank;
        private bool isLinked = false;
        private float remainingTime = 0f;
        private SpriteRenderer chainRenderer;
        private Vector3 chainInitialScale = Vector3.one;

        private const string SelfBonusKey = "ShadowLink_Self";
        private const string AllyBonusKey = "ShadowLink_Ally";

        // Thuộc tính công khai (Read-only Getters & UI)
        public bool IsActive => isLinked && remainingTime > 0f;
        public float RemainingTime => remainingTime;
        public float Duration => duration;
        public TankBrain TargetTank => allyTank;
        public TankBrain OwnerTank => ownerTank;

        private void Awake()
        {
            if (visualChain != null)
            {
                chainRenderer = visualChain.GetComponent<SpriteRenderer>();
                chainInitialScale = visualChain.transform.localScale;
                visualChain.SetActive(false);
            }

            // Tắt SpriteRenderer gắn sẵn trên GameObject này (nếu có) để tránh hiện xích thừa trên xe
            SpriteRenderer selfSr = GetComponent<SpriteRenderer>();
            if (selfSr != null)
            {
                selfSr.enabled = false;
            }

            if (visualBurst != null)
            {
                visualBurst.SetActive(false);

                if (visualBurstAlly == null && transform.parent != null)
                {
                    visualBurstAlly = Instantiate(visualBurst, transform.parent);
                    visualBurstAlly.name = $"{visualBurst.name}_Ally";
                }
                if (visualBurstAlly != null)
                {
                    visualBurstAlly.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Kích hoạt liên kết bóng tối với đồng minh tại vị trí ngắm của xe.
        /// </summary>
        public bool Initialize(TankBrain owner)
        {
            return owner != null && Initialize(owner, owner.LookTarget);
        }

        /// <summary>
        /// Kiểm tra tại vị trí chuột xem có phải TankBrain và không phải bản thân hay không.
        /// Nếu thỏa mãn: tăng tốc độ cho cả hai, giảm hồi chiêu cho đồng minh và bật visual.
        /// </summary>
        public bool Initialize(TankBrain owner, Vector2 targetWorldPos)
        {
            if (owner == null) return false;

            // Dọn dẹp trạng thái của lần kích hoạt trước trước khi áp cấu hình mới
            Clear();

            this.ownerTank = owner;

            if (targetWorldPos == Vector2.zero && owner.HasLookTarget)
            {
                targetWorldPos = owner.LookTarget;
            }

            if (!TryFindAllyAt(targetWorldPos, out TankBrain foundAlly))
            {
                return false;
            }

            this.allyTank = foundAlly;
            this.remainingTime = duration;
            this.isLinked = true;

            // Tăng tốc độ di chuyển cho Black Shadow
            TankStats selfBonus = TankStats.Zeroed();
            selfBonus.MoveSpeed = moveSpeedBonus;
            ownerTank.AddBonusStats(SelfBonusKey, selfBonus);

            // Tăng tốc độ di chuyển + giảm hồi chiêu kỹ năng thường cho đồng minh
            TankStats allyBonus = TankStats.Zeroed();
            allyBonus.MoveSpeed = moveSpeedBonus;
            allyBonus.CooldownReduction = cooldownReductionBonus;
            allyTank.AddBonusStats(AllyBonusKey, allyBonus);

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            SetVisualsActive(true);
            UpdateVisuals();

            OnLinkStarted?.Invoke(allyTank);
            return true;
        }

        /// <summary>
        /// Tìm kiếm tất cả các collider xung quanh vị trí chuột.
        /// Nếu có collider nào thuộc về xe tăng đồng minh (khác owner và còn sống) thì chọn xe đó.
        /// </summary>
        public bool TryFindAllyAt(Vector2 targetPos, TankBrain owner, out TankBrain ally)
        {
            ally = null;
            Collider2D[] colliders = Physics2D.OverlapCircleAll(targetPos, selectRadius);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null) continue;

                ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(col);
                if (targetRef != null && targetRef.TryGetOwner<TankBrain>(out var target))
                {
                    if (target != owner && target.IsAlive)
                    {
                        ally = target;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tìm kiếm xe tăng đồng minh xung quanh vị trí chỉ định đối với ownerTank hiện tại.
        /// </summary>
        public bool TryFindAllyAt(Vector2 targetPos, out TankBrain ally)
        {
            return TryFindAllyAt(targetPos, ownerTank, out ally);
        }

        private void Update()
        {
            if (!isLinked) return;

            // Nếu một trong hai xe tăng bị hủy hoặc chết -> Ngắt liên kết
            if (ownerTank == null || !ownerTank.IsAlive || allyTank == null || !allyTank.IsAlive)
            {
                BreakLink();
                return;
            }

            // Đếm ngược thời gian duy trì liên kết
            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                BreakLink();
            }
        }

        private void LateUpdate()
        {
            if (isLinked) UpdateVisuals();
        }

        /// <summary>
        /// Cập nhật vị trí sợi xích nối từ xe Black Shadow sang xe đồng minh và khói đen dưới thân mỗi xe.
        /// </summary>
        private void UpdateVisuals()
        {
            if (ownerTank == null || allyTank == null) return;

            Vector3 start = ownerTank.transform.position;
            Vector3 end = allyTank.transform.position;

            // 1. Cập nhật sợi xích nối 2 xe
            if (visualChain != null)
            {
                Vector3 diff = end - start;
                float dist = diff.magnitude;

                if (dist > 0.01f)
                {
                    // Vì pivot của sprite asset_shadow_chain nằm ở đầu bên trái (x = 33.38px),
                    // nên đặt vị trí của visualChain ngay tại start để sợi xích bắt đầu từ xe Black Shadow vươn tới đồng minh
                    visualChain.transform.position = start;
                    visualChain.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);

                    // Chiều dài thực tế từ pivot tới đuôi xích
                    float baseLength = 9.9f;
                    Sprite sprite = chainRenderer != null ? chainRenderer.sprite : null;
                    if (sprite != null && sprite.rect.width > 0f)
                    {
                        baseLength = (sprite.rect.width - sprite.pivot.x) / sprite.pixelsPerUnit;
                    }

                    visualChain.transform.localScale = new Vector3(dist / Mathf.Max(0.01f, baseLength), 1f, 1f);
                }
            }

            // 2. Cập nhật vị trí khói đen dưới thân mỗi xe
            if (visualBurst != null) visualBurst.transform.position = start;
            if (visualBurstAlly != null) visualBurstAlly.transform.position = end;
        }

        private void SetVisualsActive(bool isActive)
        {
            if (visualChain != null) visualChain.SetActive(isActive);
            if (visualBurst != null) visualBurst.SetActive(isActive);
            if (visualBurstAlly != null) visualBurstAlly.SetActive(isActive);
        }

        /// <summary>
        /// Đặt lại toàn bộ trạng thái runtime của liên kết bóng tối về mặc định.
        /// CHỈ reset state: không phát event, không tắt GameObject, không ghi đè trường cấu hình.
        /// Trả về true nếu đang thực sự có liên kết mở và vừa được ngắt; prevAlly là đồng minh vừa bị ngắt.
        /// </summary>
        private bool ResetRuntimeState(out TankBrain prevAlly)
        {
            prevAlly = null;

            if (!isLinked)
            {
                return false;
            }

            isLinked = false;
            remainingTime = 0f;

            if (ownerTank != null) ownerTank.RemoveBonusStats(SelfBonusKey);
            if (allyTank != null) allyTank.RemoveBonusStats(AllyBonusKey);

            SetVisualsActive(false);

            // Trả dải xích về kích thước gốc đã cache trong Awake
            if (visualChain != null)
            {
                visualChain.transform.localScale = chainInitialScale;
            }

            prevAlly = allyTank;
            allyTank = null;
            return true;
        }

        /// <summary>
        /// Ngắt liên kết bóng tối, hoàn trả toàn bộ chỉ số buff và ẩn hình ảnh.
        /// </summary>
        public void BreakLink()
        {
            if (ResetRuntimeState(out TankBrain prevAlly))
            {
                OnLinkEnded?.Invoke(prevAlly);
            }
        }

        /// <summary>
        /// Dọn dẹp để tái sử dụng: reset state và gỡ owner.
        /// Không phát event, không tắt GameObject, KHÔNG xoá subscriber —
        /// người nghe tự gỡ đăng ký của mình trong OnDisable/OnDestroy.
        /// Initialize gọi hàm này trước tiên.
        /// </summary>
        public void Clear()
        {
            ResetRuntimeState(out _);
            ownerTank = null;
        }

        private void OnDisable()
        {
            BreakLink();
        }

        private void OnDestroy()
        {
            BreakLink();
        }
    }
}
