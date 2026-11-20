using System.Collections;
using IronWasteland.Core;
using IronWasteland.Core.Effects;
using UnityEngine;

namespace IronWasteland.Enemies.Test
{
    /// <summary>
    /// Bia tap ban / Target Dummy ke dich TINH ANH (Elite/Boss) dung de kiem thu:
    /// - Co che "Bung no nang luong" (State Machine: Normal -> Charging -> Active -> Reward / Penalty).
    /// - Su kien OnEnergyBurstSuccess de kiem thu Phu kien 20 (D1ONK).
    /// - Mien nhiem khong che cung (Root/Stun).
    /// - He thong sat thuong, giap, xuyen giap va log chi tiet.
    /// </summary>
    [DisallowMultipleComponent]
    public class EliteEnemyTest : EliteEnemyBrain
    {
        [Header("--- Test Settings & Toggles ---")]
        [Tooltip("Neu bat, ke dich se mien nhiem moi sat thuong.")]
        [SerializeField] private bool isInvincible = false;

        [Tooltip("Neu bat, moi don danh nhan vao se duoc in log chi tiet vao Console.")]
        [SerializeField] private bool logDetailedDamage = true;

        [Tooltip("Neu bat, se lien tuc tu dong hoi phuc % mau toi da moi giay.")]
        [SerializeField] private bool continuousHeal = false;
        [SerializeField, Range(0.01f, 1f)] private float continuousHealRate = 0.1f; // 10%/s

        [Tooltip("Neu bat, se lien tuc tu dong mat % mau toi da moi giay.")]
        [SerializeField] private bool continuousHpLoss = false;
        [SerializeField, Range(0.01f, 1f)] private float continuousHpLossRate = 0.1f; // 10%/s

        [Tooltip("Tu dong hoi sinh sau khi bi ha guc de tiep tuc test.")]
        [SerializeField] private bool autoReviveOnDeath = true;
        [SerializeField] private float autoReviveDelay = 1.5f;

        private Rigidbody2D m_Rb;
        private ColliderOwnerRef m_OwnerRef;

        public bool IsInvincible
        {
            get => isInvincible;
            set => isInvincible = value;
        }

        public bool LogDetailedDamage
        {
            get => logDetailedDamage;
            set => logDetailedDamage = value;
        }

        protected override void Awake()
        {
            base.Awake();

            m_Rb = GetComponent<Rigidbody2D>();
            if (m_Rb != null)
            {
                m_Rb.bodyType = RigidbodyType2D.Kinematic;
                m_Rb.linearVelocity = Vector2.zero;
                m_Rb.angularVelocity = 0f;
            }

            m_OwnerRef = GetComponent<ColliderOwnerRef>();
            if (m_OwnerRef == null) m_OwnerRef = GetComponentInChildren<ColliderOwnerRef>();
            if (m_OwnerRef != null)
            {
                m_OwnerRef.SetOwner(this);
            }

            if (Stats == null || Stats.MaxHealth <= 0f)
            {
                EnemyStats defaultStats = new EnemyStats
                {
                    MaxHealth = 3000f,
                    Attack = 50f,
                    Defense = 80f,
                    MoveSpeed = 0f
                };
                Initialize(defaultStats);
            }
            else
            {
                Initialize();
            }
        }

        protected virtual void Update()
        {
            if (!IsAlive) return;

            float dt = Time.deltaTime;

            if (continuousHeal && MaxHealth > 0f)
            {
                float healAmount = MaxHealth * continuousHealRate * dt;
                Heal(healAmount);
            }

            if (continuousHpLoss && MaxHealth > 0f)
            {
                float lossAmount = MaxHealth * continuousHpLossRate * dt;
                TakeDamage(new DamageInfo(null, this, lossAmount, isDirectToHealth: true, sourceType: DamageSourceType.DamageOverTime));
            }
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            Debug.Log($"<color=red>[ELITE: {name}] Đã bị tiêu diệt!</color>");
            if (autoReviveOnDeath && gameObject.activeInHierarchy)
            {
                StartCoroutine(ReviveRoutine());
            }
        }

        private IEnumerator ReviveRoutine()
        {
            yield return new WaitForSeconds(autoReviveDelay);
            Revive();
        }

