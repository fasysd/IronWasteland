using System.Collections;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// Hiệu ứng hình ảnh Sóng Thủy Triều (ThuyTrieuVFX) cho VFX_ThuyTrieuHitbox.
    /// Luồng sóng nước hình nón 4m góc 60° cuộn trào về phía trước.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThuyTrieuVFX : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer waveConeSprite;
        [SerializeField] private ParticleSystem waterSplashParticles;
        [SerializeField] private ParticleSystem foamParticles;

        [Header("Settings")]
        [SerializeField] private float radius = 4f;
        [SerializeField] private float duration = 0.6f;

        private void Start()
        {
            if (waterSplashParticles != null) waterSplashParticles.Play();
            if (foamParticles != null) foamParticles.Play();
            StartCoroutine(WaveRoutine());
        }

        private IEnumerator WaveRoutine()
        {
            float elapsed = 0f;
            Vector3 targetScale = new Vector3(radius * 2f, radius * 2f, 1f);

            if (waveConeSprite != null)
            {
                waveConeSprite.transform.localScale = Vector3.zero;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (waveConeSprite != null)
                {
                    float scaleT = Mathf.Sin(t * Mathf.PI * 0.5f);
                    waveConeSprite.transform.localScale = targetScale * scaleT;

                    float alpha = t > 0.6f ? (1f - t) / 0.4f : 1f;
                    Color c = waveConeSprite.color;
                    c.a = alpha;
                    waveConeSprite.color = c;
                }

                yield return null;
            }
        }
    }
}
