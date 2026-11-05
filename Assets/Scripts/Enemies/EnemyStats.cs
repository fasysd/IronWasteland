using System;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Bang chi so cua 1 ke dich theo Tai lieu thiet ke Game (GDD):
    /// - Attack: xac dinh luong sat thuong gay ra
    /// - MaxHealth: xac dinh luong mau toi da
    /// - Defense: diem phong thu (flat stat), giam sat thuong nhan tu Tank theo cong thuc Logarit
    /// - MoveSpeed: xac dinh toc do di chuyen cua ke dich
    /// </summary>
    [Serializable]
    public class EnemyStats
    {
        [Header("Combat")]
        [Tooltip("Sat thuong gay ra cho Tank / nguoi choi.")]
        public float Attack = 10f;

        [Tooltip("Diem phong thu (flat stat), giam sat thuong nhan tu Tank theo cong thuc Logarit.")]
        public float Defense;

        [Header("Survivability")]
        [Tooltip("Luong mau toi da.")]
        public float MaxHealth = 50f;

        [Header("Mobility")]
        [Tooltip("Toc do di chuyen (don vi / giay).")]
        public float MoveSpeed = 2f;

        public EnemyStats Clone() => (EnemyStats)MemberwiseClone();

        /// <summary>Moi chi so = 0.</summary>
        public static EnemyStats Zeroed()
        {
            return new EnemyStats
            {
                Attack = 0f,
                Defense = 0f,
                MaxHealth = 0f,
                MoveSpeed = 0f,
            };
        }
    }
}
