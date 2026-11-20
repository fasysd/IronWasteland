using System;
using System.Collections;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Enemies.DeadlandPhantom
{
    /// <summary>
    /// VFX 4: Vết Nứt Không Gian (SpatialRift).
    /// Vết nứt hư không rách toạc trên mặt đất phát quang nhấp nháy,
    /// các hạt hư không xoay hút vào tâm và hiệu ứng nổ xoáy khi Tank chạm vào hoặc kích hoạt.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpatialRiftVFX : MonoBehaviour
    {
        [Header("Fissure & Suction Settings")]
        [Tooltip("Bán kính vùng ảnh hưởng vết nứt (m).")]
        [SerializeField, Min(0.5f)] private float riftRadius = 1.8f;

        [Tooltip("Tốc độ nhấp nháy phát quang của vết nứt.")]
        [SerializeField, Min(0.1f)] private float flickerSpeed = 5f;

        [Tooltip("Thời gian tồn tại tối đa trước khi tự biến mất (giây).")]
        [SerializeField, Min(1f)] private float lifetime = 14f;

        [Header("Components")]
        [SerializeField] private SpriteRenderer fissureSprite;
        [SerializeField] private SpriteRenderer singularitySprite;
        [SerializeField] private ParticleSystem vortexSuctionParticles;
        [SerializeField] private GameObject vortexExplosionPrefab;

        private float m_Age;
        private bool m_Exploded;
        private Vector3 m_BaseScale = Vector3.one;

        /// <summary>Sự kiện bắn ra khi Tank bước vào vùng ảnh hưởng của vết nứt.</summary>
        public event Action<SpatialRiftVFX, TankBrain> TankEntered;
        public bool HasTankEnteredListeners => TankEntered != null;

        public float RiftRadius => riftRadius;
        public bool IsExploded => m_Exploded;

        private void Awake()
        {
            m_BaseScale = transform.localScale;
        }

        private void Start()
        {
            if (vortexSuctionParticles != null)
            {
                var shape = vortexSuctionParticles.shape;
                shape.radius = riftRadius;
                vortexSuctionParticles.Play();
            }
        }

        private void Update()
        {
            if (m_Exploded) return;

            m_Age += Time.deltaTime;

            // Pulsing and flickering luminescence
            float flicker = 0.85f + 0.15f * Mathf.Sin(m_Age * flickerSpeed);
            float scalePulse = 1f + 0.05f * Mathf.Cos(m_Age * (flickerSpeed * 0.7f));

            if (fissureSprite != null)
            {
                Color c = fissureSprite.color;
                c.a = flicker;
                fissureSprite.color = c;

                float unitWidth = (fissureSprite.sprite != null && fissureSprite.sprite.rect.width > 0f)
                    ? fissureSprite.sprite.rect.width / fissureSprite.sprite.pixelsPerUnit
                    : 1f;
                float baseScaleX = (riftRadius * 2f) / Mathf.Max(0.01f, unitWidth);
                float baseScaleY = (riftRadius * 1.2f) / Mathf.Max(0.01f, unitWidth);

                fissureSprite.transform.localScale = new Vector3(baseScaleX * scalePulse, baseScaleY * scalePulse, 1f);
            }

            if (singularitySprite != null)
            {
                // Slow rotation of the singularity core
                singularitySprite.transform.Rotate(0f, 0f, -45f * Time.deltaTime);
                float singPulse = 0.9f + 0.2f * Mathf.Sin(m_Age * 8f);
                singularitySprite.transform.localScale = Vector3.one * (singPulse * 0.8f);
            }

            if (m_Age >= lifetime)
            {
                TriggerCollapseAndDestroy();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_Exploded) return;

            TankBrain tank = ResolveTankBrain(other);
            if (tank != null)
            {
                TankEntered?.Invoke(this, tank);
                TriggerExplosion();
            }
        }

        public static TankBrain ResolveTankBrain(Collider2D col)
        {
            if (col == null) return null;
            if (col.TryGetComponent<ColliderOwnerRef>(out var targetRef) && targetRef != null)
            {
                if (targetRef.TryGetOwner<TankBrain>(out var directBrain) && directBrain != null)
                    return directBrain;
                if (targetRef.Owner is TankBrain brain)
                    return brain;
                if (targetRef.Owner is Component comp)
                {
                    var compBrain = comp.GetComponent<TankBrain>() ?? comp.GetComponentInParent<TankBrain>();
                    if (compBrain != null) return compBrain;
                }
                if (targetRef.Owner is GameObject go)
                {
                    var goBrain = go.GetComponent<TankBrain>() ?? go.GetComponentInParent<TankBrain>();
                    if (goBrain != null) return goBrain;
                }
            }
            return col.GetComponentInParent<TankBrain>();
        }

        /// <summary>Kích hoạt nổ xoáy khi Tank chạm vào vết nứt.</summary>
        public void TriggerExplosion(Action onComplete = null)
        {
            if (m_Exploded) return;
            m_Exploded = true;

            StartCoroutine(ExplosionRoutine(onComplete));
        }

        private void TriggerCollapseAndDestroy()
        {
            if (m_Exploded) return;
            m_Exploded = true;
            StartCoroutine(CollapseRoutine());
        }

        private IEnumerator ExplosionRoutine(Action onComplete)
        {
            // Rapid inward suction collapse then burst
            float collapseTime = 0.15f;
            float elapsed = 0f;
            Vector3 initScale = transform.localScale;

            while (elapsed < collapseTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / collapseTime;
                transform.localScale = Vector3.Lerp(initScale, Vector3.zero, t * t);
                yield return null;
            }

            // Spawn explosive vortex VFX
            if (vortexExplosionPrefab != null)
            {
                Instantiate(vortexExplosionPrefab, transform.position, Quaternion.identity);
            }

            onComplete?.Invoke();
            Destroy(gameObject);
        }

        private IEnumerator CollapseRoutine()
        {
            float fadeTime = 0.5f;
            float elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeTime;
                float alpha = 1f - t;

                if (fissureSprite != null)
                {
                    Color c = fissureSprite.color;
                    c.a = alpha;
                    fissureSprite.color = c;
                }
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
