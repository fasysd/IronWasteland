using System;
using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Bang chi so cua 1 ke dich. Theo mo ta he thong:
    /// - Attack: xac dinh luong sat thuong gay ra
    /// - MaxHealth: xac dinh luong mau toi da
    /// - Defense: xac dinh luong giam sat thuong nhan vao tu Tank
    /// - MoveSpeed: xac dinh toc do di chuyen cua ke dich
    ///
    /// Designer chinh truc tiep tren prefab (hoac gui tu definition sau nay).
    /// </summary>
    [Serializable]
    public class EnemyStats
    {
        [Header("Combat")]
        [Tooltip("Sat thuong gay ra cho Tank / nguoi choi.")]
        public float Attack = 10f;

        [Tooltip("Phan tram giam sat thuong nhan vao (0 = 0%, 0.3 = 30%).")]
        public float Defense;

        [Header("Survivability")]
        [Tooltip("Luong mau toi da.")]
        public float MaxHealth = 50f;

        [Header("Mobility")]
        [Tooltip("Don vi / giay toi da.")]
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