using System;
using System.Collections;
using IronWasteland.Core;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// VFX 2: Sóng Xung Kích Triệu Hồi (SoulSummon Pulse).
    /// Vòng xung kích ma pháp tím lan rộng bán kính 10m dưới chân Boss,
    /// tạo hiệu ứng sương mù u linh bao phủ mặt đất và tàn hồn bay lơ lửng.
    /// </summary>
    [DisallowMultipleComponent]
    public class SoulSummonPulseVFX : MonoBehaviour, IronWasteland.Net.INetVisualGhostDriver
    {
        [Header("Shockwave Pulse Settings")]
        [Tooltip("Bán kính tối đa của vòng xung kích (m). Chuẩn GDD là 10m.")]
        [SerializeField, Min(1f)] private float maxRadius = 10f;

        [Tooltip("Thời gian sóng xung kích lan ra hết bán kính tối đa (giây).")]
        [SerializeField, Min(0.2f)] private float pulseDuration = 0.9f;

        [Tooltip("Thời gian sương mù u linh tồn tại trên mặt đất sau khi kích hoạt (giây).")]
        [SerializeField, Min(0.5f)] private float mistDuration = 4.5f;

        [Tooltip("Tự động kích hoạt khi Start.")]
        [SerializeField] private bool autoPlayOnStart = true;

        [Tooltip("Tự động hủy GameObject sau khi hiệu ứng sương mù tàn.")]
        [SerializeField] private bool autoDestroy = true;

        [Header("Combat Debuff Settings")]
        [Tooltip("Tự động làm chậm Tank khi chạm vòng sóng xung kích.")]
        [SerializeField] private bool applySlowDebuff = true;

        [Tooltip("Tỷ lệ làm chậm Tank (0.2 = 20%).")]
        [SerializeField, Range(0f, 1f)] private float slowRatio = 0.2f;

        [Tooltip("Thời gian làm chậm Tank (giây). GDD: 5s.")]
        [SerializeField, Min(0.1f)] private float slowDuration = 5f;

        [Header("Components")]
        [SerializeField] private SpriteRenderer pulseRingSprite;
        [SerializeField] private ParticleSystem groundMistParticles;
        [SerializeField] private ParticleSystem soulWispsParticles;

        [Header("Color Palette")]
        [SerializeField] private Color voidPurple = new Color(0.29f, 0.078f, 0.549f, 1f); // #4A148C
        [SerializeField] private Color ghostlyCyan = new Color(0f, 0.898f, 1f, 1f);       // #00E5FF

        public float MaxRadius => maxRadius;
        public float PulseDuration => pulseDuration;

        private Coroutine m_PulseRoutine;

        private void Start()
        {
            if (autoPlayOnStart)
            {
                Play();
            }
        }

        public void Play(Action onPulseComplete = null)
        {
            if (m_PulseRoutine != null)
            {
                StopCoroutine(m_PulseRoutine);
            }
            m_PulseRoutine = StartCoroutine(PulseRoutine(onPulseComplete));
        }

        private IEnumerator PulseRoutine(Action onPulseComplete)
        {
            float elapsed = 0f;
            float targetDiameter = maxRadius * 2f;

            // Chuẩn hóa scale theo kích thước thật của sprite
            float spriteUnitSize = 1f;
            if (pulseRingSprite != null && pulseRingSprite.sprite != null && pulseRingSprite.sprite.rect.width > 0f)
            {
                spriteUnitSize = pulseRingSprite.sprite.rect.width / pulseRingSprite.sprite.pixelsPerUnit;
            }
            float baseScale = targetDiameter / Mathf.Max(0.01f, spriteUnitSize);

            if (groundMistParticles != null)
            {
                // Cấu hình vùng phát sương mù rộng 10m
                var shape = groundMistParticles.shape;
                shape.radius = maxRadius * 0.9f;
                groundMistParticles.Play();
            }

            if (soulWispsParticles != null)
            {
                var shape = soulWispsParticles.shape;
                shape.radius = maxRadius * 0.85f;
                soulWispsParticles.Play();
            }

            if (pulseRingSprite != null)
            {
                pulseRingSprite.gameObject.SetActive(true);
                pulseRingSprite.transform.localScale = Vector3.zero;
            }

            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pulseDuration);

                // Quad ease-out expansion
                float easeOut = 1f - Mathf.Pow(1f - t, 2f);
                float currentScale = baseScale * easeOut;

                if (pulseRingSprite != null)
                {
                    pulseRingSprite.transform.localScale = new Vector3(currentScale, currentScale, 1f);

                    // Fade out ring in the second half
                    float alpha = t < 0.3f ? Mathf.Lerp(0f, 1f, t / 0.3f) : Mathf.Lerp(1f, 0f, (t - 0.3f) / 0.7f);
                    Color c = pulseRingSprite.color;
                    c.a = alpha;
                    pulseRingSprite.color = c;
                }

                yield return null;
            }

            if (pulseRingSprite != null)
            {
                pulseRingSprite.gameObject.SetActive(false);
            }

            onPulseComplete?.Invoke();

            if (autoDestroy)
            {
                yield return new WaitForSeconds(mistDuration);
                if (groundMistParticles != null) groundMistParticles.Stop();
                if (soulWispsParticles != null) soulWispsParticles.Stop();
                yield return new WaitForSeconds(1.5f);
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!applySlowDebuff || other == null) return;
            if (other.GetComponentInParent<EnemyBrain>() != null) return;

            TankBrain tank = ResolveTank(other);
            if (tank != null && tank.IsAlive)
            {
                TankSlowDebuff.Apply(tank, slowRatio, slowDuration);
            }
        }

        private static TankBrain ResolveTank(Collider2D col)
        {
            if (col == null) return null;
            if (col.TryGetComponent<ColliderOwnerRef>(out var targetRef) && targetRef != null)
            {
                if (targetRef.TryGetOwner<TankBrain>(out var directBrain) && directBrain != null)
                    return directBrain;
                if (targetRef.Owner is TankBrain b)
                    return b;
                if (targetRef.Owner is Component comp)
                {
                    var compBrain = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                    if (compBrain != null) return compBrain;
                }
            }
            return col.GetComponent<TankBrain>() ?? col.GetComponentInParent<TankBrain>();
        }
    }
}
