using IronWasteland;
using IronWasteland.Core.Effects;
using IronWasteland.Tanks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.Leviathan
{
    /// <summary>
    /// Kỹ năng Tùy Chọn 2 - Biến Thể 3 của Leviathan: Tide Commander (Chỉ Huy Thủy Triều).
    /// 
    /// Hiệu quả:
    /// - Tạo ra một vùng thủy triều lan rộng dưới bánh xe và di chuyển đồng bộ bám sát theo vị trí xe tăng Leviathan.
    /// - Toàn bộ đồng minh (và bản thân nếu bật includeSelf) di chuyển trong vùng được gia tăng tốc độ di chuyển (MoveSpeed)
    ///   và nhận một lớp giáp nước hấp thụ sát thương (<see cref="ShieldEffect"/> do Brain quản lý).
    /// - Khi đồng minh rời khỏi vùng, bùa tăng tốc độ di chuyển sẽ tự động được gỡ bỏ.
    /// 
    /// Hiển thị trực quan:
    /// - Vùng nước xoáy cuộn sóng lan rộng dưới bánh xe (VisualZone, sprite asset_tide_zone_0) xoay tròn liên tục.
    /// - Sóng nước bùng nổ khi mở vùng thủy triều (VisualBurst, sprite vfx_water_burst_0).
    /// </summary>
    [DisallowMultipleComponent]
    public class OptionalSkill2_3_Leviathan : MonoBehaviour
    {
        public event Action<TankBrain> OnAllyEnteredZone;
        public event Action<TankBrain> OnAllyExitedZone;
        public event Action<bool> OnZoneStateChange;

        [Header("Thông Số Vùng & Thời Gian")]
        [Tooltip("Thời gian duy trì vùng thủy triều (giây).")]
        [SerializeField, Min(1.0f)] private float duration = 7.0f;

        [Tooltip("Bán kính vùng thủy triều di chuyển theo xe (đơn vị).")]
        [SerializeField, Min(1.0f)] private float zoneRadius = 4.0f;

        [Tooltip("Chu kỳ quét tìm đồng minh trong vùng (giây).")]
        [SerializeField, Min(0.02f)] private float scanInterval = 0.08f;

        [Header("Chỉ Số Hỗ Trợ (Combat Buffs)")]
        [Tooltip("Tỉ lệ Tốc độ cộng thêm cho đồng minh khi đứng trong vùng (0.35 = +35% Tốc độ).")]
        [SerializeField, Min(0f)] private float bonusMoveSpeedRate = 0.35f;

        [Header("Giáp Nước (Water Shield)")]
        [Tooltip("Thời gian duy trì của lớp giáp nước (giây); mỗi nhịp hồi khiên sẽ đặt lại thời gian này.")]
        [SerializeField, Min(0.1f)] private float shieldLifetime = 5.0f;

        [Tooltip("Tỉ lệ Giới hạn máu của Leviathan cho lượng khiên ban đầu (0.1 = 10%).")]
        [SerializeField, Min(0f)] private float initialShieldMaxHealthRatio = 0.1f;

        [Tooltip("Tỉ lệ Giới hạn máu của Leviathan hồi thêm mỗi nhịp khi đồng minh đã có khiên (0.05 = 5%).")]
        [SerializeField, Min(0f)] private float shieldRefreshMaxHealthRatio = 0.05f;

        [Tooltip("Tỉ lệ Giới hạn máu của Leviathan cho mức khiên tối đa (0.15 = 15%).")]
        [SerializeField, Min(0f)] private float maxShieldMaxHealthRatio = 0.15f;

        [Tooltip("Giãn cách hồi khiên cho đồng minh đứng trong vùng (giây).")]
        [SerializeField, Min(0.05f)] private float shieldRefreshInterval = 1.0f;

        [Tooltip("Áp dụng hiệu ứng bùa lợi thủy triều lên cả bản thân xe tăng Leviathan.")]
        [SerializeField] private bool includeSelf = true;

        [Header("Hình Ảnh & Hiệu Ứng (Visuals)")]
        [Tooltip("GameObject hiển thị vùng nước cuộn sóng dưới bánh xe (VisualZone).")]
        [SerializeField] private GameObject visualZone;

        [Tooltip("GameObject hiển thị sóng nước bùng nổ lan tỏa khi mở vùng thủy triều (VisualBurst).")]
        [SerializeField] private GameObject visualBurst;

        [Tooltip("Tốc độ xoay của vùng nước cuộn sóng (độ/giây).")]
        [SerializeField] private float rotationSpeed = 70.0f;

        [Tooltip("Thời gian hiển thị hiệu ứng sóng nước bùng nổ khi kích hoạt (giây).")]
        [SerializeField, Min(0.05f)] private float burstDuration = 0.35f;

        private const string TideCommanderSpeedBuffKey = "Leviathan_TideCommander_MoveSpeed";
        private const string TideCommanderShieldEffectId = "Leviathan_TideCommander_Shield";

        // Trạng thái nội bộ (Runtime State)
        private TankBrain ownerTank;
        private bool isZoneActive = false;
        private float remainingTime = 0f;
        private float scanTimer = 0f;

        // Quản lý đồng minh và khiên trong vùng
        private readonly HashSet<TankBrain> alliesInZone = new HashSet<TankBrain>();
        private readonly Dictionary<TankBrain, ShieldEffect> activeShields = new Dictionary<TankBrain, ShieldEffect>();

        // Bộ đếm thời gian hồi khiên của từng đồng minh đang đứng trong vùng (giây).
        private readonly Dictionary<TankBrain, float> shieldRefreshTimers = new Dictionary<TankBrain, float>();

        // Bộ đệm dùng lại mỗi frame khi duyệt đồng minh, tránh cấp phát List mới liên tục.
        private readonly List<TankBrain> shieldRefreshBuffer = new List<TankBrain>();

        private SpriteRenderer burstRenderer;
        private Vector3 burstInitialScale = Vector3.one;
        private Color burstInitialColor = Color.white;
        private Coroutine burstCoroutine;

        // Thuộc tính công khai (Read-only Getters & UI)
        public bool IsActive => isZoneActive && remainingTime > 0f;
        public float RemainingTime => remainingTime;
        public float Duration => duration;
        public float ZoneRadius => zoneRadius;
        public float BonusMoveSpeedRate => bonusMoveSpeedRate;
        public float ShieldLifetime => shieldLifetime;
        public int AlliesCount => alliesInZone.Count;
        public TankBrain OwnerTank => ownerTank;

        private void Awake()
        {
            if (visualZone != null)
            {
                visualZone.SetActive(false);
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
                    burstInitialColor = burstRenderer.color;
                }

                visualBurst.SetActive(false);
            }

            // Tắt SpriteRenderer thừa trên chính GameObject này (nếu có)
            SpriteRenderer selfSr = GetComponent<SpriteRenderer>();
            if (selfSr != null)
            {
                selfSr.enabled = false;
            }
        }

        /// <summary>
        /// Kích hoạt kỹ năng Tide Commander cho xe tăng Leviathan.
        /// Cho phép truyền đè các thông số chiến đấu từ TankBrain.
        /// </summary>
        public void Initialize(
            TankBrain owner,
            float customDuration = -1f,
            float customMoveSpeedRate = -1f,
            float customShieldLifetime = -1f,
            float customInitialShieldRatio = -1f,
            float customShieldRefreshRatio = -1f,
            float customMaxShieldRatio = -1f,
            float customShieldRefreshInterval = -1f)
        {
            if (owner == null) return;

            // Dọn dẹp trạng thái của lần kích hoạt trước trước khi áp cấu hình mới
            Clear();

            this.ownerTank = owner;

            if (customDuration >= 0f) this.duration = customDuration;
            if (customMoveSpeedRate >= 0f) this.bonusMoveSpeedRate = customMoveSpeedRate;
            if (customShieldLifetime >= 0f) this.shieldLifetime = customShieldLifetime;
            if (customInitialShieldRatio >= 0f) this.initialShieldMaxHealthRatio = customInitialShieldRatio;
            if (customShieldRefreshRatio >= 0f) this.shieldRefreshMaxHealthRatio = customShieldRefreshRatio;
            if (customMaxShieldRatio >= 0f) this.maxShieldMaxHealthRatio = customMaxShieldRatio;
            if (customShieldRefreshInterval >= 0f) this.shieldRefreshInterval = customShieldRefreshInterval;

            remainingTime = duration;
            scanTimer = 0f;
            isZoneActive = true;
            shieldRefreshTimers.Clear();

            transform.position = ownerTank.transform.position;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (visualZone != null)
            {
                visualZone.transform.localPosition = Vector3.zero;
                visualZone.SetActive(true);
            }

            // Hiệu ứng sóng nước lan tỏa khi mở vùng thủy triều
            if (burstCoroutine != null) StopCoroutine(burstCoroutine);
            burstCoroutine = StartCoroutine(AnimateBurst());

            // Quét lần đầu tiên ngay khi vừa kích hoạt
            UpdateAlliesInZone();

            OnZoneStateChange?.Invoke(true);
        }

        private void Update()
        {
            if (!isZoneActive) return;

            // Kiểm tra xe tăng chủ thể
            if (ownerTank == null || !ownerTank.IsAlive)
            {
                CancelSkill();
                return;
            }

            // Di chuyển đồng bộ bám sát theo vị trí xe tăng Leviathan
            transform.position = ownerTank.transform.position;

            // Xoay tròn cuộn sóng của vùng nước
            if (visualZone != null && rotationSpeed != 0f)
            {
                visualZone.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            }

            // Quét tìm và cập nhật các đồng minh đứng trong vùng
            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = scanInterval;
                UpdateAlliesInZone();
            }

            // Hồi khiên theo nhịp cho đồng minh đang đứng trong vùng
            TickShieldRefresh();

            // Đếm ngược thời gian duy trì
            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                FinishSkill();
            }
        }

        /// <summary>
        /// Quét tìm toàn bộ xe tăng đồng minh trong bán kính vùng thủy triều thuần túy qua ColliderOwnerRef.
        /// Áp dụng buff tốc chạy và trao khiên giáp nước cho đồng minh mới vào vùng,
        /// và gỡ bùa tăng tốc khi đồng minh bước ra ngoài.
        /// </summary>
        private void UpdateAlliesInZone()
        {
            if (ownerTank == null) return;

            Physics2D.SyncTransforms();
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, zoneRadius);
            var currentAlliesFound = new HashSet<TankBrain>();

            // Bản thân luôn nằm trong vùng nếu bật includeSelf
            if (includeSelf && ownerTank != null && ownerTank.IsAlive)
            {
                currentAlliesFound.Add(ownerTank);
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null) continue;

                ColliderOwnerRef ownerRef = ColliderOwnerRef.FromCollider(col);
                if (ownerRef != null && ownerRef.TryGetOwner<TankBrain>(out var tank) && tank.IsAlive)
                {
                    if (tank == ownerTank)
                        continue;

                    currentAlliesFound.Add(tank);
                }
            }

            // 1. Xử lý các đồng minh mới bước vào vùng thủy triều
            foreach (TankBrain ally in currentAlliesFound)
            {
                if (!alliesInZone.Contains(ally))
                {
                    alliesInZone.Add(ally);
                    ApplyMoveSpeedBuff(ally);
                    shieldRefreshTimers[ally] = 0f;
                    GrantWaterShield(ally);
                    OnAllyEnteredZone?.Invoke(ally);
                }
            }

            // 2. Xử lý các đồng minh đã rời khỏi vùng hoặc tử trận
            var exitedAllies = new List<TankBrain>();
            foreach (TankBrain ally in alliesInZone)
            {
                if (!currentAlliesFound.Contains(ally) || !ally.IsAlive)
                {
                    exitedAllies.Add(ally);
                }
            }

            for (int i = 0; i < exitedAllies.Count; i++)
            {
                TankBrain ally = exitedAllies[i];
                alliesInZone.Remove(ally);
                RemoveMoveSpeedBuff(ally);
                shieldRefreshTimers.Remove(ally);
                OnAllyExitedZone?.Invoke(ally);
            }
        }

        private void ApplyMoveSpeedBuff(TankBrain tank)
        {
            if (tank == null || bonusMoveSpeedRate <= 0f) return;

            TankStats speedBonus = TankStats.Zeroed();
            speedBonus.MoveSpeed = bonusMoveSpeedRate;

            tank.AddBonusStats(TideCommanderSpeedBuffKey, null, speedBonus);
        }

        private void RemoveMoveSpeedBuff(TankBrain tank)
        {
            if (tank != null)
            {
                tank.RemoveBonusStats(TideCommanderSpeedBuffKey);
            }
        }

        /// <summary>
        /// Cấp/hồi giáp nước cho một đồng minh. Nếu đồng minh chưa có khiên thì nhận lượng khiên
        /// ban đầu (10% Giới hạn máu của Leviathan); nếu đã có khiên thì hồi thêm 5% Giới hạn máu,
        /// đặt lại thời gian duy trì 5 giây và không vượt quá 15% Giới hạn máu.
        /// </summary>
        private void GrantWaterShield(TankBrain tank)
        {
            if (tank == null) return;

            float leviathanMaxHealth = ownerTank != null && ownerTank.Stats != null ? ownerTank.Stats.MaxHealth : 0f;
            if (leviathanMaxHealth <= 0f) return;

            float maxShield = leviathanMaxHealth * Mathf.Max(0f, maxShieldMaxHealthRatio);
            ShieldEffect currentShield = tank.GetEffect(TideCommanderShieldEffectId) as ShieldEffect;

            float targetShield;
            if (currentShield != null)
            {
                // Đã có giáp nước: hồi thêm một nhịp và đặt lại thời gian duy trì.
                targetShield = Mathf.Max(0f, currentShield.RemainingShield)
                               + leviathanMaxHealth * Mathf.Max(0f, shieldRefreshMaxHealthRatio);
            }
            else
            {
                // Chưa có (hoặc vừa vỡ): cấp lượng khiên ban đầu.
                targetShield = leviathanMaxHealth * Mathf.Max(0f, initialShieldMaxHealthRatio);
            }

            targetShield = Mathf.Min(targetShield, maxShield);
            if (targetShield <= 0f) return;

            // Gỡ lớp cũ rồi cấp lại để ShieldEffect nhận đúng lượng khiên mới và thời gian duy trì mới
            // (ShieldEffect không có API cộng dồn lượng khiên, chỉ có hấp thụ).
            if (currentShield != null)
            {
                tank.RemoveEffect(TideCommanderShieldEffectId);
            }

            ShieldEffect shield = tank.AddShield(
                targetShield,
                Mathf.Max(0.1f, shieldLifetime),
                TideCommanderShieldEffectId,
                this,
                "Giáp Nước");

            if (shield != null)
            {
                activeShields[tank] = shield;
            }
        }

        /// <summary>
        /// Đếm nhịp hồi khiên cho từng đồng minh đang đứng trong vùng thủy triều.
        /// </summary>
        private void TickShieldRefresh()
        {
            if (alliesInZone.Count == 0) return;

            float interval = Mathf.Max(0.05f, shieldRefreshInterval);

            // Duyệt trên bộ đệm để an toàn nếu GrantWaterShield làm thay đổi tập đồng minh.
            shieldRefreshBuffer.Clear();
            shieldRefreshBuffer.AddRange(alliesInZone);

            for (int i = 0; i < shieldRefreshBuffer.Count; i++)
            {
                TankBrain ally = shieldRefreshBuffer[i];
                if (ally == null || !ally.IsAlive) continue;

                shieldRefreshTimers.TryGetValue(ally, out float timer);
                timer += Time.deltaTime;

                if (timer >= interval)
                {
                    timer -= interval;
                    GrantWaterShield(ally);
                }

                shieldRefreshTimers[ally] = timer;
            }
        }

        private IEnumerator AnimateBurst()
        {
            if (visualBurst == null) yield break;

            visualBurst.transform.localPosition = Vector3.zero;
            visualBurst.transform.localScale = burstInitialScale;
            if (burstRenderer != null)
            {
                burstRenderer.color = burstInitialColor;
            }
            visualBurst.SetActive(true);

            Vector3 startScale = burstInitialScale;
            Vector3 targetScale = burstInitialScale * 1.4f;

            float elapsed = 0f;
            while (elapsed < burstDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / burstDuration);

                visualBurst.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

                if (burstRenderer != null)
                {
                    Color c = burstInitialColor;
                    c.a = Mathf.Lerp(burstInitialColor.a, 0f, t);
                    burstRenderer.color = c;
                }

                yield return null;
            }

            visualBurst.transform.localScale = burstInitialScale;
            if (burstRenderer != null)
            {
                burstRenderer.color = burstInitialColor;
            }
            visualBurst.SetActive(false);
        }

        private void FinishSkill()
        {
            isZoneActive = false;
            remainingTime = 0f;

            // Gỡ bỏ bùa tăng tốc khỏi toàn bộ đồng minh đang ở trong vùng
            foreach (TankBrain ally in alliesInZone)
            {
                RemoveMoveSpeedBuff(ally);
            }
            alliesInZone.Clear();

            // Gỡ bỏ các lớp giáp nước còn lại (lớp đã cạn thì Brain tự dọn trước đó rồi)
            foreach (var kvp in activeShields)
            {
                TankBrain ally = kvp.Key;
                if (ally != null)
                {
                    ally.RemoveEffect(TideCommanderShieldEffectId);
                }
            }
            activeShields.Clear();

            // Reset bộ đếm hồi khiên để lần bật chiêu sau cấp lại từ đầu
            shieldRefreshTimers.Clear();

            if (visualZone != null)
            {
                visualZone.SetActive(false);
            }

            if (visualBurst != null)
            {
                visualBurst.SetActive(false);
            }

            gameObject.SetActive(false);
            OnZoneStateChange?.Invoke(false);
        }

        /// <summary>
        /// Đặt lại toàn bộ trạng thái runtime của vùng thủy triều về mặc định.
        /// CHỈ reset state: không phát event, không tắt GameObject.
        /// Dùng chung cho CancelSkill (kết thúc trong trận) và Clear (dọn dẹp khi tái sử dụng).
        /// </summary>
        private void ResetRuntimeState()
        {
            StopAllCoroutines();
            burstCoroutine = null;

            if (isZoneActive)
            {
                foreach (TankBrain ally in alliesInZone)
                {
                    RemoveMoveSpeedBuff(ally);
                }
                alliesInZone.Clear();

                foreach (var kvp in activeShields)
                {
                    TankBrain ally = kvp.Key;
                    if (ally != null)
                    {
                        ally.RemoveEffect(TideCommanderShieldEffectId);
                    }
                }
                activeShields.Clear();
            }

            // Reset bộ đếm hồi khiên để lần bật chiêu sau cấp lại từ đầu
            shieldRefreshTimers.Clear();

            isZoneActive = false;
            remainingTime = 0f;

            if (visualZone != null)
            {
                visualZone.SetActive(false);
            }

            if (visualBurst != null)
            {
                visualBurst.transform.localScale = burstInitialScale;
                if (burstRenderer != null)
                {
                    burstRenderer.color = burstInitialColor;
                }
                visualBurst.SetActive(false);
            }
        }

        /// <summary>
        /// Hủy bỏ kỹ năng khi xe tăng tử trận hoặc kết thúc trận đấu.
        /// </summary>
        public void CancelSkill()
        {
            ResetRuntimeState();
            gameObject.SetActive(false);
            OnZoneStateChange?.Invoke(false);
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
            CancelSkill();
        }

        private void OnDestroy()
        {
            CancelSkill();
        }

        private void OnValidate()
        {
            duration = Mathf.Max(1.0f, duration);
            zoneRadius = Mathf.Max(1.0f, zoneRadius);
            scanInterval = Mathf.Max(0.02f, scanInterval);
            bonusMoveSpeedRate = Mathf.Max(0f, bonusMoveSpeedRate);
            shieldLifetime = Mathf.Max(0.1f, shieldLifetime);
            initialShieldMaxHealthRatio = Mathf.Max(0f, initialShieldMaxHealthRatio);
            shieldRefreshMaxHealthRatio = Mathf.Max(0f, shieldRefreshMaxHealthRatio);
            maxShieldMaxHealthRatio = Mathf.Max(0f, maxShieldMaxHealthRatio);
            shieldRefreshInterval = Mathf.Max(0.05f, shieldRefreshInterval);
            burstDuration = Mathf.Max(0.05f, burstDuration);
        }
    }
}
