using IronWasteland;
using IronWasteland.Tanks;
using System;
using UnityEngine;

namespace IronWasteland.Tanks.Leviathan
{
    /// <summary>
    /// Kỹ năng Tùy Chọn 1 - Biến thể 2 của Leviathan: Guardian Current (Dòng Chảy Hộ Vệ).
    /// Kế thừa và triển khai <see cref="ITankShield"/>.
    /// Hiệu quả:
    /// - Liên kết dòng nước uốn lượn nối giữa Leviathan và xe tăng đồng minh.
    /// - Một phần sát thương nhận vào của đồng minh sẽ được chuyển hướng sang cho Leviathan gánh thay (thông qua cơ chế ITankShield).
    /// - Đồng thời hồi phục máu liên tục cho đồng minh trong suốt thời gian liên kết.
    /// - Liên kết sẽ tự động đứt nếu khoảng cách giữa hai xe vượt quá giới hạn tối đa hoặc khi một trong hai xe tử trận.
    /// Hiển thị trực quan:
    /// - Dòng nước màu xanh trong suốt uốn lượn nối giữa 2 xe tăng (VisualCurrent).
    /// - Bong bóng nước hồi phục bao quanh xe tăng đồng minh (VisualHealingBubbles).
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionalSkill1_2_Leviathan : MonoBehaviour, ITankShield
    {
        public event Action<TankBrain> OnLinkStarted;
        public event Action<TankBrain> OnLinkEnded;
        public event Action<float> OnDamageShared;

        [Header("Mục Tiêu & Khoảng Cách")]
        [Tooltip("Bán kính tìm kiếm xe tăng đồng minh xung quanh vị trí con trỏ chuột (đơn vị).")]
        [SerializeField, Min(0.1f)] private float selectRadius = 1.5f;

        [Tooltip("Thời gian duy trì tối đa của dòng chảy liên kết (giây).")]
        [SerializeField, Min(0.1f)] private float duration = 8.0f;

        [Tooltip("Khoảng cách tối đa giữa Leviathan và đồng minh trước khi dòng nước bị đứt (đơn vị).")]
        [SerializeField, Min(1.0f)] private float maxBreakDistance = 10.0f;

        [Header("Chỉ Số Hỗ Trợ (Combat)")]
        [Tooltip("Tỷ lệ sát thương của đồng minh chuyển sang cho Leviathan gánh thay (0.35 = gánh 35% sát thương).")]
        [SerializeField, Range(0f, 1f)] private float damageShareRatio = 0.35f;

        [Tooltip("Lượng máu hồi phục liên tục mỗi giây cho đồng minh.")]
        [SerializeField, Min(0f)] private float healPerSecond = 15.0f;

        [Header("Hình Ảnh & Hiệu Ứng (Visuals)")]
        [Tooltip("GameObject hiển thị dòng nước nối 2 xe (VisualCurrent).")]
        [SerializeField] private GameObject visualCurrent;

        [Tooltip("GameObject hiển thị bong bóng hồi phục bao quanh đồng minh (VisualHealingBubbles).")]
        [SerializeField] private GameObject visualHealingBubbles;

        [Tooltip("Độ dày (scale trục Y) của dòng nước nối 2 xe.")]
        [SerializeField, Min(0.1f)] private float streamThickness = 1.0f;

        [Tooltip("Tốc độ xoay của bong bóng hồi phục quanh đồng minh (độ/giây).")]
        [SerializeField] private float bubbleRotationSpeed = 30.0f;

        [Tooltip("Biên độ nhấp nhô kích thước bong bóng hồi phục (0 = không nhấp nhô, 0.05 = 5%).")]
        [SerializeField, Range(0f, 0.3f)] private float bubblePulseScale = 0.05f;

        [Tooltip("Tần số nhấp nhô bong bóng hồi phục (chu kỳ/giây).")]
        [SerializeField, Min(0.1f)] private float bubblePulseSpeed = 2.0f;

        // Trạng thái nội bộ (Runtime State)
        private TankBrain ownerTank;
        private TankBrain allyTank;
        private bool isLinked = false;
        private float remainingTime = 0f;

        private SpriteRenderer currentRenderer;
        private Vector3 bubbleInitialScale = Vector3.one;

        // Triển khai ITankShield
        bool ITankShield.IsActive => isLinked && remainingTime > 0f && ownerTank != null && ownerTank.IsAlive;

        // Thuộc tính công khai (Read-only Getters & UI)
        public bool IsActive => isLinked && remainingTime > 0f;
        public float RemainingTime => remainingTime;
        public float Duration => duration;
        public TankBrain TargetTank => allyTank;
        public TankBrain OwnerTank => ownerTank;
        public float DamageShareRatio => damageShareRatio;
        public float HealPerSecond => healPerSecond;

        private void Awake()
        {
            if (visualCurrent != null)
            {
                currentRenderer = visualCurrent.GetComponent<SpriteRenderer>();
                if (currentRenderer == null)
                {
                    currentRenderer = visualCurrent.GetComponentInChildren<SpriteRenderer>();
                }
                visualCurrent.SetActive(false);
            }

            if (visualHealingBubbles != null)
            {
                bubbleInitialScale = visualHealingBubbles.transform.localScale;
                visualHealingBubbles.SetActive(false);
            }

            // Tắt SpriteRenderer trực tiếp trên GameObject gốc nếu có
            SpriteRenderer selfSr = GetComponent<SpriteRenderer>();
            if (selfSr != null)
            {
                selfSr.enabled = false;
            }
        }

        /// <summary>
        /// Kích hoạt liên kết dòng nước với đồng minh tại vị trí ngắm của xe.
        /// </summary>
        public bool Initialize(TankBrain owner)
        {
            return owner != null && Initialize(owner, owner.LookTarget);
        }

        /// <summary>
        /// Kích hoạt liên kết dòng nước với đồng minh tại vị trí chỉ định trong thế giới.
        /// </summary>
        public bool Initialize(TankBrain owner, Vector2 targetWorldPos, float healRate = -1f, float shareRatio = -1f, float dur = -1f)
        {
            if (owner == null) return false;
            this.ownerTank = owner;

            if (healRate >= 0f) this.healPerSecond = healRate;
            if (shareRatio >= 0f) this.damageShareRatio = shareRatio;
            if (dur >= 0f) this.duration = dur;

            if (targetWorldPos == Vector2.zero && owner.HasLookTarget)
            {
                targetWorldPos = owner.LookTarget;
            }

            if (!TryFindAllyAt(targetWorldPos, out TankBrain foundAlly))
            {
                return false;
            }

            // Nếu đang liên kết với xe khác thì ngắt liên kết cũ trước
            if (isLinked)
            {
                BreakLink();
            }

            this.allyTank = foundAlly;
            this.remainingTime = duration;
            this.isLinked = true;

            // Đăng ký lá chắn chia sẻ sát thương lên xe tăng đồng minh
            allyTank.AddShield(this);

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            SetVisualsActive(true);
            UpdateVisuals();

            OnLinkStarted?.Invoke(allyTank);
            return true;
        }

        /// <summary>
        /// Tìm kiếm xe tăng đồng minh còn sống xung quanh tọa độ chỉ định bằng ColliderOwnerRef thuần túy.
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

            // Kiểm tra trạng thái sống của hai xe
            if (ownerTank == null || !ownerTank.IsAlive || allyTank == null || !allyTank.IsAlive)
            {
                BreakLink();
                return;
            }

            // Kiểm tra khoảng cách giữa hai xe (đứt nếu vượt quá maxBreakDistance)
            float currentDistance = Vector2.Distance(ownerTank.transform.position, allyTank.transform.position);
            if (currentDistance > maxBreakDistance)
            {
                BreakLink();
                return;
            }

            // Hồi phục máu liên tục cho đồng minh
            if (healPerSecond > 0f)
            {
                allyTank.Heal(healPerSecond * Time.deltaTime);
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
            if (isLinked)
            {
                UpdateVisuals();
            }
        }

        /// <summary>
        /// Cập nhật vị trí và kích thước dòng nước nối 2 xe và bong bóng bao quanh đồng minh.
        /// </summary>
        private void UpdateVisuals()
        {
            if (ownerTank == null || allyTank == null) return;

            Vector3 start = ownerTank.transform.position;
            Vector3 end = allyTank.transform.position;

            // 1. Cập nhật dòng nước uốn lượn nối giữa hai xe tăng
            if (visualCurrent != null)
            {
                Vector3 diff = end - start;
                float dist = diff.magnitude;

                if (dist > 0.01f)
                {
                    // Đặt vị trí tại trung điểm nối 2 xe vì pivot của sprite asset_guardian_current nằm ở tâm
                    visualCurrent.transform.position = (start + end) * 0.5f;
                    visualCurrent.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);

                    float baseWidth = 12.4f;
                    Sprite currentSprite = currentRenderer != null ? currentRenderer.sprite : null;
                    if (currentSprite != null && currentSprite.rect.width > 0f)
                    {
                        baseWidth = currentSprite.rect.width / currentSprite.pixelsPerUnit;
                    }

                    visualCurrent.transform.localScale = new Vector3(
                        dist / Mathf.Max(0.01f, baseWidth),
                        streamThickness,
                        1f
                    );
                }
            }

            // 2. Cập nhật vị trí, xoay và nhấp nhô nhẹ của bong bóng hồi phục quanh đồng minh
            if (visualHealingBubbles != null)
            {
                visualHealingBubbles.transform.position = end;

                if (bubbleRotationSpeed != 0f)
                {
                    visualHealingBubbles.transform.Rotate(0f, 0f, bubbleRotationSpeed * Time.deltaTime);
                }

                if (bubblePulseScale > 0f)
                {
                    float pulse = 1f + Mathf.Sin(Time.time * bubblePulseSpeed * Mathf.PI * 2f) * bubblePulseScale;
                    visualHealingBubbles.transform.localScale = bubbleInitialScale * pulse;
                }
            }
        }

