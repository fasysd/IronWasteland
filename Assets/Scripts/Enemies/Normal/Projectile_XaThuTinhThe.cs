using System.Collections.Generic;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Đạn xuyên thấu của Xạ Thủ Tinh Thể.
    /// Bay thẳng về phía trước, xuyên qua mọi mục tiêu xe tăng và gây sát thương (mỗi mục tiêu 1 lần).
    /// </summary>
    [DisallowMultipleComponent]
    public class Projectile_XaThuTinhThe : EnemyProjectileBase
    {
        private readonly HashSet<TankBrain> m_DamagedTanks = new HashSet<TankBrain>();

        public override void Clear()
        {
            m_DamagedTanks.Clear();
            base.Clear();
        }

        protected override void OnTanksHit(HashSet<TankBrain> tanks)
        {
            foreach (TankBrain tank in tanks)
            {
                if (tank == null || !tank.IsAlive) continue;

                // Mỗi xe tăng chỉ nhận sát thương đúng 1 lần từ viên đạn xuyên thấu này
                if (m_DamagedTanks.Add(tank))
                {
                    // Phát event va chạm để Brain xử lý trừ máu và hiển thị TextDamage
                    // Lưu ý: Đạn KHÔNG biến mất (không gọi Despawn), tiếp tục bay xuyên qua
                    NotifyTargetHit(tank);
                }
            }
        }
    }
}
