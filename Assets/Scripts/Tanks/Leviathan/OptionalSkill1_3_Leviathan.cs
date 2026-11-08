using IronWasteland;
using IronWasteland.Tanks;
using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.Leviathan
{
    /// <summary>
    /// Kỹ năng Tùy Chọn 1 - Biến thể 3 của Leviathan: Regenerative Shell (Kén Nước Trị Liệu).
    /// Hiệu quả:
    /// - Bao phủ Leviathan bằng một kén nước xanh ngọc bích liên tục hồi phục sinh lực.
    /// - Trong suốt thời gian kén nước duy trì (duration), nếu Leviathan không nhận bất kỳ sát thương nào,
    ///   khi kén hoàn tất sẽ bùng nổ ánh sáng trị liệu và hồi phục thêm một lượng máu cực lớn (Bonus Completion Heal).
    /// - Nếu bị dính sát thương trong lúc kén duy trì, hiệu ứng thưởng hoàn hảo bị mất nhưng lượng hồi máu cơ bản vẫn tiếp tục cho đến hết thời gian.
    /// Hiển thị trực quan:
    /// - Kén nước xanh ngọc bích bao trọn lấy xe tăng (VisualShell).
    /// - Hiệu ứng ánh sáng trị liệu bùng nổ khi nhận thưởng hồi phục hoàn hảo (VisualBurst).
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionalSkill1_3_Leviathan : MonoBehaviour
    {
        public event Action<bool> OnShellStateChange;
        public event Action<float> OnBonusHealTriggered;
        public event Action OnDamagedDuringShell;

        [Header("Thời Gian & Hồi Phục")]
        [Tooltip("Thời gian duy trì của kén nước trị liệu (giây).")]
        [SerializeField, Min(0.5f)] private float duration = 5.0f;

        [Tooltip("Giãn cách giữa hai nhịp hồi máu trong thời gian kén duy trì (giây).")]
        [SerializeField, Min(0.05f)] private float healInterval = 0.5f;

        [Tooltip("Lượng máu hồi mỗi nhịp theo Giới hạn máu của chính Leviathan (0.04 = 4% máu tối đa).")]
        [SerializeField, Range(0f, 1f)] private float healMaxHealthRatio = 0.04f;

        [Tooltip("Lượng máu hồi phục thưởng thêm khi kén hoàn tất mà không nhận bất kỳ sát thương nào, theo Giới hạn máu của chính Leviathan (0.2 = 20% máu tối đa).")]
        [SerializeField, Range(0f, 1f)] private float bonusCompletionHealMaxHealthRatio = 0.2f;

        [Header("Hình Ảnh & Hiệu Ứng (Visuals)")]
        [Tooltip("GameObject hiển thị kén nước xanh ngọc bích bao trọn xe tăng (VisualShell).")]
        [SerializeField] private GameObject visualShell;

        [Tooltip("GameObject hiển thị hiệu ứng ánh sáng bùng nổ khi nhận thưởng hồi phục hoàn hảo (VisualBurst).")]
        [SerializeField] private GameObject visualBurst;

        [Tooltip("Biên độ co giãn nhấp nhô nhẹ của kén nước (0 = không nhấp nhô, 0.04 = 4%).")]
        [SerializeField, Range(0f, 0.2f)] private float shellPulseScale = 0.04f;

        [Tooltip("Tần số nhấp nhô của kén nước (chu kỳ/giây).")]
        [SerializeField, Min(0.1f)] private float shellPulseSpeed = 1.8f;

        [Tooltip("Thời gian hiển thị hiệu ứng bùng nổ trị liệu khi hoàn tất hoàn hảo (giây).")]
        [SerializeField, Min(0.05f)] private float burstDuration = 0.4f;

        // Trạng thái nội bộ (Runtime State)
        private TankBrain ownerTank;
        private bool isShellActive = false;
        private float remainingTime = 0f;
        private float healTimer = 0f;
        private bool hasTakenDamage = false;

        private SpriteRenderer shellRenderer;
        private SpriteRenderer burstRenderer;

        private Vector3 shellInitialScale = Vector3.one;
        private Vector3 burstInitialScale = Vector3.one;
        private Color shellOriginalColor = Color.white;
        private Color burstOriginalColor = Color.white;

        private Coroutine burstCoroutine;

        // Thuộc tính công khai (Read-only Getters & UI)
        public bool IsActive => isShellActive && remainingTime > 0f;
        public float RemainingTime => remainingTime;
        public float Duration => duration;
        public bool HasTakenDamage => hasTakenDamage;
        public float HealInterval => healInterval;
        public float HealMaxHealthRatio => healMaxHealthRatio;
        public float BonusCompletionHealMaxHealthRatio => bonusCompletionHealMaxHealthRatio;
        public TankBrain OwnerTank => ownerTank;

        private void Awake()
        {
            // Tắt SpriteRenderer thừa trên chính GameObject này (nếu có) để tránh hiện kén tĩnh không bao giờ tắt
            SpriteRenderer selfSr = GetComponent<SpriteRenderer>();
            if (selfSr != null)
            {
                selfSr.enabled = false;
            }

            if (visualShell != null)
            {
                shellRenderer = visualShell.GetComponent<SpriteRenderer>();
                if (shellRenderer == null)
                {
                    shellRenderer = visualShell.GetComponentInChildren<SpriteRenderer>();
                }

                shellInitialScale = visualShell.transform.localScale;
                if (shellRenderer != null)
                {
                    shellOriginalColor = shellRenderer.color;
                }

                visualShell.SetActive(false);
            }

            if (visualBurst != null)
            {
                burstRenderer = visualBurst.GetComponent<SpriteRenderer>();
                if (burstRenderer == null)
                {
                    burstRenderer = visualBurst.GetComponentInChildren<SpriteRenderer>();
                }

                burstInitialScale = visualBurst.transform.localScale;
                if (burstRenderer != null)
                {
                    burstOriginalColor = burstRenderer.color;
                }

                visualBurst.SetActive(false);
            }
        }

        /// <summary>
        /// Kích hoạt kén nước trị liệu cho xe tăng Leviathan.
        /// </summary>
        public void Initialize(TankBrain owner, float healTickInterval = -1f, float healRatio = -1f, float bonusHealRatio = -1f, float dur = -1f)
        {
            if (owner == null) return;

            // Dọn dẹp trạng thái của lần kích hoạt trước trước khi áp cấu hình mới
            Clear();

            this.ownerTank = owner;

            if (healTickInterval >= 0f) this.healInterval = healTickInterval;
            if (healRatio >= 0f) this.healMaxHealthRatio = healRatio;
            if (bonusHealRatio >= 0f) this.bonusCompletionHealMaxHealthRatio = bonusHealRatio;
            if (dur >= 0f) this.duration = dur;

            remainingTime = duration;
            isShellActive = true;
            hasTakenDamage = false;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // Thiết lập vị trí và hiển thị kén nước
            if (visualShell != null)
            {
                visualShell.transform.position = ownerTank.transform.position;
                visualShell.transform.localScale = shellInitialScale;
                if (shellRenderer != null)
                {
                    shellRenderer.color = shellOriginalColor;
                }
                visualShell.SetActive(true);
            }

            if (visualBurst != null)
            {
                visualBurst.SetActive(false);
            }

            OnShellStateChange?.Invoke(true);
        }

        /// <summary>
        /// Được gọi khi xe tăng Leviathan nhận sát thương trong lúc kén nước đang hoạt động.
        /// Đánh dấu đã nhận sát thương, làm mất điều kiện nhận thưởng hoàn hảo khi hết giờ.
        /// </summary>
        public void OnOwnerDamaged(float damageAmount)
        {
            if (!isShellActive || damageAmount <= 0f) return;

            if (!hasTakenDamage)
            {
                hasTakenDamage = true;
                OnDamagedDuringShell?.Invoke();

                // Giảm nhẹ độ sáng/độ trong suốt của kén để báo hiệu đã mất trạng thái hoàn hảo
                if (shellRenderer != null)
                {
                    Color dimmedColor = shellOriginalColor;
                    dimmedColor.a *= 0.65f;
                    dimmedColor.r = Mathf.Max(0f, dimmedColor.r - 0.15f);
                    shellRenderer.color = dimmedColor;
                }
            }
        }

        private void Update()
        {
            if (!isShellActive) return;

            // Nếu xe tăng tử trận -> Hủy kén ngay lập tức
            if (ownerTank == null || !ownerTank.IsAlive)
            {
                CancelShell();
                return;
            }

            // Hồi máu theo nhịp: mỗi healInterval giây hồi một lượng theo Giới hạn máu của Leviathan
            float healAmount = ownerTank.Stats != null ? ownerTank.Stats.MaxHealth * healMaxHealthRatio : 0f;
            if (healAmount > 0f)
            {
                float interval = Mathf.Max(0.05f, healInterval);
                healTimer += Time.deltaTime;
                while (healTimer >= interval)
                {
                    healTimer -= interval;
                    ownerTank.Heal(healAmount);
                }
            }

            // Đếm ngược thời gian duy trì
            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                CompleteShell();
            }
        }

        private void LateUpdate()
        {
            if (!isShellActive || ownerTank == null) return;

            Vector3 tankPos = ownerTank.transform.position;

            // Đồng bộ vị trí kén nước theo thân xe và nhấp nhô nhẹ
            if (visualShell != null)
            {
                visualShell.transform.position = tankPos;

                if (shellPulseScale > 0f)
                {
                    float pulse = 1f + Mathf.Sin(Time.time * shellPulseSpeed * Mathf.PI * 2f) * shellPulseScale;
                    visualShell.transform.localScale = shellInitialScale * pulse;
                }
            }
        }

        /// <summary>
        /// Hoàn tất thời gian duy trì kén nước.
        /// Nếu không nhận bất kỳ sát thương nào trong suốt thời gian duy trì -> kích hoạt hồi máu thưởng cực lớn.
        /// </summary>
        private void CompleteShell()
        {
            if (!isShellActive) return;

            isShellActive = false;
            remainingTime = 0f;
            healTimer = 0f;

            // Tắt ngay kén nước
            if (visualShell != null)
            {
                visualShell.SetActive(false);
            }

            bool triggeredBurst = false;

            // Nếu không bị nhận sát thương trong suốt thời gian duy trì -> nhận thưởng hồi phục mạnh mẽ
            float bonusHeal = ownerTank != null && ownerTank.Stats != null
                ? ownerTank.Stats.MaxHealth * bonusCompletionHealMaxHealthRatio
                : 0f;
            if (!hasTakenDamage && bonusHeal > 0f && ownerTank != null && ownerTank.IsAlive)
            {
                ownerTank.Heal(bonusHeal);
                OnBonusHealTriggered?.Invoke(bonusHeal);

                // Kích hoạt hiệu ứng ánh sáng bùng nổ
                if (visualBurst != null)
                {
                    visualBurst.transform.position = ownerTank.transform.position;
                    visualBurst.SetActive(true);

                    if (burstCoroutine != null)
                    {
                        StopCoroutine(burstCoroutine);
                    }
                    burstCoroutine = StartCoroutine(AnimateBurst());
                    triggeredBurst = true;
                }
            }

            OnShellStateChange?.Invoke(false);

            // Nếu không có burst chạy thì tắt luôn component
            if (!triggeredBurst)
            {
                gameObject.SetActive(false);
            }
        }

        private IEnumerator AnimateBurst()
        {
            if (visualBurst == null) yield break;

            Vector3 startScale = burstInitialScale;
            Vector3 targetScale = burstInitialScale * 1.35f;

            float elapsed = 0f;
            while (elapsed < burstDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / burstDuration);

                visualBurst.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

                if (burstRenderer != null)
                {
                    Color c = burstOriginalColor;
                    c.a = Mathf.Lerp(burstOriginalColor.a, 0f, t);
                    burstRenderer.color = c;
                }

                yield return null;
            }

            visualBurst.transform.localScale = burstInitialScale;
            if (burstRenderer != null)
            {
                burstRenderer.color = burstOriginalColor;
            }

            visualBurst.SetActive(false);
            burstCoroutine = null;

            // Tắt toàn bộ GameObject khi hiệu ứng bùng nổ kết thúc
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Đặt lại toàn bộ trạng thái runtime của kén nước về mặc định.
        /// CHỈ reset state: không phát event, không tắt GameObject.
        /// Dùng chung cho CancelShell (kết thúc trong trận) và Clear (dọn dẹp khi tái sử dụng).
        /// </summary>
        private void ResetRuntimeState()
        {
            if (burstCoroutine != null)
            {
                StopCoroutine(burstCoroutine);
                burstCoroutine = null;
            }

            isShellActive = false;
            remainingTime = 0f;
            healTimer = 0f;
            hasTakenDamage = false;

            if (visualShell != null)
            {
                visualShell.transform.localScale = shellInitialScale;
                if (shellRenderer != null)
                {
                    shellRenderer.color = shellOriginalColor;
                }
                visualShell.SetActive(false);
            }

            if (visualBurst != null)
            {
                visualBurst.transform.localScale = burstInitialScale;
                if (burstRenderer != null)
                {
                    burstRenderer.color = burstOriginalColor;
                }
                visualBurst.SetActive(false);
            }
        }

        /// <summary>
        /// Hủy bỏ kén nước mà không kích hoạt thưởng hoàn tất.
        /// </summary>
        public void CancelShell()
        {
            ResetRuntimeState();
            OnShellStateChange?.Invoke(false);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Dọn dẹp để tái sử dụng: reset state và gỡ owner.
        /// Không phát event, không tắt GameObject, KHÔNG xoá subscriber —
        /// người nghe tự gỡ đăng ký của mình trong OnDisable/OnDestroy.
        /// Initialize gọi hàm này trước tiên.
        /// </summary>
        public void Clear()
        {
            ResetRuntimeState();
            ownerTank = null;
        }

        private void OnDisable()
        {
            CancelShell();
        }

        private void OnDestroy()
        {
            CancelShell();
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0.5f, duration);
            healInterval = Mathf.Max(0.05f, healInterval);
            healMaxHealthRatio = Mathf.Clamp01(healMaxHealthRatio);
            bonusCompletionHealMaxHealthRatio = Mathf.Clamp01(bonusCompletionHealMaxHealthRatio);
            shellPulseScale = Mathf.Clamp(shellPulseScale, 0f, 0.2f);
            shellPulseSpeed = Mathf.Max(0.1f, shellPulseSpeed);
            burstDuration = Mathf.Max(0.05f, burstDuration);
        }
    }
}
