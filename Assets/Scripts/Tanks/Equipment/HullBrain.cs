using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Vo xe -> gan vao slot <see cref="EquipmentSlot.Hull"/> (Than Xe).
    ///
    /// Ke thua diem chung cua <see cref="EquipmentBrain"/> (them chi so + kha nang bi dong).
    /// Lo nay chi dinh nghia passive: cong them % MaxHealth va % Defense tinh tu BaseStats.
    /// </summary>
    public class HullBrain : EquipmentBrain
    {
        [Tooltip("Phan tram tang MaxHealth tinh tu BaseStats. 0.1 = 10%.")]
        [SerializeField, Range(0f, 1f)] private float healthPercent;

        [Tooltip("Phan tram tang Defense tinh tu BaseStats. 0.1 = 10%.")]
        [SerializeField, Range(0f, 1f)] private float defensePercent;

        /// <summary>Cong them % MaxHealth + % Defense — base luon la BaseStats (khong tinh statBonus).</summary>
        protected override TankStats BuildPassiveBonus(TankBrain owner)
        {
            TankStats bonus = TankStats.Zeroed();
            bonus.MaxHealth += owner.BaseStats.MaxHealth * healthPercent;
            bonus.Defense += owner.BaseStats.Defense * defensePercent;
            return bonus;
        }

        private void OnValidate()
        {
            healthPercent = Mathf.Clamp01(healthPercent);
            defensePercent = Mathf.Clamp01(defensePercent);
        }
    }
}