        public override void TakeDamage(DamageInfo info)
        {
            if (isInvincible)
            {
                if (logDetailedDamage)
                {
                    Debug.Log($"<color=yellow>[ELITE: {name}] Đang BẤT TỬ! Bỏ qua sát thương từ {info.Attacker?.name ?? "Unknown"} ({info.SourceType}).</color>");
                }
                return;
            }

            float hpBefore = CurrentHealth;
            float shieldBefore = CurrentShield;
            float defenseAtHit = Stats != null ? Stats.Defense : 0f;

            base.TakeDamage(info);

            if (logDetailedDamage)
            {
                float damageToShield = Mathf.Max(0f, shieldBefore - CurrentShield);
                float damageToHealth = Mathf.Max(0f, hpBefore - CurrentHealth);
                float reducedByArmor = Mathf.Max(0f, info.RawDamage - info.FinalDamage);

                string attackerName = info.Attacker != null ? info.Attacker.name : "None";
                string burstStatus = BurstState switch
                {
                    EliteBurstState.Charging => $"<color=yellow>ĐANG VẬN SỨC ({BurstTimer:F1}s)</color>",
                    EliteBurstState.Active => $"<color=orange>ĐANG BÙNG NỔ (Còn {BurstTimer:F1}s)</color>",
                    EliteBurstState.RewardPhase => $"<color=green>THƯỞNG ({BurstTimer:F1}s)</color>",
                    EliteBurstState.PenaltyPhase => $"<color=red>PHẠT ({BurstTimer:F1}s)</color>",
                    EliteBurstState.Finished => "<color=grey>KẾT THÚC</color>",
                    _ => "Chưa kích hoạt"
                };

                string log =
                    $"<b><color=#FF5500>[Elite Damage Log: {name}]</color></b>\n" +
                    $"  • <b>Attacker:</b> {attackerName}\n" +
                    $"  • <b>Source Type:</b> <color=cyan>{info.SourceType}</color> " +
                    $"(IsNormalSkill: {info.IsNormalSkill}, IsTankSkill: {info.IsTankSkill}, IsAccessory: {info.IsAccessoryDamage}, IsDoT: {info.IsDamageOverTime})\n" +
                    $"  • <b>Raw Damage:</b> {info.RawDamage:F1} | <b>Armor Pen:</b> {info.ArmorPenetration:F1}\n" +
                    $"  • <b>Target Defense:</b> {defenseAtHit:F1} → <b>Giảm bởi giáp:</b> {reducedByArmor:F1}\n" +
                    $"  • <b>Final Damage (sau giáp):</b> {info.FinalDamage:F1}\n" +
                    $"  • <b>Absorbed by Shield:</b> {damageToShield:F1} (Khiên còn: {CurrentShield:F1})\n" +
                    $"  • <b>Actual Damage to HP:</b> {damageToHealth:F1}\n" +
                    $"  • <b>HP Remaining:</b> {CurrentHealth:F1} / {MaxHealth:F1} ({(CurrentHealth / Mathf.Max(1f, MaxHealth) * 100f):F1}%)\n" +
                    $"  • <b>Bùng Nổ Năng Lượng:</b> {burstStatus}\n" +
                    $"  • <b>Status:</b> {(IsAlive ? "<color=green>Alive</color>" : "<color=red>Dead</color>")}";

                Debug.Log(log);
            }
        }

        #region Burst Hooks Implementation

        protected override void OnBurstStart()
        {
            Debug.Log($"<color=orange>[ELITE: {name}] OnBurstStart: Bắt đầu giai đoạn Bùng nổ năng lượng!</color>");
        }

        protected override void OnBurstSuccess()
        {
            Debug.Log($"<color=green>[ELITE: {name}] OnBurstSuccess: Phá cơ chế Bùng nổ thành công! Nhận trạng thái Thưởng.</color>");
        }

        protected override void OnBurstFail()
        {
            Debug.Log($"<color=red>[ELITE: {name}] OnBurstFail: Hết giờ hoặc thất bại cơ chế! Nhận trạng thái Phạt.</color>");
        }

        #endregion

        #region Context Menu Actions - General

        [ContextMenu("1. Hồi đầy máu")]
        public void RestoreFullHealth()
        {
            if (Stats == null) return;
            Heal(Stats.MaxHealth);
            Debug.Log($"[{name}] Đã hồi đầy máu: {CurrentHealth:F1}/{MaxHealth:F1}");
        }

        [ContextMenu("2. Bật / Tắt Bất tử")]
        public void ToggleInvincible()
        {
            isInvincible = !isInvincible;
            Debug.Log($"[{name}] Bất tử (Invincible): {(isInvincible ? "<color=green>BẬT</color>" : "<color=red>TẮT</color>")}");
        }

        [ContextMenu("3. Bật / Tắt Liên tục hồi máu (+10%/s)")]
        public void ToggleContinuousHeal()
        {
            continuousHeal = !continuousHeal;
            Debug.Log($"[{name}] Liên tục hồi máu: {(continuousHeal ? "<color=green>BẬT</color>" : "<color=red>TẮT</color>")}");
        }

