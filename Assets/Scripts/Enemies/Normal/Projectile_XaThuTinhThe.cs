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
        private readonly HashSet<Component> m_DamagedTargets = new HashSet<Component>();

        public override void Clear()
        {
            m_DamagedTargets.Clear();
            base.Clear();
        }

        protected override void OnAnyTargetsHit(HashSet<Component> targets)
        {
            foreach (Component target in targets)
            {
                if (!EnemyTargetUtility.IsTargetAlive(target)) continue;

                // Mỗi mục tiêu chỉ nhận sát thương đúng 1 lần từ viên đạn xuyên thấu này
                if (m_DamagedTargets.Add(target))
                {
                    // Phát event va chạm để Brain xử lý trừ máu; đạn KHÔNG biến mất, tiếp tục bay xuyên qua
                    NotifyAnyTargetHit(target);
                }
            }
        }

        protected override void OnTanksHit(HashSet<TankBrain> tanks)
        {
            foreach (TankBrain tank in tanks)
            {
                if (tank == null || !tank.IsAlive) continue;

                if (m_DamagedTargets.Add(tank))
                {
                    NotifyTargetHit(tank);
                }
            }
        }
    }
}
