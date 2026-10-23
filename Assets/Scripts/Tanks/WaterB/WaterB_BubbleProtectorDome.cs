using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class WaterB_BubbleProtectorDome : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private SpriteRenderer domeSpriteRenderer;

        private float m_Duration;
        private float m_HealOnEnd;
        private float m_DefenseBonus;

        private readonly HashSet<TankBrain> m_InsideTanks = new HashSet<TankBrain>();
        private readonly Dictionary<TankBrain, string> m_TankBonusKeys = new Dictionary<TankBrain, string>();
        private bool m_IsActive = false;

        public void Initialize(float duration, float healOnEnd, float defenseBonus = 1.0f)
        {
            m_Duration = duration > 0f ? duration : 6f;
            m_HealOnEnd = healOnEnd;
            m_DefenseBonus = defenseBonus;
            m_IsActive = true;

            // Thay vì dùng Destroy có sẵn thời gian, ta dùng Coroutine để kiểm soát chính xác thứ tự thực thi
            StartCoroutine(DomeLifeRoutine());
        }

        private IEnumerator DomeLifeRoutine()
        {
            // Chờ đúng thời gian tồn tại của vòm
            yield return new WaitForSeconds(m_Duration);

            // Xử lý hiệu ứng kết thúc (gỡ giáp + hồi máu) TRƯỚC KHI hủy đối tượng
            TriggerEndEffect();

            // Sau khi đã hồi máu xong xuôi mới xóa GameObject
            Destroy(gameObject);
        }

        private void TriggerEndEffect()
        {
            if (!m_IsActive) return;
            m_IsActive = false;

            // Gỡ buff giáp và hồi máu cho tất cả xe tăng còn đứng bên trong
            foreach (var tank in m_InsideTanks)
            {
                if (tank != null)
                {
                    RemoveTankBuff(tank);

                    tank.Heal(m_HealOnEnd);
                    TextDamageManager.Instance?.Show(m_HealOnEnd, tank.transform.position);
                    Debug.Log($"[BubbleProtector] Vòm tan vỡ! Hồi phục {m_HealOnEnd} HP cho {tank.name}. HP hiện tại: {tank.CurrentHealth}");
                }
            }

            m_InsideTanks.Clear();
            m_TankBonusKeys.Clear();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!m_IsActive) return;

            TankBrain tank = collision.GetComponentInParent<TankBrain>();
            if (tank != null && !m_InsideTanks.Contains(tank))
            {
                m_InsideTanks.Add(tank);

                string bonusKey = "BubbleProtector_Def_" + tank.GetHashCode();
                m_TankBonusKeys[tank] = bonusKey;

                TankStats bonus = TankStats.Zeroed();
                bonus.Defense = m_DefenseBonus; // +100% Defense
                tank.AddBonusStats(bonusKey, bonus);

                Debug.Log($"[BubbleProtector] Xe tăng {tank.name} bước vào vòm, nhận +100% Defense.");
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            TankBrain tank = collision.GetComponentInParent<TankBrain>();
            if (tank != null && m_InsideTanks.Contains(tank))
            {
                RemoveTankBuff(tank);
                m_InsideTanks.Remove(tank);
                Debug.Log($"[BubbleProtector] Xe tăng {tank.name} rời khỏi vòm.");
            }
        }

        private void RemoveTankBuff(TankBrain tank)
        {
            if (tank != null && m_TankBonusKeys.TryGetValue(tank, out string key))
            {
                tank.RemoveBonusStats(key);
                m_TankBonusKeys.Remove(tank);
            }
        }

        private void OnDestroy()
        {
            // Dự phòng trường hợp vòm bị xóa đột ngột bởi script khác bên ngoài
            if (m_IsActive)
            {
                TriggerEndEffect();
            }
        }
    }
}