        [ContextMenu("4. Bật / Tắt Liên tục giảm máu (-10%/s)")]
        public void ToggleContinuousHpLoss()
        {
            continuousHpLoss = !continuousHpLoss;
            Debug.Log($"[{name}] Liên tục giảm máu: {(continuousHpLoss ? "<color=green>BẬT</color>" : "<color=red>TẮT</color>")}");
        }

        [ContextMenu("5. Bật / Tắt Log chi tiết sát thương")]
        public void ToggleLogDetailedDamage()
        {
            logDetailedDamage = !logDetailedDamage;
            Debug.Log($"[{name}] Log chi tiết sát thương: {(logDetailedDamage ? "<color=green>BẬT</color>" : "<color=red>TẮT</color>")}");
        }

        [ContextMenu("6. Trừ 25% Máu tối đa")]
        public void Damage25Percent() => DamagePercent(0.25f);

        [ContextMenu("7. Trừ 50% Máu tối đa (Đạt ngưỡng Bùng nổ)")]
        public void Damage50Percent() => DamagePercent(0.50f);

        [ContextMenu("8. Trừ 75% Máu tối đa")]
        public void Damage75Percent() => DamagePercent(0.75f);

        public void DamagePercent(float percent)
        {
            if (!IsAlive || MaxHealth <= 0f) return;
            float amount = MaxHealth * percent;
            TakeDamage(new DamageInfo(null, this, amount, isDirectToHealth: true, sourceType: DamageSourceType.Default));
            Debug.Log($"[{name}] Bị trừ {percent * 100}% HP ({amount:F1} HP). Còn lại: {CurrentHealth:F1}/{MaxHealth:F1}");
        }

        [ContextMenu("9. Hạ gục ngay lập tức")]
        public void KillInstantly()
        {
            if (!IsAlive) return;
            TakeDamage(new DamageInfo(null, this, CurrentHealth + 99999f, isDirectToHealth: true, sourceType: DamageSourceType.Default));
        }

        [ContextMenu("10. Hồi sinh ngay lập tức")]
        public void Revive()
        {
            Initialize(Stats);
            RestoreFullHealth();
            ResetBurstManual();
            Debug.Log($"<color=green>[ELITE: {name}] Đã hồi sinh với đầy đủ máu ({CurrentHealth:F1}/{MaxHealth:F1})!</color>");
        }

        [ContextMenu("11. Cấp khiên 500 HP trong 10s")]
        public void GrantShield500()
        {
            AddShield(500f, 10f);
            Debug.Log($"[{name}] Đã nhận khiên 500 HP trong 10s. Tổng khiên: {CurrentShield:F1}");
        }

        #endregion

        #region Context Menu Actions - Elite Specific (Energy Burst)

        [ContextMenu("ELITE: 1. Kích hoạt Bùng nổ năng lượng")]
        public void TestTriggerEnergyBurst()
        {
            TriggerEnergyBurstManual();
            Debug.Log($"<color=cyan>[ELITE: {name}] Đã kích hoạt Bùng nổ năng lượng! Trạng thái: {BurstState}, Thời gian: {BurstTimer:F1}s.</color>");
        }

        [ContextMenu("ELITE: 2. Phá thành công Bùng nổ năng lượng (Thưởng)")]
        public void TestForceBurstSuccess()
        {
            ForceBurstSuccess();
            Debug.Log($"<color=green>[ELITE: {name}] Phá thành công Bùng nổ! Sự kiện OnEnergyBurstSuccess đã phát ra. Trạng thái: {BurstState}.</color>");
        }

        [ContextMenu("ELITE: 3. Phá thất bại Bùng nổ năng lượng (Phạt)")]
        public void TestForceBurstFailure()
        {
            ForceBurstFailure();
            Debug.Log($"<color=red>[ELITE: {name}] Phá thất bại Bùng nổ! Trạng thái: {BurstState}.</color>");
        }

        [ContextMenu("ELITE: 4. Đặt lại trạng thái Bùng nổ năng lượng")]
        public void TestResetBurst()
        {
            ResetBurstManual();
            Debug.Log($"<color=grey>[ELITE: {name}] Đã đặt lại trạng thái Bùng nổ năng lượng về Inactive.</color>");
        }

        [ContextMenu("ELITE: 5. Đặt máu về 49% (Kích hoạt tự động)")]
        public void SetHpTo49Percent()
        {
            if (Stats == null || MaxHealth <= 0f) return;
            float targetHp = MaxHealth * 0.49f;
            float diff = CurrentHealth - targetHp;
            if (diff > 0f)
            {
                TakeDamage(new DamageInfo(null, this, diff, isDirectToHealth: true, sourceType: DamageSourceType.Default));
            }
            Debug.Log($"[{name}] Đặt HP về 49% ({CurrentHealth:F1}/{MaxHealth:F1}). Kiểm tra kích hoạt Bùng nổ tự động.");
        }

        #endregion
    }
}
