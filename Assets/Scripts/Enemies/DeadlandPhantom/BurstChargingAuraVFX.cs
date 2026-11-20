using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// VFX 6: Vận Sức Bùng Nổ (Burst Charging Aura).
    /// Luồng năng lượng hắc ám cuộn trào từ mặt đất tụ về Boss
    /// và vòng cảnh báo đỏ tím mở rộng dưới chân, báo hiệu đòn hủy diệt sắp xảy ra.
    /// </summary>
    [DisallowMultipleComponent]
    public class BurstChargingAuraVFX : MonoBehaviour
    {
        [Header("Charging Settings")]
        [Tooltip("Thời gian vận sức (giây). Chuẩn GDD là 2 giây.")]
        [SerializeField, Min(0.2f)] private float chargeDuration = 2f;

        [Tooltip("Bán kính tối đa của vòng cảnh báo đỏ tím (m).")]
        [SerializeField, Min(1f)] private float warningRadius = 8f;

        [Tooltip("Tự động kích hoạt khi Start.")]
        [SerializeField] private bool autoPlayOnStart = false;

        [Tooltip("Tự động hủy GameObject sau khi vận sức kết thúc.")]
        [SerializeField] private bool autoDestroy = false;

        [Header("Components")]
        [SerializeField] private SpriteRenderer warningCircleSprite;
        [SerializeField] private SpriteRenderer coreChargingAuraSprite;
        [SerializeField] private ParticleSystem convergingDarkEnergyParticles;
        [SerializeField] private GameObject burstReleaseExplosionPrefab;

        [Header("Colors")]
        [SerializeField] private Color warningRedPurple = new Color(0.85f, 0.11f, 0.38f, 0.85f);
        [SerializeField] private Color voidCorePurple = new Color(0.29f, 0.078f, 0.549f, 0.9f);

        private Coroutine m_ChargeRoutine;
        private bool m_IsCharging;

        public float ChargeDuration => chargeDuration;
        public float WarningRadius => warningRadius;
        public bool IsCharging => m_IsCharging;

        private void Start()
        {
            if (autoPlayOnStart)
            {
                StartCharging();
            }
        }

        /// <summary>Bắt đầu chu trình vận sức tích tụ năng lượng.</summary>
        public void StartCharging(Action onChargeComplete = null)
        {
            StopCharging();
            m_ChargeRoutine = StartCoroutine(ChargeRoutine(onChargeComplete));
        }

        public void StopCharging()
        {
            if (m_ChargeRoutine != null)
            {
                StopCoroutine(m_ChargeRoutine);
                m_ChargeRoutine = null;
            }

            m_IsCharging = false;

            if (warningCircleSprite != null) warningCircleSprite.gameObject.SetActive(false);
            if (coreChargingAuraSprite != null) coreChargingAuraSprite.gameObject.SetActive(false);
            if (convergingDarkEnergyParticles != null) convergingDarkEnergyParticles.Stop();
        }

        private IEnumerator ChargeRoutine(Action onChargeComplete)
        {
            m_IsCharging = true;
            float elapsed = 0f;
            float targetDiameter = warningRadius * 2f;

            // Chuẩn hóa tỉ lệ scale của vòng cảnh báo theo sprite unit size
            float warnUnitSize = 1f;
            if (warningCircleSprite != null && warningCircleSprite.sprite != null && warningCircleSprite.sprite.rect.width > 0f)
            {
                warnUnitSize = warningCircleSprite.sprite.rect.width / warningCircleSprite.sprite.pixelsPerUnit;
            }
            float baseWarnScale = targetDiameter / Mathf.Max(0.01f, warnUnitSize);

            if (warningCircleSprite != null)
            {
                warningCircleSprite.gameObject.SetActive(true);
                warningCircleSprite.transform.localScale = Vector3.zero;
            }

            if (coreChargingAuraSprite != null)
            {
                coreChargingAuraSprite.gameObject.SetActive(true);
            }

            if (convergingDarkEnergyParticles != null)
            {
                var shape = convergingDarkEnergyParticles.shape;
                shape.radius = warningRadius;
                convergingDarkEnergyParticles.Play();
            }

            Vector3 bossOriginalPos = coreChargingAuraSprite != null ? coreChargingAuraSprite.transform.localPosition : Vector3.zero;

            while (elapsed < chargeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / chargeDuration);

                // Warning circle expands to targetDiameter
                float currentScale = baseWarnScale * t;
                // Pulsing frequency increases as t -> 1
                float freq = Mathf.Lerp(4f, 20f, t);

                if (warningCircleSprite != null)
                {
                    warningCircleSprite.transform.localScale = new Vector3(currentScale, currentScale, 1f);

                    float pulse = 0.6f + 0.4f * Mathf.Sin(elapsed * freq);
                    Color c = warningRedPurple;
                    c.a = pulse;
                    warningCircleSprite.color = c;
                }

                // Core aura shakes with increasing intensity
                if (coreChargingAuraSprite != null)
                {
                    float shake = Mathf.Lerp(0.02f, 0.15f, t);
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * shake;
                    coreChargingAuraSprite.transform.localPosition = bossOriginalPos + (Vector3)offset;

                    float coreScale = 1.8f + 0.4f * Mathf.Sin(elapsed * freq * 1.5f);
                    coreChargingAuraSprite.transform.localScale = new Vector3(coreScale, coreScale, 1f);
                }

                yield return null;
            }

            // Restore position
            if (coreChargingAuraSprite != null)
            {
                coreChargingAuraSprite.transform.localPosition = bossOriginalPos;
            }

            // Explosion on charge complete
            if (burstReleaseExplosionPrefab != null)
            {
                Instantiate(burstReleaseExplosionPrefab, transform.position, Quaternion.identity);
            }

            m_IsCharging = false;
            onChargeComplete?.Invoke();

            if (autoDestroy)
            {
                yield return new WaitForSeconds(0.2f);
                Destroy(gameObject);
            }
            else
            {
                StopCharging();
            }
        }
    }
}
