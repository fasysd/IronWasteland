using IronWasteland;
using IronWasteland.Tanks;
using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.Leviathan
{
    /// <summary>
    /// Kỹ năng Tùy Chọn 2 - Biến Thể 2 của Leviathan: Leviathan Form (Dạng Thủy Quái).
    /// Triển khai giao diện <see cref="ITankShield"/> để cung cấp khả năng miễn nhiễm sát thương tạm thời.
    /// 
    /// Hiệu quả:
    /// - Chuyển sang dạng thủy quái phòng thủ: tăng mạnh giới hạn máu (MaxHealth),
    ///   hồi máu liên tục mỗi giây và nhận khoảng thời gian ngắn miễn hoàn toàn sát thương (Invulnerability) khi vừa chuyển dạng.
    /// - Đổi lại, chỉ số tấn công (DamageMultiplier) và giảm hồi chiêu (CooldownReduction) bị giảm (phạt).
    /// 
    /// Hiển thị trực quan:
    /// - Cơ thể xe tăng phình to (View scale phóng to 1.25x).
    /// - Linh ảnh đầu rồng nước xanh đậm bao phủ lấy thân xe (VisualDragonSpirit) rống lên chấn động toàn bộ khung hình (VisualRoarBurst + Screen Shake).
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionalSkill2_1_Leviathan : MonoBehaviour, ITankShield
    {
        public event Action<bool> OnFormStateChange;
        public event Action OnRoarTriggered;
        public event Action<float> OnDamageImmunityAbsorbed;

        [Header("Thông Số Thời Gian")]
        [Tooltip("Thời gian duy trì dạng thủy quái phòng thủ (giây).")]
        [SerializeField, Min(1.0f)] private float duration = 8.0f;

        [Tooltip("Khoảng thời gian ngắn miễn nhiễm hoàn toàn sát thương khi vừa chuyển dạng (giây).")]
        [SerializeField, Min(0.1f)] private float invulnerabilityDuration = 1.2f;

        [Header("Chỉ Số Buff Sinh Tồn")]
        [Tooltip("Lượng máu tối đa (MaxHealth) cộng thêm khi chuyển sang dạng thủy quái.")]
        [SerializeField, Min(0f)] private float bonusMaxHealth = 300.0f;

        [Tooltip("Lượng máu hồi phục liên tục mỗi giây trong suốt thời gian biến dạng.")]
        [SerializeField, Min(0f)] private float healPerSecond = 20.0f;

        [Header("Chỉ Số Phạt (Trade-off)")]
        [Tooltip("Phần trăm sát thương của đòn đánh thường bị giảm trong khi ở dạng thủy quái (0.25 = giảm 25% sát thương đòn đánh thường).")]
        [SerializeField, Range(0f, 1f)] private float damagePenalty = 0.25f;

        [Tooltip("Phần trăm thời gian hồi chiêu của đòn đánh thường bị tăng thêm trong khi ở dạng thủy quái (0.2 = tăng 20% thời gian hồi chiêu đòn đánh thường).")]
        [SerializeField, Range(0f, 1f)] private float cooldownPenalty = 0.2f;

        [Header("Hình Ảnh & Hiệu Ứng (Visuals)")]
        [Tooltip("GameObject hiển thị linh ảnh đầu rồng nước bao phủ thân xe (VisualDragonSpirit).")]
        [SerializeField] private GameObject visualDragonSpirit;

        [Tooltip("GameObject hiển thị sóng nước bùng nổ khi đầu rồng gầm thét (VisualRoarBurst).")]
        [SerializeField] private GameObject visualRoarBurst;

        [Tooltip("Tỷ lệ phóng to cơ thể xe tăng khi biến dạng (1.25 = to hơn 25%).")]
        [SerializeField, Range(1.0f, 1.5f)] private float bodyExpansionScale = 1.25f;

        [Tooltip("Thời gian chuyển đổi phóng to/thu nhỏ thân xe tăng (giây).")]
        [SerializeField, Min(0.05f)] private float transformTransitionDuration = 0.35f;

        [Tooltip("Biên độ nhấp nhô uy dũng của linh ảnh đầu rồng (0 = không nhấp nhô, 0.05 = 5%).")]
        [SerializeField, Range(0f, 0.2f)] private float dragonPulseScale = 0.04f;

        [Tooltip("Tần số nhấp nhô của linh ảnh đầu rồng (chu kỳ/giây).")]
        [SerializeField, Min(0.1f)] private float dragonPulseSpeed = 2.0f;

        [Header("Rung Lắc Màn Hình (Screen Shake)")]
        [Tooltip("Thời gian rung màn hình khi đầu rồng gầm thét (giây).")]
        [SerializeField, Min(0.05f)] private float roarShakeDuration = 0.4f;

        [Tooltip("Cường độ rung màn hình.")]
        [SerializeField, Min(0.01f)] private float roarShakeMagnitude = 0.16f;

        private const string FormBonusStatsKey = "Leviathan_LeviathanForm";

        // Trạng thái nội bộ (Runtime State)
        private TankBrain ownerTank;
        private bool isFormActive = false;
        private float remainingTime = 0f;
        private float remainingInvulnerableTime = 0f;

        private SpriteRenderer dragonRenderer;
        private SpriteRenderer burstRenderer;

        private Vector3 dragonInitialScale = Vector3.one;
        private Vector3 burstInitialScale = Vector3.one;
        private Color dragonInitialColor = Color.white;
        private Color burstInitialColor = Color.white;

        private Vector3 originalViewScale = Vector3.one;
        private Coroutine viewScaleCoroutine;
        private Coroutine burstCoroutine;
        private Coroutine shakeCoroutine;

        // Triển khai ITankShield (Miễn sát thương trong cửa sổ invulnerabilityDuration)
        bool ITankShield.IsActive => isFormActive && remainingInvulnerableTime > 0f && ownerTank != null && ownerTank.IsAlive;

        public bool TryAbsorbDamage(ref float incomingDamage)
        {
            if (!isFormActive || remainingInvulnerableTime <= 0f)
                return false;

            float absorbed = incomingDamage;
            incomingDamage = 0f;

            OnDamageImmunityAbsorbed?.Invoke(absorbed);
            return true;
        }

        public void BreakShield()
        {
            remainingInvulnerableTime = 0f;
            if (ownerTank != null)
            {
                ownerTank.RemoveShield(this);
            }
        }

        // Thuộc tính công khai (Read-only Getters & UI)
        public bool IsActive => isFormActive && remainingTime > 0f;
        public bool IsInvulnerable => isFormActive && remainingInvulnerableTime > 0f;
        public float RemainingTime => remainingTime;
        public float Duration => duration;
        public float RemainingInvulnerableTime => remainingInvulnerableTime;
        public float InvulnerabilityDuration => invulnerabilityDuration;
        public float BonusMaxHealth => bonusMaxHealth;
        public float HealPerSecond => healPerSecond;
        public float DamagePenalty => damagePenalty;
        public float CooldownPenalty => cooldownPenalty;
        public TankBrain OwnerTank => ownerTank;

        private void Awake()
        {
            if (visualDragonSpirit != null)
            {
                dragonRenderer = visualDragonSpirit.GetComponent<SpriteRenderer>();
                if (dragonRenderer == null)
                {
                    dragonRenderer = visualDragonSpirit.GetComponentInChildren<SpriteRenderer>();
                }

                dragonInitialScale = visualDragonSpirit.transform.localScale;
                if (dragonRenderer != null)
                {
                    dragonInitialColor = dragonRenderer.color;
                }

                visualDragonSpirit.SetActive(false);
            }

            if (visualRoarBurst != null)
            {
                burstRenderer = visualRoarBurst.GetComponent<SpriteRenderer>();
                if (burstRenderer == null)
                {
                    burstRenderer = visualRoarBurst.GetComponentInChildren<SpriteRenderer>();
                }

                burstInitialScale = visualRoarBurst.transform.localScale;
                if (burstRenderer != null)
                {
                    burstInitialColor = burstRenderer.color;
                }

                visualRoarBurst.SetActive(false);
            }

            // Tắt SpriteRenderer thừa trên chính GameObject này (nếu có)
            SpriteRenderer selfSr = GetComponent<SpriteRenderer>();
            if (selfSr != null)
            {
                selfSr.enabled = false;
            }
        }

        /// <summary>
        /// Kích hoạt kỹ năng Leviathan Form cho xe tăng chủ thể.
        /// Cho phép truyền đè các thông số tính toán từ TankBrain.
        /// </summary>
        public void Initialize(
            TankBrain owner,
            float customDuration = -1f,
            float customInvulDuration = -1f,
            float customBonusMaxHp = -1f,
            float customHealRate = -1f,
            float customDmgPenalty = -1f,
            float customCdrPenalty = -1f)
        {
            if (owner == null) return;
            this.ownerTank = owner;

            if (customDuration >= 0f) this.duration = customDuration;
            if (customInvulDuration >= 0f) this.invulnerabilityDuration = customInvulDuration;
            if (customBonusMaxHp >= 0f) this.bonusMaxHealth = customBonusMaxHp;
            if (customHealRate >= 0f) this.healPerSecond = customHealRate;
            if (customDmgPenalty >= 0f) this.damagePenalty = customDmgPenalty;
            if (customCdrPenalty >= 0f) this.cooldownPenalty = customCdrPenalty;

            // Nếu đang trong dạng trước thì gỡ bỏ trạng thái cũ trước khi kích hoạt mới
            if (isFormActive)
            {
                CancelForm();
            }

            remainingTime = duration;
            remainingInvulnerableTime = invulnerabilityDuration;
            isFormActive = true;

            // 1. Áp dụng Bonus Stats: Tăng MaxHealth, giảm DamageMultiplier & CooldownReduction
            ApplyFormBonus();

            // 2. Đăng ký khiên miễn sát thương lên xe tăng chủ thể
            ownerTank.AddShield(this);

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // 3. Phóng to cơ thể xe tăng (View)
            if (ownerTank.View != null)
            {
                originalViewScale = ownerTank.View.transform.localScale;
                if (viewScaleCoroutine != null) StopCoroutine(viewScaleCoroutine);
                viewScaleCoroutine = StartCoroutine(AnimateViewScale(originalViewScale, originalViewScale * bodyExpansionScale, transformTransitionDuration));
            }

            // 4. Kích hoạt linh ảnh đầu rồng nước
            if (visualDragonSpirit != null)
            {
                visualDragonSpirit.transform.localPosition = Vector3.zero;
                visualDragonSpirit.transform.localScale = dragonInitialScale;
                if (dragonRenderer != null)
                {
                    dragonRenderer.color = dragonInitialColor;
                }
                visualDragonSpirit.SetActive(true);
            }

            // 5. Hiệu ứng gầm thét: Sóng nước bùng nổ + Rung màn hình
            if (burstCoroutine != null) StopCoroutine(burstCoroutine);
            burstCoroutine = StartCoroutine(AnimateRoarBurst());

            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(AnimateScreenShake(roarShakeDuration, roarShakeMagnitude));

            OnRoarTriggered?.Invoke();
            OnFormStateChange?.Invoke(true);
        }

        private void ApplyFormBonus()
        {
            if (ownerTank == null) return;

            TankStats bonus = TankStats.Zeroed();
            bonus.MaxHealth = bonusMaxHealth;
            // Lưu ý: Cả sát thương phạt (damagePenalty) và thời gian hồi chiêu phạt (cooldownPenalty)
            // đều được áp dụng riêng biệt cho đòn đánh thường (Normal Attack) bên trong TankBrain_Leviathan.

            ownerTank.AddBonusStats(FormBonusStatsKey, bonus);

            // Hồi phục ngay lập tức một lượng máu bằng bonusMaxHealth để việc gia tăng thanh máu có ý nghĩa tức thì
            if (bonusMaxHealth > 0f)
            {
                ownerTank.Heal(bonusMaxHealth);
            }
        }

        private void RemoveFormBonus()
        {
            if (ownerTank != null)
            {
                ownerTank.RemoveBonusStats(FormBonusStatsKey);
            }
        }

        private void Update()
        {
            if (!isFormActive) return;

            // Kiểm tra xe tăng chủ thể
            if (ownerTank == null || !ownerTank.IsAlive)
            {
                CancelForm();
                return;
            }

            // Đồng bộ vị trí linh ảnh theo xe tăng
            transform.position = ownerTank.transform.position;

            // 1. Quản lý thời gian miễn sát thương
            if (remainingInvulnerableTime > 0f)
            {
                remainingInvulnerableTime -= Time.deltaTime;
                if (remainingInvulnerableTime <= 0f)
                {
                    remainingInvulnerableTime = 0f;
                    ownerTank.RemoveShield(this);
                }
            }

            // 2. Hồi máu liên tục mỗi giây
            if (healPerSecond > 0f)
            {
                ownerTank.Heal(healPerSecond * Time.deltaTime);
            }

            // 3. Hiệu ứng nhấp nhô uy dũng của linh ảnh đầu rồng
            if (visualDragonSpirit != null && dragonPulseScale > 0f && dragonPulseSpeed > 0f)
            {
                float pulse = 1f + Mathf.Sin(Time.time * dragonPulseSpeed * Mathf.PI * 2f) * dragonPulseScale;
                visualDragonSpirit.transform.localScale = dragonInitialScale * pulse;
            }

            // 4. Đếm ngược thời gian duy trì dạng thủy quái
            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                FinishForm();
            }
        }

        private IEnumerator AnimateViewScale(Vector3 startScale, Vector3 targetScale, float dur)
        {
            if (ownerTank == null || ownerTank.View == null) yield break;

            Transform viewTransform = ownerTank.View.transform;
            float elapsed = 0f;

            while (elapsed < dur)
            {
                if (ownerTank == null || ownerTank.View == null) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                // Ease Out Quad
                float easeOut = 1f - (1f - t) * (1f - t);

                viewTransform.localScale = Vector3.Lerp(startScale, targetScale, easeOut);
                yield return null;
            }

            if (ownerTank != null && ownerTank.View != null)
            {
                ownerTank.View.transform.localScale = targetScale;
            }
        }

        private IEnumerator AnimateRoarBurst()
        {
            if (visualRoarBurst == null) yield break;

            visualRoarBurst.transform.localPosition = Vector3.zero;
            visualRoarBurst.transform.localScale = burstInitialScale;
            if (burstRenderer != null)
            {
                burstRenderer.color = burstInitialColor;
            }
            visualRoarBurst.SetActive(true);

            Vector3 startScale = burstInitialScale;
            Vector3 targetScale = burstInitialScale * 1.5f;
            float dur = 0.45f;
            float elapsed = 0f;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);

                visualRoarBurst.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

                if (burstRenderer != null)
                {
                    Color c = burstInitialColor;
                    c.a = Mathf.Lerp(burstInitialColor.a, 0f, t);
                    burstRenderer.color = c;
                }

                yield return null;
            }

            visualRoarBurst.transform.localScale = burstInitialScale;
            if (burstRenderer != null)
            {
                burstRenderer.color = burstInitialColor;
            }
            visualRoarBurst.SetActive(false);
        }

        private IEnumerator AnimateScreenShake(float dur, float magnitude)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;

            Transform camTransform = cam.transform;
            Vector3 originalCamPos = camTransform.position;
            float elapsed = 0f;

            while (elapsed < dur)
            {
                if (cam == null) yield break;

                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / dur);
                float strength = (1f - progress) * magnitude;

                Vector2 offset = UnityEngine.Random.insideUnitCircle * strength;
                camTransform.position = new Vector3(originalCamPos.x + offset.x, originalCamPos.y + offset.y, originalCamPos.z);
                yield return null;
            }

            if (cam != null)
            {
                camTransform.position = originalCamPos;
            }
        }

        private void FinishForm()
        {
            isFormActive = false;
            remainingTime = 0f;
            remainingInvulnerableTime = 0f;

            // Gỡ bỏ khiên miễn sát thương nếu còn
            if (ownerTank != null)
            {
                ownerTank.RemoveShield(this);
            }

            // Gỡ bỏ chỉ số buff/debuff
            RemoveFormBonus();

            // Thu nhỏ View của xe tăng về lại kích thước ban đầu
            if (ownerTank != null && ownerTank.View != null)
            {
                if (viewScaleCoroutine != null) StopCoroutine(viewScaleCoroutine);
                viewScaleCoroutine = StartCoroutine(AnimateViewScale(ownerTank.View.transform.localScale, originalViewScale, transformTransitionDuration));
            }

            // Tắt linh ảnh đầu rồng
            if (visualDragonSpirit != null)
            {
                visualDragonSpirit.transform.localScale = dragonInitialScale;
                visualDragonSpirit.SetActive(false);
            }

            if (visualRoarBurst != null)
            {
                visualRoarBurst.transform.localScale = burstInitialScale;
                visualRoarBurst.SetActive(false);
            }

            OnFormStateChange?.Invoke(false);
        }

        /// <summary>
        /// Hủy bỏ dạng thủy quái khi xe tử trận hoặc tắt game.
        /// </summary>
        public void CancelForm()
        {
            StopAllCoroutines();
            viewScaleCoroutine = null;
            burstCoroutine = null;
            shakeCoroutine = null;

            if (isFormActive)
            {
                if (ownerTank != null)
                {
                    ownerTank.RemoveShield(this);
                }
                RemoveFormBonus();
            }

            isFormActive = false;
            remainingTime = 0f;
            remainingInvulnerableTime = 0f;

            if (ownerTank != null && ownerTank.View != null && originalViewScale != Vector3.zero)
            {
                ownerTank.View.transform.localScale = originalViewScale;
            }

            if (visualDragonSpirit != null)
            {
                visualDragonSpirit.transform.localScale = dragonInitialScale;
                visualDragonSpirit.SetActive(false);
            }

            if (visualRoarBurst != null)
            {
                visualRoarBurst.transform.localScale = burstInitialScale;
                visualRoarBurst.SetActive(false);
            }

            gameObject.SetActive(false);
            OnFormStateChange?.Invoke(false);
        }

        public void Clear()
        {
            CancelForm();
            ownerTank = null;
            OnFormStateChange = null;
            OnRoarTriggered = null;
            OnDamageImmunityAbsorbed = null;
        }

        private void OnDisable()
        {
            CancelForm();
        }

        private void OnDestroy()
        {
            CancelForm();
        }

        private void OnValidate()
        {
            duration = Mathf.Max(1.0f, duration);
            invulnerabilityDuration = Mathf.Max(0.1f, invulnerabilityDuration);
            bonusMaxHealth = Mathf.Max(0f, bonusMaxHealth);
            healPerSecond = Mathf.Max(0f, healPerSecond);
            transformTransitionDuration = Mathf.Max(0.05f, transformTransitionDuration);
            roarShakeDuration = Mathf.Max(0.05f, roarShakeDuration);
            roarShakeMagnitude = Mathf.Max(0.01f, roarShakeMagnitude);
        }
    }
}
