using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Trang bi sung -> gan vao slot <see cref="EquipmentSlot.Weapon"/> (Nong Phao).
    ///
    /// Ke thua diem chung cua <see cref="EquipmentBrain"/> (them chi so + kha nang bi dong).
    /// Lo nay chi dinh nghia passive: cong them % Attack tinh tu BaseStats cua tank.
    /// </summary>
    public class WeaponBrain : EquipmentBrain
    {
        [Tooltip("Phan tram tang Attack tinh tu BaseStats. 0.1 = 10%. Khong dung bonus lam base.")]
        [SerializeField, Range(0f, 1f)] private float attackPercent;

        /// <summary>Cong them % Attack — base luon la BaseStats (khong tinh statBonus).</summary>
        protected override TankStats BuildPassiveBonus(TankBrain owner)
        {
            TankStats bonus = TankStats.Zeroed();
            bonus.Attack += owner.BaseStats.Attack * attackPercent;
            return bonus;
        }

        private void OnValidate()
        {
            attackPercent = Mathf.Clamp01(attackPercent);
        }
    }
}
