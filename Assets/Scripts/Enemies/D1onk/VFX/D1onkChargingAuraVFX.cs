using UnityEngine;

namespace IronWasteland.Enemies.D1onk.VFX
{
    /// <summary>
    /// VFX Hào quang Tụ Năng Lượng (Charging / BurstCharging) cho Trưởng Quản Ngục D1onk.
    /// Vòng xoáy năng lượng cao thế, các hạt plasma hút ngược về tâm tàu chiến.
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkChargingAuraVFX : MonoBehaviour, IronWasteland.Net.INetVisualGhostDriver
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer swirlSpriteOuter;
        [SerializeField] private SpriteRenderer swirlSpriteInner;
        [SerializeField] private ParticleSystem gatheringParticles;

        [Header("Settings")]
        [SerializeField] private float outerSpinSpeed = -180f;
        [SerializeField] private float innerSpinSpeed = 240f;

        private bool m_IsActive = false;
        public bool IsActive => m_IsActive;

        private void Update()
        {
            ManualTick(Time.deltaTime);
        }

        public void ManualTick(float dt)
        {
            if (!m_IsActive) return;

            if (swirlSpriteOuter != null)
                swirlSpriteOuter.transform.Rotate(0f, 0f, outerSpinSpeed * dt);

            if (swirlSpriteInner != null)
                swirlSpriteInner.transform.Rotate(0f, 0f, innerSpinSpeed * dt);
        }

        public void Play()
        {
            m_IsActive = true;
            gameObject.SetActive(true);

            if (gatheringParticles != null)
                gatheringParticles.Play();
        }

        public void Stop()
        {
            m_IsActive = false;

            if (gatheringParticles != null)
                gatheringParticles.Stop();

            if (Application.isPlaying)
                Destroy(gameObject, 0.5f);
            else
                DestroyImmediate(gameObject);
        }
    }
}
