using System.Collections;
using IronWasteland.Core;
using UnityEngine;

namespace IronWasteland.Tanks.WaterB
{
    /// <summary>
    /// Script TEST: log khiên/giáp và tự gây sát thương lên xe mỗi giây để kiểm tra Bubble Shield.
    /// Gắn lên xe cần test. Bật/tắt bằng "Auto Damage" trong Inspector khi đang chạy.
    /// </summary>
    public class WaterB_ShieldDebug : MonoBehaviour
    {
        [Header("Auto Damage (test)")]
        [SerializeField] private bool autoDamage = true;
        [SerializeField, Min(0f)] private float damagePerTick = 20f;
        [SerializeField, Min(0.1f)] private float tickInterval = 1f;

        [Header("Hiển thị")]
        [SerializeField] private bool showDamageText = true;

        private TankBrain m_Tank;
        private Coroutine m_DamageRoutine;

        private void OnEnable()
        {
            m_Tank = GetComponent<TankBrain>();
            m_Tank.OnShieldChangedEvent += OnShieldChanged;
            m_Tank.OnTakeDamageEvent += OnTakeDamage;

            m_DamageRoutine = StartCoroutine(AutoDamageRoutine());
        }

        private void OnDisable()
        {
            if (m_DamageRoutine != null) StopCoroutine(m_DamageRoutine);

            if (m_Tank == null) return;
            m_Tank.OnShieldChangedEvent -= OnShieldChanged;
            m_Tank.OnTakeDamageEvent -= OnTakeDamage;
        }

        private IEnumerator AutoDamageRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(tickInterval); // đọc lại mỗi vòng nên chỉnh Inspector lúc chạy vẫn có tác dụng

                if (autoDamage && m_Tank != null && m_Tank.IsAlive && damagePerTick > 0f)
                {
                    m_Tank.TakeDamage(damagePerTick);
                }
            }
        }

        private void OnShieldChanged(float shield)
        {
            Debug.Log($"[ShieldDebug] Khiên còn: {shield} | Defense hiện tại: {m_Tank.Stats.Defense}");
        }

        private void OnTakeDamage(DamageInfo info)
        {
            Debug.Log($"[ShieldDebug] Sát thương gốc {info.RawDamage} -> sau giáp {info.FinalDamage} " +
                      $"-> khiên hấp thụ {info.AbsorbedByShield} -> trừ máu {info.FinalDamage - info.AbsorbedByShield} " +
                      $"| Máu {m_Tank.CurrentHealth}/{m_Tank.Stats.MaxHealth}");

            if (showDamageText) ShowDamageText(info);
        }

        /// <summary>Hiện số sát thương: phần khiên hấp thụ nằm cao hơn một chút, phần trừ máu nằm ngay trên xe.</summary>
        private void ShowDamageText(DamageInfo info)
        {
            if (TextDamageManager.Instance == null) return;

            Vector3 pos = m_Tank.transform.position;
            float absorbed = info.AbsorbedByShield;
            float healthLoss = info.FinalDamage - absorbed;

            if (healthLoss > 0.01f)
            {
                TextDamageManager.Instance.Show(healthLoss, pos);
            }

            if (absorbed > 0.01f)
            {
                TextDamageManager.Instance.Show(absorbed, pos + Vector3.up * 0.6f);
            }
        }
    }
}