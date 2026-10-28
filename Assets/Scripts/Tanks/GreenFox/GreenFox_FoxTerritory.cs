using UnityEngine;
using IronWasteland.Enemies;
using System.Collections.Generic;

namespace IronWasteland.Tanks.GreenFox
{
    [RequireComponent(typeof(CircleCollider2D))]
    public class GreenFox_FoxTerritory : MonoBehaviour
    {
        private TankBrain m_Owner;
        private float m_Duration;
        private float m_SlowAmount;
        private float m_SpeedBoostPercent;
        private float m_EnergyPerHit;
        private float m_MaxEnergyGain;

        private float m_TotalEnergyGained = 0f;
        private float m_ElapsedTime = 0f;

        private const string TerritorySpeedKey = "FoxTerritory_SpeedBoost";

        // Quản lý danh sách các xe tăng đang đứng trong vòng để cộng/trừ buff
        private readonly HashSet<TankBrain> m_TanksInside = new HashSet<TankBrain>();

        public void Initialize(TankBrain owner, float duration, float slowAmount, float speedBoostPercent, float energyPerHit, float maxEnergyGain)
        {
            m_Owner = owner;
            m_Duration = duration;
            m_SlowAmount = Mathf.Clamp01(slowAmount);
            m_SpeedBoostPercent = speedBoostPercent;
            m_EnergyPerHit = energyPerHit;
            m_MaxEnergyGain = maxEnergyGain;
            m_TotalEnergyGained = 0f;
            m_ElapsedTime = 0f;

            Destroy(gameObject, m_Duration);
        }

        private void Update()
        {
            m_ElapsedTime += Time.deltaTime;
            if (m_ElapsedTime >= m_Duration)
            {
                OnDestroyCleanup();
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            // 1. Làm chậm kẻ địch bước vào lãnh địa (Giảm 30% Tốc độ di chuyển)
            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null && enemy.Stats != null)
            {
                float slowValue = enemy.Stats.MoveSpeed * m_SlowAmount;
                enemy.Stats.MoveSpeed = Mathf.Max(0.1f, enemy.Stats.MoveSpeed - slowValue);
            }

            // 2. Tăng tốc cho Xe Tăng đồng minh hoặc Green Fox bước vào lãnh địa (Tăng 30% Tốc độ)
            TankBrain tank = targetRef.GetOwner<TankBrain>();
            if (tank != null && !m_TanksInside.Contains(tank))
            {
                m_TanksInside.Add(tank);

                TankStats speedBonus = TankStats.Zeroed();
                speedBonus.MoveSpeed = tank.BaseStats.MoveSpeed * m_SpeedBoostPercent;

                tank.AddBonusStats(TerritorySpeedKey, speedBonus);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            // Phục hồi lại tốc độ di chuyển cho kẻ địch khi ra khỏi vòng
            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null && enemy.Stats != null)
            {
                float slowValue = enemy.Stats.MoveSpeed * (m_SlowAmount / (1f - m_SlowAmount));
                enemy.Stats.MoveSpeed += slowValue;
            }

            // Gỡ bỏ buff tăng tốc di chuyển khi Xe Tăng rời lãnh địa
            TankBrain tank = targetRef.GetOwner<TankBrain>();
            if (tank != null && m_TanksInside.Contains(tank))
            {
                tank.RemoveBonusStats(TerritorySpeedKey);
                m_TanksInside.Remove(tank);
            }
        }

        /// <summary>
        /// Gọi hàm này khi bất kỳ đồng minh hoặc bản thân gây sát thương lên kẻ địch nằm trong vùng lãnh địa
        /// </summary>
        public void NotifyDamageDealtInTerritory()
        {
            if (m_Owner == null || m_TotalEnergyGained >= m_MaxEnergyGain) return;

            float energyToGain = Mathf.Min(m_EnergyPerHit, m_MaxEnergyGain - m_TotalEnergyGained);
            if (energyToGain > 0f)
            {
                m_Owner.GainEnergy(energyToGain);
                m_TotalEnergyGained += energyToGain;
                Debug.Log($"[FoxTerritory] Hồi {energyToGain} năng lượng cho Green Fox (Đã hồi tổng cộng: {m_TotalEnergyGained}/{m_MaxEnergyGain})");
            }
        }

        private void OnDestroyCleanup()
        {
            // Thu hồi buff tốc độ di chuyển của toàn bộ xe tăng vẫn còn đang đứng trong lãnh địa khi hết thời gian duy trì
            foreach (TankBrain tank in m_TanksInside)
            {
                if (tank != null)
                {
                    tank.RemoveBonusStats(TerritorySpeedKey);
                }
            }
            m_TanksInside.Clear();
        }

        private void OnDestroy()
        {
            OnDestroyCleanup();
        }
    }
}