using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// VFX 1: Vết Chém Tử Địa (DeadlandSlash).
    /// Vệt kiếm khí / lưỡi liềm tím xoay 360 độ (bán kính 4m) vung chém chớp nhoáng
    /// kèm tàn khói đen hư không và particle phát nổ khi chém trúng.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeadlandSlashVFX : MonoBehaviour
    {
        [Header("Slash Arc Configuration")]
        [Tooltip("Bán kính vết chém (m). Chuẩn GDD là 4m.")]
        [SerializeField, Min(0.5f)] private float radius = 4f;

        [Tooltip("Thời gian vung chém xoay 360 độ (giây).")]
        [SerializeField, Min(0.05f)] private float slashDuration = 0.35f;

        [Tooltip("Tự động phát hiệu ứng khi được Spawn/Enable.")]
        [SerializeField] private bool autoPlayOnStart = true;

        [Tooltip("Tự động hủy GameObject sau khi hiệu ứng kết thúc.")]
        [SerializeField] private bool autoDestroy = true;

        [Header("Components")]
        [SerializeField] private SpriteRenderer slashSprite;
        [SerializeField] private ParticleSystem voidSmokeParticles;
        [SerializeField] private ParticleSystem sparkParticles;
        [SerializeField] private GameObject impactPrefab;

        [Header("Color Palette")]
        [SerializeField] private Color voidDarkColor = new Color(0.165f, 0.031f, 0.271f, 1f); // #2A0845
        [SerializeField] private Color voidPurpleColor = new Color(0.29f, 0.078f, 0.549f, 1f); // #4A148C
        [SerializeField] private Color ghostlyCyanColor = new Color(0f, 0.898f, 1f, 1f);       // #00E5FF

        public float Radius => radius;
        public float SlashDuration => slashDuration;

        private Coroutine m_SlashRoutine;

        private void Start()
        {
            if (autoPlayOnStart)
            {
                Play();
            }
        }

        /// <summary>Kích hoạt vung chém tử địa 360 độ.</summary>
        public void Play(Action onComplete = null)
        {
            if (m_SlashRoutine != null)
            {
                StopCoroutine(m_SlashRoutine);
            }
            m_SlashRoutine = StartCoroutine(SlashAnimationRoutine(onComplete));
        }

        /// <summary>Spawn particle nổ khi chém trúng mục tiêu.</summary>
        public void SpawnImpact(Vector3 hitPoint)
        {
            if (impactPrefab != null)
            {
                Instantiate(impactPrefab, hitPoint, Quaternion.identity);
            }
        }

        private IEnumerator SlashAnimationRoutine(Action onComplete)
        {
            if (voidSmokeParticles != null) voidSmokeParticles.Play();
            if (sparkParticles != null) sparkParticles.Play();

            float elapsed = 0f;
            float targetDiameter = radius * 2f;

            // Tính toán tỉ lệ scale chính xác dựa trên kích thước thật của sprite (Pixels To Units)
            float spriteUnitSize = 1f;
            if (slashSprite != null && slashSprite.sprite != null && slashSprite.sprite.rect.width > 0f)
            {
                spriteUnitSize = slashSprite.sprite.rect.width / slashSprite.sprite.pixelsPerUnit;
            }
            float baseScale = targetDiameter / Mathf.Max(0.01f, spriteUnitSize);

            if (slashSprite != null)
            {
                slashSprite.gameObject.SetActive(true);
                slashSprite.transform.localScale = Vector3.zero;
            }

            while (elapsed < slashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slashDuration);

                // Smooth expansion and snap rotation 360 deg
                float scaleT = Mathf.Sin(t * Mathf.PI * 0.5f); // ease-out
                float currentScale = baseScale * scaleT;

                if (slashSprite != null)
                {
                    slashSprite.transform.localScale = new Vector3(currentScale, currentScale, 1f);
                    // Rapid spin 360 degrees
                    float angle = t * 360f;
                    slashSprite.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

                    // Fade out in the last 40%
                    if (t > 0.6f)
                    {
                        float alpha = Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
                        Color c = slashSprite.color;
                        c.a = alpha;
                        slashSprite.color = c;
                    }
                }

                yield return null;
            }

            if (slashSprite != null)
            {
                slashSprite.gameObject.SetActive(false);
            }

            onComplete?.Invoke();

            if (autoDestroy)
            {
                // Cho particle tàn dần rồi hủy
                yield return new WaitForSeconds(0.4f);
                Destroy(gameObject);
            }
        }
    }
}
