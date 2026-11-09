using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Bubble Protector (GDD): vòm nước lớn, đồng minh bên trong được +25 Phòng thủ và +25% Tấn công.
    /// Khi hết 6s, mọi đồng minh còn bên trong được hồi máu (100% ATK + 6% Giới hạn máu của Water B).
    ///
    /// Vùng ảnh hưởng = CircleCollider2D (Is Trigger) đã cài trên prefab.
    /// Buff cộng qua TankBrain.AddBonusStats (flat Defense + percent Attack).
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class WaterB_BubbleProtectorDome : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private SpriteRenderer domeSpriteRenderer;

        private TankBrain_WaterB m_Caster;
        private float m_Duration;
        private float m_DefenseBonus;
        private float m_AttackBonusPercent;
        private float m_HealOnEnd;
        private string m_BonusKey;
        private bool m_IsActive;

        // Số collider của từng xe đang nằm trong vòm (xe có nhiều collider vẫn tính là "còn trong vòm")
        private readonly Dictionary<TankBrain, int> m_Inside = new Dictionary<TankBrain, int>();

        public void Initialize(TankBrain_WaterB caster, float duration, float defenseBonus,
                               float attackBonusPercent, float healOnEnd)
        {
            m_Caster = caster;
            m_Duration = duration > 0f ? duration : 6f;
            m_DefenseBonus = defenseBonus;
            m_AttackBonusPercent = attackBonusPercent;
            m_HealOnEnd = healOnEnd;
            m_BonusKey = $"WaterB_BubbleProtector_{System.Guid.NewGuid():N}";
            m_IsActive = true;

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

            StartCoroutine(DomeLifeRoutine());
        }

        private IEnumerator DomeLifeRoutine()
        {
            yield return new WaitForSeconds(m_Duration);

            // Hồi máu + gỡ buff TRƯỚC khi hủy vòm
            EndDome();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!m_IsActive) return;

            TankBrain tank = collision.GetComponentInParent<TankBrain>();
            if (tank == null) return;

            m_Inside.TryGetValue(tank, out int count);
            m_Inside[tank] = count + 1;

            if (count == 0) ApplyBuff(tank); // collider đầu tiên của xe này chạm vào vòm
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!m_IsActive) return;

            TankBrain tank = collision.GetComponentInParent<TankBrain>();
            if (tank == null || !m_Inside.TryGetValue(tank, out int count)) return;

            count--;
            if (count <= 0)
            {
                RemoveBuff(tank);
                m_Inside.Remove(tank);
            }
            else
            {
                m_Inside[tank] = count;
            }
        }

        private void ApplyBuff(TankBrain tank)
        {
            TankStats flat = TankStats.Zeroed();
            flat.Defense = m_DefenseBonus;          // +25 Phòng thủ (cộng phẳng)

            TankStats percent = TankStats.Zeroed();
            percent.Attack = m_AttackBonusPercent;  // +25% Tấn công

            tank.AddBonusStats(m_BonusKey, flat, percent);
        }

        private void RemoveBuff(TankBrain tank)
        {
            if (tank != null) tank.RemoveBonusStats(m_BonusKey);
        }

        private void EndDome()
        {
            if (!m_IsActive) return;
            m_IsActive = false;

            foreach (var kv in m_Inside)
            {
                TankBrain tank = kv.Key;
                if (tank == null) continue;

                RemoveBuff(tank);

                if (m_Caster != null) m_Caster.HealAlly(tank, m_HealOnEnd);
                else tank.Heal(m_HealOnEnd);
            }

            m_Inside.Clear();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // Dự phòng: vòm bị xóa đột ngột thì vẫn gỡ buff (không hồi máu)
            if (!m_IsActive) return;
            m_IsActive = false;

            foreach (var kv in m_Inside) RemoveBuff(kv.Key);
            m_Inside.Clear();
        }
    }
}