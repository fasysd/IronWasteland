using System;
using System.Collections;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper.VFX
{
    /// <summary>
    /// VFX 9: Vòng Ma Pháp Tam Nguyên Tố Tụ Lực (VFX_BurstChargingAura).
    /// Vòng tròn ma pháp tam sắc (Lam - Lục - Đỏ) xoay tròn hội tụ dưới chân Boss khi vận sức bùng nổ.
    /// </summary>
    [DisallowMultipleComponent]
    public class BurstChargingAuraVFX : MonoBehaviour, IronWasteland.Net.INetVisualGhostDriver
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer magicCircleOuter;
        [SerializeField] private SpriteRenderer magicCircleInner;
        [SerializeField] private ParticleSystem triElementSparks;
        [SerializeField] private ParticleSystem energyInflowParticles;

        [Header("Settings")]
        [SerializeField] private float outerRotationSpeed = 90f;
        [SerializeField] private float innerRotationSpeed = -140f;
        [SerializeField, Min(0.5f)] private float defaultDuration = 2.0f;

        private Coroutine m_Routine;
        private bool m_IsCharging;

        public bool IsCharging => m_IsCharging;

        public void StartCharging(float duration = -1f, Action onComplete = null)
        {
            float effectiveDuration = duration > 0f ? duration : defaultDuration;
            m_IsCharging = true;
            gameObject.SetActive(true);

            if (triElementSparks != null) triElementSparks.Play();
            if (energyInflowParticles != null) energyInflowParticles.Play();

            if (m_Routine != null) StopCoroutine(m_Routine);
            m_Routine = StartCoroutine(ChargingRoutine(effectiveDuration, onComplete));
        }

        public void StopCharging()
        {
            m_IsCharging = false;
            if (m_Routine != null)
            {
                StopCoroutine(m_Routine);
                m_Routine = null;
            }
            if (triElementSparks != null) triElementSparks.Stop();
            if (energyInflowParticles != null) energyInflowParticles.Stop();
            gameObject.SetActive(false);
        }

        private IEnumerator ChargingRoutine(float duration, Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float dt = Time.deltaTime;

                if (magicCircleOuter != null)
                {
                    magicCircleOuter.transform.Rotate(0f, 0f, outerRotationSpeed * dt);
                }
                if (magicCircleInner != null)
                {
                    magicCircleInner.transform.Rotate(0f, 0f, innerRotationSpeed * dt);
                }

                yield return null;
            }

            m_IsCharging = false;
            onComplete?.Invoke();
        }

        private void Update()
        {
            if (m_IsCharging)
            {
                float dt = Time.deltaTime;
                if (magicCircleOuter != null) magicCircleOuter.transform.Rotate(0f, 0f, outerRotationSpeed * dt);
                if (magicCircleInner != null) magicCircleInner.transform.Rotate(0f, 0f, innerRotationSpeed * dt);
            }
        }
    }
}
