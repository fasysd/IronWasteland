using UnityEngine;
using IronWasteland.Enemies;

namespace IronWasteland.Tanks.GreenFox
{
    [RequireComponent(typeof(CircleCollider2D))]
    public class GreenFox_FoxTerritory : MonoBehaviour
    {
        private TankBrain m_Owner;
        private float m_Duration;
        private float m_SlowAmount;
        private float m_SpeedBoostAmount;
        private float m_ElapsedTime;

        // Định danh key riêng cho lãnh địa này
        private const string TerritorySpeedKey = "FoxTerritory_SpeedBoost";

        public void Initialize(TankBrain owner, float duration, float slowAmount, float speedBoostAmount = 5f)
        {
            m_Owner = owner;
            m_Duration = duration;
            m_SlowAmount = Mathf.Clamp01(slowAmount);
            m_SpeedBoostAmount = speedBoostAmount;
            m_ElapsedTime = 0f;

            // Tự hủy sau khi hết thời gian tồn tại của lãnh địa
            Destroy(gameObject, m_Duration);
        }

        private void Update()
        {
            m_ElapsedTime += Time.deltaTime;
            if (m_ElapsedTime >= m_Duration)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            // 1. Làm chậm kẻ địch (nếu cần)
            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null)
            {
                // enemy.ApplySlow(m_SlowAmount);
            }

            // 2. Tăng tốc cho BẤT KỲ Xe Tăng nào (Bản thân hoặc Đồng minh) bước vào vòng
            TankBrain tank = targetRef.GetOwner<TankBrain>();
            if (tank != null)
            {
                // Tạo một TankStats mới chuyên dùng cho Buff Tốc Độ
                TankStats speedBonus = TankStats.Zeroed();
                speedBonus.MoveSpeed = m_SpeedBoostAmount; // Giá trị = 5

                // Gọi trực tiếp hàm AddBonusStats của lớp cha TankBrain
                tank.AddBonusStats(TerritorySpeedKey, speedBonus);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collision);
            if (targetRef == null) return;

            // Gỡ bỏ stat bonus tốc độ khi bất kỳ Xe Tăng nào rời khỏi vòng
            TankBrain tank = targetRef.GetOwner<TankBrain>();
            if (tank != null)
            {
                // Gọi hàm RemoveBonusStats của TankBrain để hệ thống tự Recalculate lại
                tank.RemoveBonusStats(TerritorySpeedKey);
            }
        }
    }
}