using IronWasteland.Core.Effects;
using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Bubble Shield (GDD): khiên = 120% ATK + 10% Giới hạn máu, duy trì 4s.
    /// Khi khiên vỡ HOẶC hết thời gian -> hồi máu cho mục tiêu (100% ATK + 5% Giới hạn máu).
    ///
    /// Phần khiên thật do TankBrain.AddShield() (ShieldEffect) xử lý: hấp thụ sát thương, hết hạn, cộng dồn nhiều lớp...
    /// Component này chỉ làm 2 việc: hiển thị hình bong bóng bám theo xe, và hồi máu khi lớp khiên của nó bị gỡ.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterB_BubbleShield : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private SpriteRenderer shieldSpriteRenderer;

        private TankBrain_WaterB m_Caster;
        private TankBrain m_Target;
        private float m_HealOnBreak;
        private string m_EffectId;
        private bool m_Finished;

        public void Initialize(TankBrain_WaterB caster, TankBrain target, float shieldAmount, float duration, float healOnBreak)
        {
            if (target == null || shieldAmount <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            m_Caster = caster;
            m_Target = target;
            m_HealOnBreak = healOnBreak;
            m_EffectId = $"WaterB_BubbleShield_{System.Guid.NewGuid():N}"; // mỗi lần cast là 1 lớp khiên riêng

            if (shieldSpriteRenderer == null) shieldSpriteRenderer = GetComponent<SpriteRenderer>();

            // Bám theo xe được bảo vệ
            transform.SetParent(target.transform, false);
            transform.localPosition = Vector3.zero;

            // Đăng ký TRƯỚC khi thêm khiên để không bỏ lỡ sự kiện gỡ khiên
            m_Target.OnEffectRemovedEvent += HandleEffectRemoved;

            ShieldEffect shield = m_Target.AddShield(
                shieldAmount,
                duration > 0f ? duration : 4f,
                m_EffectId,
                caster,
                "Bubble Shield");

            if (shield == null)
            {
                Cleanup();
                Destroy(gameObject);
            }
        }

        /// <summary>TankBrain phát sự kiện này khi khiên bị phá hết, hết hạn hoặc bị xóa.</summary>
        private void HandleEffectRemoved(StatusEffect effect)
        {
            if (m_Finished || effect == null || effect.EffectId != m_EffectId) return;
            m_Finished = true;

            if (m_Target != null && m_HealOnBreak > 0f)
            {
                if (m_Caster != null) m_Caster.HealAlly(m_Target, m_HealOnBreak);
                else m_Target.Heal(m_HealOnBreak, false);
            }

            Cleanup();
            Destroy(gameObject);
        }

        private void Cleanup()
        {
            if (m_Target != null) m_Target.OnEffectRemovedEvent -= HandleEffectRemoved;
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}