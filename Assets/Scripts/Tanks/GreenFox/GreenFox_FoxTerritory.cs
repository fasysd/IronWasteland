using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies;
using UnityEngine;

namespace IronWasteland.Tanks.GreenFox
{
    /// <summary>
    /// Fox Territory (GDD): 6s, kẻ địch trong vùng giảm 30% tốc độ, đồng minh (kể cả Green Fox) tăng 30% tốc độ.
    /// Đồng minh gây sát thương trong vùng giúp Green Fox hồi 3 năng lượng, tối đa 30.
    ///
    /// Vùng ảnh hưởng = CircleCollider2D (Is Trigger) trên prefab.
    /// - Tốc độ đồng minh: AddBonusStats (percent MoveSpeed).
    /// - Tốc độ kẻ địch: GreenFox_EnemyMoveModifier (hệ số nhân, tự hoàn trả đúng).
    /// - Mỗi xe/kẻ địch được đếm theo số collider để không gỡ buff nhầm khi chỉ rời 1 collider.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class GreenFox_FoxTerritory : MonoBehaviour
    {
        private TankBrain m_Owner;
        private float m_SlowAmount;
        private float m_SpeedBoostPercent;
        private float m_EnergyPerHit;
        private float m_MaxEnergyGain;
        private float m_TotalEnergyGained;
        private string m_Key;

        private readonly Dictionary<EnemyBrain, int> m_EnemiesInside = new Dictionary<EnemyBrain, int>();
        private readonly Dictionary<TankBrain, int> m_TanksInside = new Dictionary<TankBrain, int>();

        public void Initialize(TankBrain owner, float duration, float slowAmount, float speedBoostPercent, float energyPerHit, float maxEnergyGain)
        {
            m_Owner = owner;
            m_SlowAmount = Mathf.Clamp01(slowAmount);
            m_SpeedBoostPercent = speedBoostPercent;
            m_EnergyPerHit = energyPerHit;
            m_MaxEnergyGain = maxEnergyGain;
            m_TotalEnergyGained = 0f;
            m_Key = $"GreenFox_Territory_{System.Guid.NewGuid():N}";

            Destroy(gameObject, duration);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            // Kẻ địch: giảm 30% tốc độ
            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null)
            {
                m_EnemiesInside.TryGetValue(enemy, out int count);
                m_EnemiesInside[enemy] = count + 1;

                if (count == 0) GreenFox_EnemyMoveModifier.Apply(enemy, m_Key, 1f - m_SlowAmount);
            }

            // Xe tăng (đồng minh hoặc Green Fox): tăng 30% tốc độ
            TankBrain tank = targetRef.GetOwner<TankBrain>();
            if (tank != null)
            {
                m_TanksInside.TryGetValue(tank, out int count);
                m_TanksInside[tank] = count + 1;

                if (count == 0)
                {
                    TankStats percent = TankStats.Zeroed();
                    percent.MoveSpeed = m_SpeedBoostPercent; // Final = Base * (1 + 0.3)
                    tank.AddBonusStats(m_Key, TankStats.Zeroed(), percent);

                    // Đồng minh khác gây sát thương -> Green Fox hồi năng lượng (Green Fox tự báo qua DamageEnemy)
                    if (tank != m_Owner) tank.OnDealDamageEvent += HandleAllyDealDamage;
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null && m_EnemiesInside.TryGetValue(enemy, out int ec))
            {
                ec--;
                if (ec <= 0)
                {
                    m_EnemiesInside.Remove(enemy);
                    GreenFox_EnemyMoveModifier.Remove(enemy, m_Key);
                }
                else m_EnemiesInside[enemy] = ec;
            }

            TankBrain tank = targetRef.GetOwner<TankBrain>();
            if (tank != null && m_TanksInside.TryGetValue(tank, out int tc))
            {
                tc--;
                if (tc <= 0)
                {
                    m_TanksInside.Remove(tank);
                    ReleaseTank(tank);
                }
                else m_TanksInside[tank] = tc;
            }
        }

        private void ReleaseTank(TankBrain tank)
        {
            if (tank == null) return;
            tank.RemoveBonusStats(m_Key);
            tank.OnDealDamageEvent -= HandleAllyDealDamage;
        }

        private void HandleAllyDealDamage(DamageInfo info)
        {
            NotifyDamageDealtInTerritory(null);
        }

        /// <summary>
        /// Gọi khi có đồng minh/Green Fox gây sát thương trong vùng. Truyền enemy để kiểm tra kẻ địch có nằm trong vùng;
        /// truyền null nếu nguồn báo không biết mục tiêu (xem như đã nằm trong vùng).
        /// </summary>
        public void NotifyDamageDealtInTerritory(EnemyBrain enemy = null)
        {
            if (m_Owner == null || m_TotalEnergyGained >= m_MaxEnergyGain) return;
            if (enemy != null && !m_EnemiesInside.ContainsKey(enemy)) return;

            float energyToGain = Mathf.Min(m_EnergyPerHit, m_MaxEnergyGain - m_TotalEnergyGained);
            if (energyToGain <= 0f) return;

            m_Owner.GainEnergy(energyToGain);
            m_TotalEnergyGained += energyToGain;
        }

        private void OnDestroy()
        {
            // Hết thời gian: gỡ toàn bộ buff/debuff còn lại
            foreach (var kv in m_TanksInside) ReleaseTank(kv.Key);
            foreach (var kv in m_EnemiesInside)
            {
                if (kv.Key != null) GreenFox_EnemyMoveModifier.Remove(kv.Key, m_Key);
            }

            m_TanksInside.Clear();
            m_EnemiesInside.Clear();
        }
    }
}