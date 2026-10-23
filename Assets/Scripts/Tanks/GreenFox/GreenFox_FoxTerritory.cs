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
        private const string TerritorySlowKey = "FoxTerritory_Slow";

        public void Initialize(TankBrain owner, float duration, float slowAmount, float speedBoostAmount = 5f)
        {
            m_Owner = owner;
            m_Duration = duration;
            m_SlowAmount = Mathf.Clamp01(slowAmount); // Giá trị từ 0 đến 1 (ví dụ: 0.3 = giảm 30% tốc độ)
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

            // 1. Làm chậm kẻ địch bước vào vùng
            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null)
            {
                // Giảm tốc độ di chuyển của địch dựa trên MoveSpeed gốc nhân với tỷ lệ làm chậm m_SlowAmount
                float slowValue = enemy.Stats.MoveSpeed * m_SlowAmount;

                EnemyStats slowBonus = EnemyStats.Zeroed();
                slowBonus.MoveSpeed = -slowValue; // Giá trị âm để giảm tốc độ

                // Lưu ý: Nếu EnemyBrain chưa có hàm AddBonusStats giống TankBrain, 
                // bạn có thể điều chỉnh trực tiếp hoặc tạo cơ chế tương ứng trên EnemyStats.
                // Hoặc nếu bạn muốn trừ trực tiếp vào stat hiện tại:
                enemy.Stats.MoveSpeed = Mathf.Max(0.1f, enemy.Stats.MoveSpeed - slowValue);
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

            // Phục hồi tốc độ cho Kẻ địch khi rời khỏi vòng (nếu bạn chỉnh trực tiếp MoveSpeed)
            EnemyBrain enemy = targetRef.GetOwner<EnemyBrain>();
            if (enemy != null)
            {
                // Khôi phục lại tốc độ gốc bằng cách tính ngược lại lượng đã giảm
                float slowValue = enemy.Stats.MoveSpeed * m_SlowAmount;
                enemy.Stats.MoveSpeed += slowValue;
            }

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