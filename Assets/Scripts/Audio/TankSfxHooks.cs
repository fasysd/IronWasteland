using UnityEngine;
using IronWasteland.Core;
using IronWasteland.Tanks;

namespace IronWasteland.Audio
{
    /// <summary>
    /// Gan len prefab tank: nghe event cua TankBrain va phat SFX tuong ung.
    ///
    /// Vi sao khong nhet AudioSource vao TankBrain: Brain chi lo logic chien dau
    /// (Iron Rule 2), am thanh la viec cua component rieng. Muon tat tieng thi
    /// chi can bo tick o day, khong phai sua code chien dau.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TankBrain))]
    public class TankSfxHooks : MonoBehaviour
    {
        [SerializeField] private TankBrain brain;

        [Header("Bat/tat tung loai")]
        [SerializeField] private bool playSkillCast = true;
        [SerializeField] private bool playHit = true;
        [SerializeField] private bool playDeath = true;
        [SerializeField] private bool playHeal = true;
        [SerializeField] private bool playShield = true;

        [Header("Chong spam")]
        [Tooltip("Khoang cach toi thieu giua 2 tieng an don (giay).")]
        [SerializeField, Min(0f)] private float hitCooldown = 0.08f;

        private float m_NextHitTime;

        private void Awake()
        {
            if (brain == null) brain = GetComponent<TankBrain>();
        }

        private void OnEnable()
        {
            if (brain == null) return;
            brain.OnSkillUsedEvent += HandleSkillUsed;
            brain.OnTakeDamageEvent += HandleTakeDamage;
            brain.OnHealEvent += HandleHeal;
            brain.OnShieldChangedEvent += HandleShieldChanged;
            brain.OnDeathEvent += HandleDeath;
        }

        private void OnDisable()
        {
            if (brain == null) return;
            brain.OnSkillUsedEvent -= HandleSkillUsed;
            brain.OnTakeDamageEvent -= HandleTakeDamage;
            brain.OnHealEvent -= HandleHeal;
            brain.OnShieldChangedEvent -= HandleShieldChanged;
            brain.OnDeathEvent -= HandleDeath;
        }

        private void HandleSkillUsed(int skillIndex)
        {
            if (!playSkillCast) return;
            AudioManager.Play(SfxCategory.SkillCast);
        }

        private void HandleTakeDamage(DamageInfo info)
        {
            if (!playHit) return;
            if (Time.unscaledTime < m_NextHitTime) return;
            m_NextHitTime = Time.unscaledTime + hitCooldown;
            AudioManager.Play(SfxCategory.TankHit);
        }

        private void HandleHeal(float amount)
        {
            if (!playHeal || amount <= 0f) return;
            AudioManager.Play(SfxCategory.Heal, 0.7f);
        }

        private void HandleShieldChanged(float currentShield)
        {
            if (!playShield) return;
            AudioManager.Play(SfxCategory.Shield, 0.8f);
        }

        private void HandleDeath()
        {
            if (!playDeath) return;
            AudioManager.Play(SfxCategory.TankDeath);
        }
    }
}
