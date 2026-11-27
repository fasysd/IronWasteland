using System.Collections;
using IronWasteland.Enemies.ElementalReaper.Hitboxes;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// Hiệu ứng hình ảnh Cột Lốc Xoáy Lửa (FireTornadoVFX) cho VFX_FireTornadoZone.
    /// Cột lửa xoáy bốc lên cuồn cuộn tàn tro bán kính khớp chính xác 100% với radius của CircleCollider2D.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireTornadoVFX : MonoBehaviour, IronWasteland.Net.INetVisualGhostDriver
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer tornadoSprite;
        [SerializeField] private ParticleSystem vortexParticles;
        [SerializeField] private ParticleSystem emberParticles;

        [Header("Settings")]
        [SerializeField] private float radius = 2.0f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float duration = 1.5f;

        private void Awake()
        {
            SyncSettingsFromHitbox();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncSettingsFromHitbox();
            if (!Application.isPlaying && tornadoSprite != null)
            {
                tornadoSprite.transform.localScale = CalculateAccurateScale();
            }
        }
#endif

        private void Start()
        {
            SyncSettingsFromHitbox();
            if (vortexParticles != null)
            {
                var shape = vortexParticles.shape;
                shape.radius = radius;
                vortexParticles.Play();
            }
            if (emberParticles != null) emberParticles.Play();
            StartCoroutine(AnimateRoutine());
        }

        public void SyncSettingsFromHitbox()
        {
            var zone = GetComponent<FireTornadoZone>();
            if (zone != null)
            {
                radius = zone.Radius;
            }
        }

        public Vector3 CalculateAccurateScale()
        {
            SyncSettingsFromHitbox();
            if (tornadoSprite != null && tornadoSprite.sprite != null)
            {
                Vector2 spriteBounds = tornadoSprite.sprite.bounds.size;
                float targetDiameter = radius * 2f;
                float sx = spriteBounds.x > 0.01f ? (targetDiameter / spriteBounds.x) : targetDiameter;
                float sy = spriteBounds.y > 0.01f ? (targetDiameter / spriteBounds.y) : targetDiameter;
                return new Vector3(sx, sy, 1f);
            }
            return new Vector3(radius * 2f, radius * 2f, 1f);
        }

        private IEnumerator AnimateRoutine()
        {
            float elapsed = 0f;
            Vector3 targetScale = CalculateAccurateScale();
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
