using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// VFX 10: Khiên Nguyên Tố & Ma Trận Tinh Thể (VFX_BurstShieldAndCrystals).
    /// Vòm khiên tam sắc bảo vệ Boss (25% Max HP) cùng 3 tinh thể nguyên tố (Tròn - Lam, Vuông - Lục, Tam Giác - Đỏ) xoay quanh.
    /// </summary>
    [DisallowMultipleComponent]
    public class BurstShieldVFX : MonoBehaviour, IronWasteland.Net.INetVisualGhostDriver
    {
        [Header("Shield Components")]
        [SerializeField] private SpriteRenderer shieldDomeSprite;
        [SerializeField] private ParticleSystem shieldGlowParticles;

        [Header("Orbiting Crystals")]
        [SerializeField] private Transform crystalWaterCircle;
        [SerializeField] private Transform crystalPoisonSquare;
        [SerializeField] private Transform crystalFireTriangle;

        [Header("Settings")]
        [SerializeField] private float orbitRadius = 2.5f;
        [SerializeField] private float orbitSpeed = 120f;
        [SerializeField] private float pulseSpeed = 2f;

        private float m_CurrentAngle;

        private void Update()
        {
            m_CurrentAngle += orbitSpeed * Time.deltaTime;
            UpdateCrystalPositions();

            if (shieldDomeSprite != null)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI);
                Color c = shieldDomeSprite.color;
                c.a = pulse * 0.7f;
                shieldDomeSprite.color = c;
            }
        }

        private void UpdateCrystalPositions()
        {
            float rad = m_CurrentAngle * Mathf.Deg2Rad;
            // 3 tinh thể cách đều nhau 120 độ
            if (crystalWaterCircle != null)
            {
                float a = rad;
                crystalWaterCircle.localPosition = new Vector3(Mathf.Cos(a) * orbitRadius, Mathf.Sin(a) * orbitRadius, 0f);
            }

            if (crystalPoisonSquare != null)
            {
                float a = rad + (2f * Mathf.PI / 3f);
                crystalPoisonSquare.localPosition = new Vector3(Mathf.Cos(a) * orbitRadius, Mathf.Sin(a) * orbitRadius, 0f);
            }

            if (crystalFireTriangle != null)
            {
                float a = rad + (4f * Mathf.PI / 3f);
                crystalFireTriangle.localPosition = new Vector3(Mathf.Cos(a) * orbitRadius, Mathf.Sin(a) * orbitRadius, 0f);
            }
        }

        public void SetActiveState(bool active)
        {
            gameObject.SetActive(active);
            if (active && shieldGlowParticles != null)
            {
                shieldGlowParticles.Play();
            }
        }
    }
}
