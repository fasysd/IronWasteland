using UnityEngine;
using IronWasteland.Core;
using IronWasteland.Enemies;

namespace IronWasteland.Audio
{
    /// <summary>
    /// Gan len prefab quai: nghe event cua EnemyBrain va phat SFX.
    ///
    /// Luu y quan trong: phan lon ky nang tank goi enemy.TakeDamage(float) chu KHONG
    /// goi TankBrain.NotifyDealtDamage, nen event "tank gay sat thuong" khong ban ra.
    /// Vi vay tieng "trung dich" phai hook o phia quai (OnTakeDamageEvent) - day chinh la
    /// ly do component nay ton tai.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBrain))]
    public class EnemySfxHooks : MonoBehaviour
    {
        [SerializeField] private EnemyBrain brain;

        [Header("Bat/tat tung loai")]
        [SerializeField] private bool playHit = true;
        [SerializeField] private bool playDeath = true;
        [SerializeField] private bool playCrowdControl = true;

        [Header("Chong spam")]
        [Tooltip("Khoang cach toi thieu giua 2 tieng an don (giay) - DoT gay sat thuong lien tuc.")]
        [SerializeField, Min(0f)] private float hitCooldown = 0.1f;

        private float m_NextHitTime;

        private void Awake()
        {
            if (brain == null) brain = GetComponent<EnemyBrain>();
        }

        private void OnEnable()
        {
            if (brain == null) return;
            brain.OnTakeDamageEvent += HandleTakeDamage;
            brain.OnCrowdControlApplied += HandleCrowdControlApplied;
            brain.OnDeathEvent += HandleDeath;
        }

        private void OnDisable()
        {
            if (brain == null) return;
            brain.OnTakeDamageEvent -= HandleTakeDamage;
            brain.OnCrowdControlApplied -= HandleCrowdControlApplied;
            brain.OnDeathEvent -= HandleDeath;
        }

        private void HandleTakeDamage(DamageInfo info)
        {
            if (!playHit) return;
            if (Time.unscaledTime < m_NextHitTime) return;
            m_NextHitTime = Time.unscaledTime + hitCooldown;
            AudioManager.Play(SfxCategory.EnemyHit);
        }

        private void HandleCrowdControlApplied(CrowdControlType type)
        {
            if (!playCrowdControl) return;
            AudioManager.Play(SfxCategory.CrowdControl, 0.6f);
        }

        private void HandleDeath()
        {
            if (!playDeath) return;
            AudioManager.Play(SfxCategory.EnemyDeath);
        }
    }
}
