using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// Hiệu ứng hình ảnh Tia Dung Nham (LavaBeamVFX) cho VFX_PhunDungNhamHitbox.
    /// Tia lửa dung nham đỏ rực 16m x 1.2m cuộn trào lửa đỏ cam và tàn tro.
    /// </summary>
    [DisallowMultipleComponent]
    public class LavaBeamVFX : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer beamCoreSprite;
        [SerializeField] private ParticleSystem flameParticles;
        [SerializeField] private ParticleSystem emberParticles;

        [Header("Settings")]
        [SerializeField] private float beamLength = 16f;
        [SerializeField] private float beamWidth = 1.2f;
        [SerializeField] private float duration = 1.2f;

        private void Start()
        {
            if (flameParticles != null) flameParticles.Play();
            if (emberParticles != null) emberParticles.Play();
            StartCoroutine(PulseRoutine());
        }

        private IEnumerator PulseRoutine()
        {
            float elapsed = 0f;
            Vector3 targetScale = new Vector3(beamLength, beamWidth, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (beamCoreSprite != null)
                {
                    // Rung nhẹ bề rộng tạo cảm giác áp lực dòng dung nham cuồn cuộn
                    float jitter = 1f + 0.08f * Mathf.Sin(Time.time * 50f);
                    beamCoreSprite.transform.localScale = new Vector3(beamLength, beamWidth * jitter, 1f);

                    float alpha = t > 0.8f ? (1f - t) / 0.2f : 1f;
                    Color c = beamCoreSprite.color;
                    c.a = alpha;
                    beamCoreSprite.color = c;
                }

                yield return null;
            }
        }
    }
}
