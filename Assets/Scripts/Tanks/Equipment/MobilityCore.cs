using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Bo banh / xich -> gan vao slot <see cref="EquipmentSlot.Mobility"/> (Bo Banh).
    ///
    /// Ke thua diem chung cua <see cref="EquipmentBrain"/> (them chi so + kha nang bi dong).
    /// Day la lop ABSTRACT (framework) - khong the truc tiep gan len GameObject,
    /// phai dung lop con cu the (vi du MobilityCore_Test).
    /// Lo nay chi dinh nghia passive: cong them % MoveSpeed tinh tu BaseStats.
    /// </summary>
    public abstract class MobilityCore : EquipmentBrain
    {
        [Tooltip("Phan tram tang MoveSpeed tinh tu BaseStats. 0.1 = 10%.")]
        [SerializeField, Range(0f, 1f)] private float moveSpeedPercent;

        /// <summary>Cong them % MoveSpeed — base luon la BaseStats (khong tinh statBonus).</summary>
        protected override TankStats BuildPassiveBonus(TankBrain owner)
        {
            TankStats bonus = TankStats.Zeroed();
            bonus.MoveSpeed += owner.BaseStats.MoveSpeed * moveSpeedPercent;
            return bonus;
        }

        private void OnValidate()
        {
            moveSpeedPercent = Mathf.Clamp01(moveSpeedPercent);
        }
    }
}