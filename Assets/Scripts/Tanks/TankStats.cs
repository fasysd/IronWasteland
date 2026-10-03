using System;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Bang chi so cua 1 Tank (chi con khung, chua toi uu hoa theo Level).
    /// Cac gia tri se duoc gan tu TankBrainFactory ScriptableObject.
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
    }
}