        private void SetVisualsActive(bool isActive)
        {
            if (visualCurrent != null) visualCurrent.SetActive(isActive);
            if (visualHealingBubbles != null) visualHealingBubbles.SetActive(isActive);
        }

        #region Triển Khai ITankShield (Cơ Chế Gánh Sát Thương Hộ)
        /// <summary>
        /// Can thiệp vào sát thương đồng minh nhận vào:
        /// Chuyển hướng phần trăm sát thương (<see cref="damageShareRatio"/>) sang cho Leviathan gánh thay.
        /// </summary>
        public bool TryAbsorbDamage(ref float incomingDamage)
        {
            if (!isLinked || incomingDamage <= 0f)
            {
                return false;
            }

            // Tính toán lượng sát thương chia sẻ
            float sharedAmount = incomingDamage * damageShareRatio;
            incomingDamage -= sharedAmount; // Đồng minh chỉ nhận phần còn lại

            // Leviathan gánh chịu phần sát thương này
            if (ownerTank != null && ownerTank.IsAlive)
            {
                ownerTank.TakeDamage(sharedAmount);
                TextDamageManager.Instance?.Show(sharedAmount, ownerTank.transform.position);
                OnDamageShared?.Invoke(sharedAmount);
            }

            return incomingDamage <= 0f;
        }

