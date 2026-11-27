using System.Collections;
using IronWasteland.Enemies.ElementalReaper.Hitboxes;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// Hiệu ứng hình ảnh Độc Quật (DocQuatVFX) cho VFX_DocQuatHitbox.
    /// Lưỡi trăng khuyết bán nguyệt 180° bán kính khớp chính xác với radius của Collider.
    /// </summary>
    [DisallowMultipleComponent]
    public class DocQuatVFX : MonoBehaviour, IronWasteland.Net.INetVisualGhostDriver
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer crescentSprite;
        [SerializeField] private ParticleSystem poisonMistParticles;
        [SerializeField] private ParticleSystem sparkParticles;

        [Header("Settings")]
        [SerializeField] private float radius = 4f;
        [SerializeField] private float duration = 0.45f;

        private void Awake()
        {
            SyncSettingsFromHitbox();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncSettingsFromHitbox();
            if (!Application.isPlaying && crescentSprite != null)
            {
                crescentSprite.transform.localScale = CalculateAccurateScale();
            }
        }
#endif

        private void Start()
        {
            SyncSettingsFromHitbox();
            if (poisonMistParticles != null)
            {
                var shape = poisonMistParticles.shape;
                shape.radius = radius;
                poisonMistParticles.Play();
            }
            if (sparkParticles != null) sparkParticles.Play();
            StartCoroutine(SlashRoutine());
        }

        public void SyncSettingsFromHitbox()
        {
            var hitbox = GetComponent<DocQuatHitbox>();
            if (hitbox != null)
            {
                radius = hitbox.Radius;
            }
        }

        public Vector3 CalculateAccurateScale()
        {
            SyncSettingsFromHitbox();

            if (crescentSprite != null && crescentSprite.sprite != null)
            {
                Vector2 spriteBounds = crescentSprite.sprite.bounds.size;
                float sx = spriteBounds.x > 0.01f ? (radius / spriteBounds.x) : radius;
                float sy = spriteBounds.y > 0.01f ? ((radius * 2f) / spriteBounds.y) : radius;
                return new Vector3(sx, sy, 1f);
            }

            return new Vector3(radius, radius, 1f);
        }

        private IEnumerator SlashRoutine()
        {
            float elapsed = 0f;
            Vector3 targetScale = CalculateAccurateScale();

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
                    // Bung nhanh trong 50% thời gian đầu (Ease-out)
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
