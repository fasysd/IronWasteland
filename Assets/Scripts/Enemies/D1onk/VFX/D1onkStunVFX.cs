using System;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk.VFX
{
    /// <summary>
    /// VFX Choáng váng (Stun) xoay tròn trên đỉnh đầu của Trưởng Quản Ngục D1onk.
    /// Gồm các ngôi sao năng lượng / hồ quang điện quay quanh theo quỹ đạo elip.
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkStunVFX : MonoBehaviour, IronWasteland.Net.INetVisualGhostDriver
    {
        [Header("Components")]
        [SerializeField] private Transform star1;
        [SerializeField] private Transform star2;
        [SerializeField] private Transform star3;
        [SerializeField] private ParticleSystem stunSparks;

        [Header("Settings")]
        [SerializeField] private float orbitSpeed = 240f;
        [SerializeField] private float orbitRadiusX = 1.2f;
        [SerializeField] private float orbitRadiusY = 0.4f;

        private float m_Angle = 0f;
        private float m_Timer = 0f;
        private float m_Duration = 3f;
        private bool m_IsActive = false;
        private Action m_OnComplete;

        public bool IsActive => m_IsActive;

        public void Play(float duration = 3f, Action onComplete = null)
        {
            m_Duration = duration;
            m_OnComplete = onComplete;
            m_Timer = 0f;
            m_Angle = 0f;
            m_IsActive = true;
            gameObject.SetActive(true);

            if (stunSparks != null)
                stunSparks.Play();
        }

        private void Update()
        {
            ManualTick(Time.deltaTime);
        }

        public void ManualTick(float dt)
        {
            if (!m_IsActive) return;

            m_Timer += dt;
            m_Angle += orbitSpeed * dt * Mathf.Deg2Rad;

            UpdateStarPosition(star1, m_Angle);
            UpdateStarPosition(star2, m_Angle + 2.094f); // + 120 độ
            UpdateStarPosition(star3, m_Angle + 4.188f); // + 240 độ

            if (m_Duration > 0f && m_Timer >= m_Duration)
            {
                Stop();
            }
        }

        private void UpdateStarPosition(Transform star, float angle)
        {
            if (star == null) return;
            float x = Mathf.Cos(angle) * orbitRadiusX;
            float y = Mathf.Sin(angle) * orbitRadiusY;
            star.localPosition = new Vector3(x, y, 0f);
        }

        public void Stop()
        {
            if (!m_IsActive) return;
            m_IsActive = false;

            if (stunSparks != null)
                stunSparks.Stop();

            m_OnComplete?.Invoke();

            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }
    }
}
