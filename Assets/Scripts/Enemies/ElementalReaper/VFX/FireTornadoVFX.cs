using System.Collections;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// Hiệu ứng hình ảnh Cột Lốc Xoáy Lửa (FireTornadoVFX) cho VFX_FireTornadoZone.
    /// Cột lửa xoáy bốc lên cuồn cuộn tàn tro bán kính 2m.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireTornadoVFX : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer tornadoSprite;
        [SerializeField] private ParticleSystem vortexParticles;
        [SerializeField] private ParticleSystem emberParticles;

        [Header("Settings")]
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float duration = 1.5f;

        private void Start()
        {
            if (vortexParticles != null) vortexParticles.Play();
            if (emberParticles != null) emberParticles.Play();
            StartCoroutine(AnimateRoutine());
        }

        private IEnumerator AnimateRoutine()
        {
            float elapsed = 0f;
            Vector3 targetScale = tornadoSprite != null ? tornadoSprite.transform.localScale : Vector3.one;
            if (tornadoSprite != null) tornadoSprite.transform.localScale = Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (tornadoSprite != null)
                {
                    // Scale up rapidly in first 20%
                    float scaleT = t < 0.2f ? t / 0.2f : (t > 0.8f ? (1f - t) / 0.2f : 1f);
                    tornadoSprite.transform.localScale = targetScale * scaleT;
                }

                yield return null;
            }
        }

        private void Update()
        {
            if (tornadoSprite != null)
            {
                tornadoSprite.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            }
        }
    }
}
