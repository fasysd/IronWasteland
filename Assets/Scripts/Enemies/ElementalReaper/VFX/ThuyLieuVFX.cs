using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// VFX 3: Thủy Liệu Trị Liệu (VFX_ThuyLieu).
    /// Vòng xoáy nước bao bọc Boss kèm bong bóng ngọc bích hồi phục.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThuyLieuVFX : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private ParticleSystem waterSwirlParticles;
        [SerializeField] private ParticleSystem jadeBubbleParticles;
        [SerializeField] private SpriteRenderer swirlSprite;

        [Header("Settings")]
        [SerializeField, Min(0.2f)] private float duration = 2.0f;
        [SerializeField] private float rotationSpeed = 360f;
        [SerializeField] private bool autoDestroy = true;

        private Coroutine m_Routine;

        private void Start()
        {
            Play();
        }

        public void Play(Action onComplete = null)
        {
            if (m_Routine != null) StopCoroutine(m_Routine);
            m_Routine = StartCoroutine(AnimateRoutine(onComplete));
        }

        private IEnumerator AnimateRoutine(Action onComplete)
        {
            if (waterSwirlParticles != null) waterSwirlParticles.Play();
            if (jadeBubbleParticles != null) jadeBubbleParticles.Play();

            float elapsed = 0f;
            Vector3 startScale = swirlSprite != null ? swirlSprite.transform.localScale : Vector3.one;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (swirlSprite != null)
                {
                    swirlSprite.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
                    // Fade out in last 30% of duration
                    float alpha = t > 0.7f ? (1f - t) / 0.3f : 1f;
                    Color c = swirlSprite.color;
                    c.a = alpha * 0.85f;
                    swirlSprite.color = c;
                }

                yield return null;
            }

            onComplete?.Invoke();

            if (autoDestroy)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
        }
    }
}