        /// <summary>
        /// Phá vỡ hoặc gỡ bỏ khiên liên kết.
        /// </summary>
        public void BreakShield()
        {
            BreakLink();
        }
        #endregion

        /// <summary>
        /// Ngắt dòng chảy liên kết, gỡ khiên chia sẻ sát thương khỏi đồng minh và ẩn hình ảnh.
        /// </summary>
        public void BreakLink()
        {
            if (!isLinked) return;

            isLinked = false;
            remainingTime = 0f;

            if (allyTank != null)
            {
                allyTank.RemoveShield(this);
            }

            if (visualHealingBubbles != null)
            {
                visualHealingBubbles.transform.localScale = bubbleInitialScale;
                visualHealingBubbles.transform.rotation = Quaternion.identity;
            }

            SetVisualsActive(false);

            TankBrain prevAlly = allyTank;
            allyTank = null;

            OnLinkEnded?.Invoke(prevAlly);
        }

        public void Clear()
        {
            BreakLink();
            ownerTank = null;
            OnLinkStarted = null;
            OnLinkEnded = null;
            OnDamageShared = null;
        }

        private void OnDisable()
        {
            BreakLink();
        }

        private void OnDestroy()
        {
            BreakLink();
        }

        private void OnValidate()
        {
            selectRadius = Mathf.Max(0.1f, selectRadius);
            duration = Mathf.Max(0.1f, duration);
            maxBreakDistance = Mathf.Max(1.0f, maxBreakDistance);
            damageShareRatio = Mathf.Clamp01(damageShareRatio);
            healPerSecond = Mathf.Max(0f, healPerSecond);
            streamThickness = Mathf.Max(0.1f, streamThickness);
            bubblePulseScale = Mathf.Clamp(bubblePulseScale, 0f, 0.3f);
            bubblePulseSpeed = Mathf.Max(0.1f, bubblePulseSpeed);
        }
    }
}
