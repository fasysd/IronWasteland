using System;
using System.Collections;
using IronWasteland.Enemies.ElementalReaper.Hitboxes;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// Hiệu ứng hình ảnh Tia Dung Nham (LavaBeamVFX) cho VFX_PhunDungNhamHitbox.
    /// Tia lửa dung nham đỏ rực 16m x 1.2m cuộn trào lửa đỏ cam và tàn tro.
    /// Tự động đồng bộ kích thước hiển thị khớp chính xác 100% với beamLength và beamWidth của BoxCollider2D.
    /// </summary>
    [DisallowMultipleComponent]
    public class LavaBeamVFX : MonoBehaviour, IronWasteland.Net.INetVisualGhostDriver
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer beamCoreSprite;
        [SerializeField] private ParticleSystem flameParticles;
        [SerializeField] private ParticleSystem emberParticles;

        [Header("Settings")]
        [SerializeField] private float beamLength = 16f;
        [SerializeField] private float beamWidth = 1.2f;
        [SerializeField] private float duration = 1.2f;

        private void Awake()
        {
            SyncSettingsFromHitbox();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncSettingsFromHitbox();
            if (!Application.isPlaying && beamCoreSprite != null)
            {
                beamCoreSprite.transform.localScale = CalculateAccurateScale();
            }
        }
#endif

        private void Start()
        {
            SyncSettingsFromHitbox();
            if (flameParticles != null)
            {
                var shape = flameParticles.shape;
                shape.position = new Vector3(beamLength * 0.5f, 0f, 0f);
                shape.scale = new Vector3(beamLength, beamWidth, 0.1f);
                flameParticles.Play();
            }
            if (emberParticles != null) emberParticles.Play();
            StartCoroutine(PulseRoutine());
        }

        public void SyncSettingsFromHitbox()
        {
            var hitbox = GetComponent<PhunDungNhamHitbox>();
            if (hitbox != null)
            {
                beamLength = hitbox.BeamLength;
                beamWidth = hitbox.BeamWidth;
            }
        }

        public Vector3 CalculateAccurateScale()
        {
            SyncSettingsFromHitbox();
            if (beamCoreSprite != null && beamCoreSprite.sprite != null)
            {
                Vector2 spriteBounds = beamCoreSprite.sprite.bounds.size;
                float sx = spriteBounds.x > 0.01f ? (beamLength / spriteBounds.x) : beamLength;
                float sy = spriteBounds.y > 0.01f ? (beamWidth / spriteBounds.y) : beamWidth;
                return new Vector3(sx, sy, 1f);
            }
            return new Vector3(beamLength, beamWidth, 1f);
        }

        private IEnumerator PulseRoutine()
        {
            float elapsed = 0f;
            Vector3 baseScale = CalculateAccurateScale();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (beamCoreSprite != null)
                {
                    // Rung nhẹ bề rộng tạo cảm giác áp lực dòng dung nham cuồn cuộn
                    float jitter = 1f + 0.08f * Mathf.Sin(Time.time * 50f);
                    beamCoreSprite.transform.localScale = new Vector3(baseScale.x, baseScale.y * jitter, 1f);

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
