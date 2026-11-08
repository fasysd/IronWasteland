using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Enemies.Normal
{
    /// <summary>
    /// Visual tia sét dạng hình chữ nhật dài (Rectangle) nối giữa hai điểm (Source và Destination).
    /// - Tự động tính toán vị trí tâm, độ dài và góc xoay.
    /// - Co giãn kích thước qua Transform.localScale: scale.x = khoảng cách, scale.y = độ dày tia sét.
    /// - Hiệu ứng mờ dần (fade out) theo thời gian và tự động thu hồi về Pool.
    /// </summary>
    [DisallowMultipleComponent]
    public class LightningBeam_TinhLinhSamSetNho : MonoBehaviour
    {
        [Header("Beam Visual Settings")]
        [Tooltip("SpriteRenderer hiển thị hình chữ nhật tia sét.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Độ dày mặc định của tia sét (đơn vị world space).")]
        [SerializeField, Min(0.05f)] private float defaultThickness = 0.2f;

        [Tooltip("Thời gian tồn tại của tia sét trước khi mờ hẳn (giây).")]
        [SerializeField, Min(0.05f)] private float defaultDuration = 0.25f;

        [Tooltip("Màu sắc tia sét.")]
        [SerializeField] private Color beamColor = new Color(0.35f, 0.85f, 1.0f, 0.95f);

        private static Sprite s_DefaultBeamSprite;
        private Coroutine m_LifeCoroutine;
        private Action<LightningBeam_TinhLinhSamSetNho> m_OnDespawn;

        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public float DefaultThickness { get => defaultThickness; set => defaultThickness = value; }
        public float DefaultDuration { get => defaultDuration; set => defaultDuration = value; }
        public Color BeamColor { get => beamColor; set => beamColor = value; }

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            EnsureSprite();
        }

        private void EnsureSprite()
        {
            if (spriteRenderer != null && spriteRenderer.sprite == null)
            {
                if (s_DefaultBeamSprite == null)
                {
                    s_DefaultBeamSprite = Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0, 0, 4, 4),
                        new Vector2(0.5f, 0.5f),
                        4f
                    );
                    s_DefaultBeamSprite.name = "GeneratedBeamSquareSprite";
                }

                spriteRenderer.sprite = s_DefaultBeamSprite;
            }
        }

        /// <summary>
        /// Kích hoạt hiển thị tia sét nối giữa startPos và endPos.
        /// </summary>
        public void ShowBeam(
            Vector2 startPos,
            Vector2 endPos,
            float thickness = -1f,
            float duration = -1f,
            Color? color = null,
            Action<LightningBeam_TinhLinhSamSetNho> onDespawn = null)
        {
            EnsureSprite();

            m_OnDespawn = onDespawn;
            float actualThickness = thickness > 0f ? thickness : defaultThickness;
            float actualDuration = duration > 0f ? duration : defaultDuration;
            Color actualColor = color.HasValue ? color.Value : beamColor;

            Vector2 delta = endPos - startPos;
            float distance = delta.magnitude;

            // Đặt vị trí tại trung điểm của 2 điểm nối
            Vector3 midPoint = (startPos + endPos) * 0.5f;
            midPoint.z = transform.position.z;
            transform.position = midPoint;

            // Xoay hướng theo vector delta
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // Co giãn theo chiều dài và độ dày
            transform.localScale = new Vector3(Mathf.Max(0.01f, distance), actualThickness, 1f);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = actualColor;
            }

            gameObject.SetActive(true);

            if (m_LifeCoroutine != null)
            {
                StopCoroutine(m_LifeCoroutine);
            }
            m_LifeCoroutine = StartCoroutine(FadeRoutine(actualDuration, actualColor));
        }

        private IEnumerator FadeRoutine(float duration, Color initialColor)
        {
            float elapsed = 0f;
            Color startColor = initialColor;
            Color targetColor = new Color(initialColor.r, initialColor.g, initialColor.b, 0f);

            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.Lerp(startColor, targetColor, t);
                }
            }

            Despawn();
        }

        public void Despawn()
        {
            if (m_LifeCoroutine != null)
            {
                StopCoroutine(m_LifeCoroutine);
                m_LifeCoroutine = null;
            }

            gameObject.SetActive(false);
            m_OnDespawn?.Invoke(this);
            m_OnDespawn = null;
        }
    }
}
