using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Phu kien mau dung cho test va kiem thu he thong 1 o phu kien moi.
    /// Vi du: Tang 15% Tan cong va 20% Phong thu.
    /// </summary>
    public class Accessory_Test : EquipmentBrain
    {
        protected override void OnEquipped(TankBrain owner)
        {
            Debug.Log($"[Accessory_Test] Da trang bi {DisplayName} cho Tank!");
        }

        public override void OnOwnerDealtDamage(DamageInfo info)
        {
            Debug.Log($"[Accessory_Test] Tank gay sat thuong: {info.FinalDamage:0.#} (Raw: {info.RawDamage:0.#})");
        }
    }
}
