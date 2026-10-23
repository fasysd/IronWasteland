using IronWasteland;
using IronWasteland.Tanks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronWasteland.Tanks.Leviathan
{
    /// <summary>
    /// Kỹ năng Tùy Chọn 2 - Biến Thể 3 của Leviathan: Tide Commander (Chỉ Huy Thủy Triều).
    /// 
    /// Hiệu quả:
    /// - Tạo ra một vùng thủy triều lan rộng dưới bánh xe và di chuyển đồng bộ bám sát theo vị trí xe tăng Leviathan.
    /// - Toàn bộ đồng minh (và bản thân nếu bật includeSelf) di chuyển trong vùng được gia tăng tốc độ di chuyển (MoveSpeed)
    ///   và nhận một lớp giáp nước hấp thụ sát thương (TideWaterShield).
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
        [SerializeField, Min(1.0f)] private float duration = 8.0f;

        [Tooltip("Bán kính vùng thủy triều di chuyển theo xe (đơn vị).")]
        [SerializeField, Min(1.0f)] private float zoneRadius = 4.0f;

        [Tooltip("Chu kỳ quét tìm đồng minh trong vùng (giây).")]
        [SerializeField, Min(0.02f)] private float scanInterval = 0.08f;

        [Header("Chỉ Số Hỗ Trợ (Combat Buffs)")]
        [Tooltip("Tốc độ di chuyển cộng thêm cho đồng minh khi đứng trong vùng (đơn vị/giây).")]
        [SerializeField, Min(0f)] private float bonusMoveSpeed = 2.0f;

        [Tooltip("Lượng sát thương tối đa lớp giáp nước có thể hấp thụ cho mỗi đồng minh.")]
        [SerializeField, Min(0f)] private float shieldCapacity = 120.0f;

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

        // Trạng thái nội bộ (Runtime State)
        private TankBrain ownerTank;
        private bool isZoneActive = false;
        private float remainingTime = 0f;
        private float scanTimer = 0f;

        // Quản lý đồng minh và khiên trong vùng
        private readonly HashSet<TankBrain> alliesInZone = new HashSet<TankBrain>();
        private readonly Dictionary<TankBrain, TideWaterShield> activeShields = new Dictionary<TankBrain, TideWaterShield>();

        // Sổ theo dõi các đồng minh ĐÃ được cấp giáp nước trong lần kích hoạt hiện tại.
        // Mục đích: mỗi đồng minh chỉ nhận tối đa 1 lớp giáp cho mỗi lần bật chiêu,
        // chặn lỗi bước ra rồi bước vào lại vùng để farm khiên liên tục.
        private readonly HashSet<TankBrain> shieldedThisCast = new HashSet<TankBrain>();

        private SpriteRenderer burstRenderer;
        private Vector3 burstInitialScale = Vector3.one;
        private Color burstInitialColor = Color.white;
        private Coroutine burstCoroutine;

        // Thuộc tính công khai (Read-only Getters & UI)
        public bool IsActive => isZoneActive && remainingTime > 0f;
        public float RemainingTime => remainingTime;
        public float Duration => duration;
        public float ZoneRadius => zoneRadius;
        public float BonusMoveSpeed => bonusMoveSpeed;
        public float ShieldCapacity => shieldCapacity;
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
            float customMoveSpeed = -1f,
            float customShield = -1f)
        {
            if (owner == null) return;

            // Dọn dẹp trạng thái của lần kích hoạt trước trước khi áp cấu hình mới
            Clear();

            this.ownerTank = owner;

            if (customDuration >= 0f) this.duration = customDuration;
            if (customMoveSpeed >= 0f) this.bonusMoveSpeed = customMoveSpeed;
            if (customShield >= 0f) this.shieldCapacity = customShield;

            remainingTime = duration;
            scanTimer = 0f;
            isZoneActive = true;

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
                OnAllyExitedZone?.Invoke(ally);
            }
        }

        private void ApplyMoveSpeedBuff(TankBrain tank)
        {
            if (tank == null || bonusMoveSpeed <= 0f) return;

            TankStats speedBonus = TankStats.Zeroed();
            speedBonus.MoveSpeed = bonusMoveSpeed;

            tank.AddBonusStats(TideCommanderSpeedBuffKey, speedBonus);
        }

        private void RemoveMoveSpeedBuff(TankBrain tank)
        {
            if (tank != null)
            {
                tank.RemoveBonusStats(TideCommanderSpeedBuffKey);
            }
        }

        private void GrantWaterShield(TankBrain tank)
        {
            if (tank == null || shieldCapacity <= 0f) return;

            // Mỗi đồng minh chỉ được cấp 1 lớp giáp nước cho mỗi lần bật chiêu.
            // Nếu đã cấp rồi thì bỏ qua, tránh việc bước ra rồi vào lại vùng để farm khiên.
            if (shieldedThisCast.Contains(tank)) return;

            // Nếu đồng minh chưa có khiên hoặc khiên cũ đã vỡ hoàn toàn -> cấp khiên mới
            if (!activeShields.TryGetValue(tank, out var shield) || !shield.IsActive)
            {
                var newShield = new TideWaterShield(tank, shieldCapacity);
                activeShields[tank] = newShield;
                tank.AddShield(newShield);
            }

            shieldedThisCast.Add(tank);
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

            // Gỡ bỏ các khiên giáp nước đang kích hoạt
            foreach (var kvp in activeShields)
            {
                TankBrain ally = kvp.Key;
                TideWaterShield shield = kvp.Value;
                if (ally != null && shield != null)
                {
                    shield.BreakShield();
                    ally.RemoveShield(shield);
                }
            }
            activeShields.Clear();

            // Reset sổ cấp khiên để lần bật chiêu sau cấp lại từ đầu
            shieldedThisCast.Clear();

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
                    TideWaterShield shield = kvp.Value;
                    if (ally != null && shield != null)
                    {
                        shield.BreakShield();
                        ally.RemoveShield(shield);
                    }
                }
                activeShields.Clear();
            }

            // Reset sổ cấp khiên để lần bật chiêu sau cấp lại từ đầu
            shieldedThisCast.Clear();

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
            bonusMoveSpeed = Mathf.Max(0f, bonusMoveSpeed);
            shieldCapacity = Mathf.Max(0f, shieldCapacity);
            burstDuration = Mathf.Max(0.05f, burstDuration);
        }
    }

    /// <summary>
    /// Lớp khiên giáp nước hấp thụ sát thương bảo vệ cho đồng minh đi trong vùng Tide Commander.
    /// Triển khai giao diện <see cref="ITankShield"/>.
    /// </summary>
    public class TideWaterShield : ITankShield
    {
        private readonly float maxCapacity;
        private float currentShield;
        private readonly TankBrain targetTank;

        public bool IsActive => currentShield > 0f && targetTank != null && targetTank.IsAlive;
        public float CurrentShield => currentShield;
        public float MaxCapacity => maxCapacity;

        public TideWaterShield(TankBrain tank, float capacity)
        {
            this.targetTank = tank;
            this.maxCapacity = capacity;
            this.currentShield = capacity;
        }

        public bool TryAbsorbDamage(ref float incomingDamage)
        {
            if (currentShield <= 0f) return false;

            if (incomingDamage <= currentShield)
            {
                currentShield -= incomingDamage;
                incomingDamage = 0f;
                return true;
            }
            else
            {
                incomingDamage -= currentShield;
                currentShield = 0f;
                return false;
            }
        }

        public void BreakShield()
        {
            currentShield = 0f;
        }
    }
}
