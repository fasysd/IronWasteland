using System.Collections;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// Hiệu ứng hình ảnh Độc Quật (DocQuatVFX) cho VFX_DocQuatHitbox.
    /// Lưỡi trăng khuyết bán nguyệt 180° bán kính 4m màu xanh lục độc tím.
    /// </summary>
    [DisallowMultipleComponent]
    public class DocQuatVFX : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer crescentSprite;
        [SerializeField] private ParticleSystem poisonMistParticles;
        [SerializeField] private ParticleSystem sparkParticles;

        [Header("Settings")]
        [SerializeField] private float radius = 4f;
        [SerializeField] private float duration = 0.45f;

        private void Start()
        {
            if (poisonMistParticles != null) poisonMistParticles.Play();
            if (sparkParticles != null) sparkParticles.Play();
            StartCoroutine(SlashRoutine());
        }

        private IEnumerator SlashRoutine()
        {
            float elapsed = 0f;
            Vector3 targetScale = new Vector3(radius * 2f, radius * 2f, 1f);

            if (crescentSprite != null)
            {
                crescentSprite.transform.localScale = Vector3.zero;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (crescentSprite != null)
                {
                    // Bung nhanh ra 4m trong 50% thời gian đầu (Ease-out)
                    float scaleT = Mathf.Sin(t * Mathf.PI * 0.5f);
                    crescentSprite.transform.localScale = targetScale * scaleT;

                    // Mờ dần về cuối
                    float alpha = t > 0.6f ? (1f - t) / 0.4f : 1f;
                    Color c = crescentSprite.color;
                    c.a = alpha;
                    crescentSprite.color = c;
                }

                yield return null;
            }
        }
    }
}
