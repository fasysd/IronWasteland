using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    [DisallowMultipleComponent]
    public class WaterB_BubbleShield : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private SpriteRenderer shieldSpriteRenderer;

        private Transform m_TargetTransform;
        private TankBrain m_TargetTank;
        private float m_ShieldCapacity;
        private float m_Duration;
        private float m_HealOnBreak;
        private bool m_IsActive;

        private const string ShieldBonusKey = "BubbleShield_DefenseBonus";

        public void Initialize(Transform target, float capacity, float duration, float healOnBreak, float defenseBonusAmount = 0.2f)
        {
            m_TargetTransform = target;
            m_TargetTank = target.GetComponent<TankBrain>();
            m_ShieldCapacity = capacity;
            m_Duration = duration > 0f ? duration : 5f;
            m_HealOnBreak = healOnBreak;
            m_IsActive = true;

            if (shieldSpriteRenderer == null)
            {
                shieldSpriteRenderer = GetComponent<SpriteRenderer>();
            }

            // Gắn khiên đi theo xe tăng mục tiêu
            transform.position = target.position;
            transform.SetParent(target);

            // 1. Tăng chỉ số Defense thêm 0.2 (+20%)
            if (m_TargetTank != null)
            {
                TankStats bonus = TankStats.Zeroed();
                bonus.Defense = defenseBonusAmount;
                m_TargetTank.AddBonusStats(ShieldBonusKey, bonus);
                Debug.Log($"[BubbleShield] Đã cộng thêm {defenseBonusAmount * 100}% Defense cho {m_TargetTank.name}");
            }

            // Bắt đầu đếm ngược thời gian duy trì khiên bằng Coroutine an toàn
            StartCoroutine(ShieldTimerRoutine());
        }

        private IEnumerator ShieldTimerRoutine()
        {
            yield return new WaitForSeconds(m_Duration);
            BreakShield();
        }

        private void LateUpdate()
        {
            if (!m_IsActive || m_TargetTransform == null)
            {
                return;
            }

            transform.position = m_TargetTransform.position;
        }

        public void TakeShieldDamage(float damage)
        {
            if (!m_IsActive) return;

            m_ShieldCapacity -= damage;
            Debug.Log($"[BubbleShield] Khiên chịu {damage} sát thương. Độ bền còn lại: {m_ShieldCapacity}");

            if (m_ShieldCapacity <= 0f)
            {
                BreakShield();
            }
        }

        private void BreakShield()
        {
            if (!m_IsActive) return;
            m_IsActive = false;

            if (m_TargetTank != null)
            {
                // 2. Gỡ bỏ chỉ số Defense cộng thêm khi khiên vỡ/hết hạn
                m_TargetTank.RemoveBonusStats(ShieldBonusKey);
                Debug.Log($"[BubbleShield] Đã gỡ bỏ chỉ số Defense của khiên khỏi {m_TargetTank.name}");

                // 3. Thực hiện hồi máu trực tiếp (Thử gọi trực tiếp hàm Heal hoặc qua Reflection nếu cần)
                ExecuteHealing();
            }

            Destroy(gameObject);
        }

        private void ExecuteHealing()
        {
            if (m_TargetTank == null) return;

            bool healSuccess = false;

            // Thử gọi phương thức Heal tiêu chuẩn nếu TankBrain có hỗ trợ
            System.Reflection.MethodInfo healMethod = m_TargetTank.GetType().GetMethod("Heal", new System.Type[] { typeof(float) });
            if (healMethod != null)
            {
                healMethod.Invoke(m_TargetTank, new object[] { m_HealOnBreak });
                healSuccess = true;
            }
            else
            {
                // Fallback dự phòng: Nếu TankBrain của bạn đặt tên hàm hồi máu khác, hãy gọi thông qua phương thức cộng máu công khai tương đương
                // Hoặc nếu TankStats có MaxHealth, ta có thể log ra để kiểm tra
                Debug.LogWarning("[BubbleShield] Không tìm thấy method Heal(float) công khai trong TankBrain!");
            }

            if (healSuccess)
            {
                TextDamageManager.Instance?.Show(m_HealOnBreak, m_TargetTransform.position);
                Debug.Log($"[BubbleShield] Khiên vỡ/hết hạn! Hồi phục thành công {m_HealOnBreak} HP cho {m_TargetTank.name}");
            }
        }
    }
}