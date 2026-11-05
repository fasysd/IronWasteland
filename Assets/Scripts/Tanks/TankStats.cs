using System;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Bang chi so cua 1 Tank theo Tai lieu thiet ke Game (GDD).
    /// </summary>
    [Serializable]
    public class TankStats
    {
        public const float MaxCooldownReduction = 0.60f; // Toi da 60%
        public const float MaxArmorPenetration = 1.0f;   // Toi da 100%

        [Header("Combat")]
        [Tooltip("Sat thuong co ban cua phao chinh.")]
        public float Attack = 10f;

        [Tooltip("Diem phong thu (flat stat) giam sat thuong nhan vao theo cong thuc Logarit.")]
        public float Defense;

        [Tooltip("Ti le sat thuong them (0 = 0%, 0.15 = +15%).")]
        public float DamageRate;

        [Tooltip("Ti le xuyen giap (0..1, toi da 100%).")]
        public float ArmorPenetration;

        [Header("Survivability")]
        [Tooltip("Luong mau toi da.")]
        public float MaxHealth = 100f;

        [Tooltip("Luong nang luong toi da.")]
        public float MaxEnergy = 50f;

        [Header("Mobility")]
        [Tooltip("Toc do di chuyen (don vi/giay).")]
        public float MoveSpeed = 3f;

        [Header("Utility")]
        [Tooltip("Ti le giam hoi chieu (0..0.6, toi da 60%).")]
        public float CooldownReduction;

        [Obsolete("Dung DamageRate thay the.")]
        public float DamageMultiplier
        {
            get => DamageRate;
            set => DamageRate = value;
        }

        public TankStats Clone() => (TankStats)MemberwiseClone();

        /// <summary>Moi chi so = 0. Dung cho bonus stats khoi tao.</summary>
        public static TankStats Zeroed()
        {
            return new TankStats
            {
                Attack = 0f,
                Defense = 0f,
                DamageRate = 0f,
                ArmorPenetration = 0f,
                MaxHealth = 0f,
                MaxEnergy = 0f,
                MoveSpeed = 0f,
                CooldownReduction = 0f,
            };
        }

        /// <summary>Cong don voi mot bang chi so khac.</summary>
        public TankStats Add(TankStats other)
        {
            if (other == null) return Clone();

            TankStats r = Clone();
            r.Attack += other.Attack;
            r.Defense += other.Defense;
            r.DamageRate += other.DamageRate;
            r.ArmorPenetration = Mathf.Clamp(r.ArmorPenetration + other.ArmorPenetration, 0f, MaxArmorPenetration);
            r.MaxHealth += other.MaxHealth;
            r.MaxEnergy += other.MaxEnergy;
            r.MoveSpeed += other.MoveSpeed;
            r.CooldownReduction = Mathf.Clamp(r.CooldownReduction + other.CooldownReduction, 0f, MaxCooldownReduction);
            return r;
        }

        /// <summary>Cong truc tiep vao bang hien tai.</summary>
        public void AddTo(TankStats other)
        {
            if (other == null) return;

            Attack += other.Attack;
            Defense += other.Defense;
            DamageRate += other.DamageRate;
            ArmorPenetration = Mathf.Clamp(ArmorPenetration + other.ArmorPenetration, 0f, MaxArmorPenetration);
            MaxHealth += other.MaxHealth;
            MaxEnergy += other.MaxEnergy;
            MoveSpeed += other.MoveSpeed;
            CooldownReduction = Mathf.Clamp(CooldownReduction + other.CooldownReduction, 0f, MaxCooldownReduction);
        }

        /// <summary>Tru chi so.</summary>
        public TankStats Subtract(TankStats other)
        {
            if (other == null) return Clone();

            TankStats r = Clone();
            r.Attack -= other.Attack;
            r.Defense -= other.Defense;
            r.DamageRate -= other.DamageRate;
            r.ArmorPenetration = Mathf.Clamp(r.ArmorPenetration - other.ArmorPenetration, 0f, MaxArmorPenetration);
            r.MaxHealth -= other.MaxHealth;
            r.MaxEnergy -= other.MaxEnergy;
            r.MoveSpeed -= other.MoveSpeed;
            r.CooldownReduction = Mathf.Clamp(r.CooldownReduction - other.CooldownReduction, 0f, MaxCooldownReduction);
            return r;
        }

        /// <summary>Doi dau moi chi so.</summary>
        public TankStats Negated()
        {
            TankStats r = Clone();
            r.Attack = -r.Attack;
            r.Defense = -r.Defense;
            r.DamageRate = -r.DamageRate;
            r.ArmorPenetration = -r.ArmorPenetration;
            r.MaxHealth = -r.MaxHealth;
            r.MaxEnergy = -r.MaxEnergy;
            r.MoveSpeed = -r.MoveSpeed;
            r.CooldownReduction = -r.CooldownReduction;
            return r;
        }

        /// <summary>
        /// Tinh toan chi so cuoi cung (Final Stats) tu Chi so co ban (Base Stats),
        /// Chi so cong phang (Flat Bonus) va Chi so cong phan tram (Percent Bonus):
        /// Final = Base * (1 + PercentBonus) + FlatBonus.
        /// Sau do clamp gioi han tran (CDR toi da 60%, Xuyen giap toi da 100%).
        /// </summary>
        public static TankStats CalculateFinalStats(TankStats baseStats, TankStats flatBonus, TankStats percentBonus)
        {
            if (baseStats == null) return Zeroed();
            flatBonus = flatBonus ?? Zeroed();
            percentBonus = percentBonus ?? Zeroed();

            TankStats result = new TankStats();
            result.Attack = Mathf.Max(0f, baseStats.Attack * (1f + percentBonus.Attack) + flatBonus.Attack);
            result.Defense = Mathf.Max(0f, baseStats.Defense * (1f + percentBonus.Defense) + flatBonus.Defense);
            result.MaxHealth = Mathf.Max(1f, baseStats.MaxHealth * (1f + percentBonus.MaxHealth) + flatBonus.MaxHealth);
            result.MaxEnergy = Mathf.Max(0f, baseStats.MaxEnergy * (1f + percentBonus.MaxEnergy) + flatBonus.MaxEnergy);
            result.MoveSpeed = Mathf.Max(0f, baseStats.MoveSpeed * (1f + percentBonus.MoveSpeed) + flatBonus.MoveSpeed);

            // Cac chi so ty le
            result.DamageRate = Mathf.Max(0f, baseStats.DamageRate + percentBonus.DamageRate + flatBonus.DamageRate);
            result.ArmorPenetration = Mathf.Clamp(baseStats.ArmorPenetration + percentBonus.ArmorPenetration + flatBonus.ArmorPenetration, 0f, MaxArmorPenetration);
            result.CooldownReduction = Mathf.Clamp(baseStats.CooldownReduction + percentBonus.CooldownReduction + flatBonus.CooldownReduction, 0f, MaxCooldownReduction);

            return result;
        }
    }
}
