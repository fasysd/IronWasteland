using System;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Bang chi so cua 1 Tank (chi con khung, chua toi uu hoa theo Level).
    /// Cac gia tri se duoc gan tu TankDefinition ScriptableObject.
    /// </summary>
    [Serializable]
    public class TankStats
    {
        [Header("Combat")]
        [Tooltip("Sat thuong co ban cua phao chinh.")]
        public float Attack = 10f;

        [Tooltip("Phan tram giam sat thuong nhan vao (0 = 0%, 0.3 = 30%).")]
        public float Defense;

        [Tooltip("He so tang sat thuong (1 = 100%).")]
        public float DamageMultiplier = 1f;

        [Tooltip("Phan tram xuyen qua pho bien (0 = khong xuyen).")]
        public float ArmorPenetration;

        [Header("Survivability")]
        [Tooltip("Luong mau toi da.")]
        public float MaxHealth = 100f;

        [Tooltip("Luong nang luong toi da.")]
        public float MaxEnergy = 50f;

        [Header("Mobility")]
        [Tooltip("Don vi/giay toi da.")]
        public float MoveSpeed = 3f;

        [Header("Utility")]
        [Tooltip("Phan tram giam hoi chieu (0.2 = -20% hoi chieu).")]
        public float CooldownReduction;

        public TankStats Clone() => (TankStats)MemberwiseClone();

        /// <summary>Moi chi so = 0. Dung cho "runtime stats" (buff/debuff chua co).</summary>
        public static TankStats Zeroed()
        {
            return new TankStats
            {
                Attack = 0f,
                Defense = 0f,
                DamageMultiplier = 0f,
                ArmorPenetration = 0f,
                MaxHealth = 0f,
                MaxEnergy = 0f,
                MoveSpeed = 0f,
                CooldownReduction = 0f,
            };
        }

        /// <summary>Cong don voi mot bang chi so khac (dung cho base + runtime).</summary>
        public TankStats Add(TankStats other)
        {
            if (other == null) return Clone();

            TankStats r = Clone();
            r.Attack += other.Attack;
            r.Defense += other.Defense;
            r.DamageMultiplier += other.DamageMultiplier;
            r.ArmorPenetration += other.ArmorPenetration;
            r.MaxHealth += other.MaxHealth;
            r.MaxEnergy += other.MaxEnergy;
            r.MoveSpeed += other.MoveSpeed;
            r.CooldownReduction += other.CooldownReduction;
            return r;
        }

        /// <summary>Cong truc tiep vao bang hien tai.</summary>
        public void AddTo(TankStats other)
        {
            if (other == null) return;

            Attack += other.Attack;
            Defense += other.Defense;
            DamageMultiplier += other.DamageMultiplier;
            ArmorPenetration += other.ArmorPenetration;
            MaxHealth += other.MaxHealth;
            MaxEnergy += other.MaxEnergy;
            MoveSpeed += other.MoveSpeed;
            CooldownReduction += other.CooldownReduction;
        }

        /// <summary>
        /// Do lai bang hien tai tru bang khac. Dung khi thoa trang bi
        /// (tru <c>statBonus</c> da cong) hoac goi y chieu nghich.
        /// </summary>
        public TankStats Subtract(TankStats other)
        {
            if (other == null) return Clone();

            TankStats r = Clone();
            r.Attack -= other.Attack;
            r.Defense -= other.Defense;
            r.DamageMultiplier -= other.DamageMultiplier;
            r.ArmorPenetration -= other.ArmorPenetration;
            r.MaxHealth -= other.MaxHealth;
            r.MaxEnergy -= other.MaxEnergy;
            r.MoveSpeed -= other.MoveSpeed;
            r.CooldownReduction -= other.CooldownReduction;
            return r;
        }

        /// <summary>Doi dau moi chi so. Add() + Negated() == Subtract().</summary>
        public TankStats Negated()
        {
            TankStats r = Clone();
            r.Attack = -r.Attack;
            r.Defense = -r.Defense;
            r.DamageMultiplier = -r.DamageMultiplier;
            r.ArmorPenetration = -r.ArmorPenetration;
            r.MaxHealth = -r.MaxHealth;
            r.MaxEnergy = -r.MaxEnergy;
            r.MoveSpeed = -r.MoveSpeed;
            r.CooldownReduction = -r.CooldownReduction;
            return r;
        }
    }
}
