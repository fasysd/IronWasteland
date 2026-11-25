using System.Collections;
using IronWasteland.Enemies.ElementalReaper.Hitboxes;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// Hiệu ứng hình ảnh Sóng Thủy Triều (ThuyTrieuVFX) cho VFX_ThuyTrieuHitbox.
    /// Tự động đồng bộ kích thước hiển thị của WaveCone khớp 100% với bán kính (radius) và góc mở (coneAngle) của Collider.
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
        [SerializeField] private float coneAngle = 60f;
        [SerializeField] private float duration = 0.6f;

        private void Awake()
        {
            SyncSettingsFromHitbox();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncSettingsFromHitbox();
            if (!Application.isPlaying && waveConeSprite != null)
            {
                waveConeSprite.transform.localScale = CalculateAccurateScale();
            }
        }
#endif

        private void Start()
        {
            SyncSettingsFromHitbox();
            if (waterSplashParticles != null) waterSplashParticles.Play();
            if (foamParticles != null) foamParticles.Play();
            StartCoroutine(WaveRoutine());
        }

        public void SyncSettingsFromHitbox()
        {
            var hitbox = GetComponent<ThuyTrieuHitbox>();
            if (hitbox != null)
            {
                radius = hitbox.Radius;
                coneAngle = hitbox.ConeAngle;
            }
        }

        public Vector3 CalculateAccurateScale()
        {
            SyncSettingsFromHitbox();

            if (waveConeSprite != null && waveConeSprite.sprite != null)
            {
                Vector2 spriteBounds = waveConeSprite.sprite.bounds.size;
                float sx = spriteBounds.x > 0.01f ? (radius / spriteBounds.x) : radius;
                float halfAngleRad = coneAngle * 0.5f * Mathf.Deg2Rad;
                float targetWidth = 2f * radius * Mathf.Tan(halfAngleRad);
                float sy = spriteBounds.y > 0.01f ? (targetWidth / spriteBounds.y) : radius;
                return new Vector3(sx, sy, 1f);
            }

            return new Vector3(radius, radius, 1f);
        }

        private IEnumerator WaveRoutine()
        {
            float elapsed = 0f;
            Vector3 targetScale = CalculateAccurateScale();

            if (waveConeSprite != null)
            {
                waveConeSprite.transform.localScale = Vector3.zero;
            }

            // Đồng bộ tầm phụt hạt nước theo bán kính
            if (waterSplashParticles != null)
            {
                var main = waterSplashParticles.main;
                main.startSpeed = radius / Mathf.Max(0.1f, duration);
                var shape = waterSplashParticles.shape;
                shape.angle = coneAngle * 0.5f;
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
