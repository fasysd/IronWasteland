using System.Collections;
using IronWasteland.Core;
using IronWasteland.Core.Effects;
using UnityEngine;

namespace IronWasteland.Enemies.Test
{
    /// <summary>
    /// Bia tap ban / Target Dummy ke dich Thuong dung de kiem thu he thong sat thuong, phu kien va chi so.
    /// - Khong the di chuyen (CanMove = false, Rigidbody dung yen).
    /// - Cung cap day du ContextMenu de thao tac truc tiep trong Editor / PlayMode.
    /// - Ho tro log chi tiet sat thuong (nguon, kieu, gia tri goc, giam boi giap, khien, mau con lai).
    /// </summary>
    [DisallowMultipleComponent]
    public class NormalEnemyTest : NormalEnemyBrain
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
                    MaxHealth = 1000f,
                    Attack = 20f,
                    Defense = 40f,
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

        protected override void OnBrainTick(float deltaTime)
        {
            // Dummy bat dong, khong di chuyen hay tim duong
        }

        protected override void OnDeath()
        {
            Debug.Log($"<color=red>[{name}] Đã bị hạ gục!</color>");
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
                    Debug.Log($"<color=yellow>[{name}] Đang BẤT TỬ! Bỏ qua sát thương từ {info.Attacker?.name ?? "Unknown"} ({info.SourceType}).</color>");
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
                string log =
                    $"<b><color=#00FFAA>[Damage Log: {name}]</color></b>\n" +
                    $"  • <b>Attacker:</b> {attackerName}\n" +
                    $"  • <b>Source Type:</b> <color=cyan>{info.SourceType}</color> " +
                    $"(IsNormalSkill: {info.IsNormalSkill}, IsTankSkill: {info.IsTankSkill}, IsAccessory: {info.IsAccessoryDamage}, IsDoT: {info.IsDamageOverTime})\n" +
                    $"  • <b>Raw Damage:</b> {info.RawDamage:F1} | <b>Armor Pen:</b> {info.ArmorPenetration:F1}\n" +
                    $"  • <b>Target Defense:</b> {defenseAtHit:F1} → <b>Giảm bởi giáp:</b> {reducedByArmor:F1}\n" +
                    $"  • <b>Final Damage (sau giáp):</b> {info.FinalDamage:F1}\n" +
                    $"  • <b>Absorbed by Shield:</b> {damageToShield:F1} (Khiên còn: {CurrentShield:F1})\n" +
                    $"  • <b>Actual Damage to HP:</b> {damageToHealth:F1}\n" +
                    $"  • <b>HP Remaining:</b> {CurrentHealth:F1} / {MaxHealth:F1} ({(CurrentHealth / Mathf.Max(1f, MaxHealth) * 100f):F1}%)\n" +
                    $"  • <b>Status:</b> {(IsAlive ? "<color=green>Alive</color>" : "<color=red>Dead</color>")}";

                Debug.Log(log);
            }
        }

        #region Context Menu Actions

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

        [ContextMenu("7. Trừ 50% Máu tối đa")]
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
            Debug.Log($"<color=green>[{name}] Đã hồi sinh với đầy đủ máu ({CurrentHealth:F1}/{MaxHealth:F1})!</color>");
        }

        [ContextMenu("11. Cấp khiên 200 HP trong 10s")]
        public void GrantShield200()
        {
            AddShield(200f, 10f);
            Debug.Log($"[{name}] Đã nhận khiên 200 HP trong 10s. Tổng khiên: {CurrentShield:F1}");
        }

        [ContextMenu("12. Tăng 100 Giáp trong 10s")]
        public void AddTemporaryDefense100()
        {
            StartCoroutine(TemporaryDefenseRoutine(100f, 10f));
        }

        private IEnumerator TemporaryDefenseRoutine(float amount, float duration)
        {
            if (Stats == null) yield break;
            Stats.Defense += amount;
            Debug.Log($"[{name}] +{amount} Giáp trong {duration}s! Giáp hiện tại: {Stats.Defense:F1}");
            yield return new WaitForSeconds(duration);
            if (Stats != null)
            {
                Stats.Defense = Mathf.Max(0f, Stats.Defense - amount);
                Debug.Log($"[{name}] Hết hiệu lực tăng giáp. Giáp hiện tại: {Stats.Defense:F1}");
            }
        }

        #endregion
    }
}
