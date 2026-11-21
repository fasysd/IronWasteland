using System;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk.VFX
{
    /// <summary>
    /// VFX Kỹ năng Viện Binh Tinh Linh (VienBinhTinhLinh):
    /// Vòng tròn ma pháp triệu hồi xuất hiện trên mặt đất, các ký tự cổ ngữ xoay tròn và cột sáng linh hồn dựng đứng.
    /// Khi thời gian tụ phép hoàn tất, quái vật thường tương ứng được triệu hồi ra.
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkVienBinhSummonVFX : MonoBehaviour
    {
        [Header("Summon Settings")]
        [Tooltip("Thời gian xuất hiện vòng ma pháp trước khi quái vật xuất hiện (giây).")]
        [SerializeField, Min(0.2f)] private float summonDuration = 0.8f;

        [Header("Components")]
        [SerializeField] private SpriteRenderer magicCircleSprite;
        [SerializeField] private ParticleSystem spiritPillarParticles;

        private GameObject m_EnemyPrefabToSpawn;
        private GameObject m_SpawnedEnemy;
        private float m_Timer = 0f;
        private bool m_HasSpawned = false;
        private bool m_IsFinished = false;
        private Action<GameObject> m_OnSpawnedCallback;

        public bool HasSpawned => m_HasSpawned;
        public bool IsFinished => m_IsFinished;
        public GameObject SpawnedEnemy => m_SpawnedEnemy;

        public void Initialize(GameObject enemyPrefabToSpawn, Action<GameObject> onSpawned = null)
        {
            m_EnemyPrefabToSpawn = enemyPrefabToSpawn;
            m_OnSpawnedCallback = onSpawned;
            m_Timer = 0f;
            m_HasSpawned = false;
            m_IsFinished = false;

            if (spiritPillarParticles != null)
            {
                spiritPillarParticles.Play();
            }
        }

        private void Update()
        {
            ManualTick(Time.deltaTime);
        }

        /// <summary>
        /// Tiến trình triệu hồi ma pháp. Hỗ trợ unit test.
        /// </summary>
        public void ManualTick(float dt)
        {
            if (m_IsFinished) return;

            m_Timer += dt;

            // Xoay vòng ma pháp cổ ngữ
            if (magicCircleSprite != null)
            {
                magicCircleSprite.transform.Rotate(0f, 0f, 180f * dt);
                float progress = Mathf.Clamp01(m_Timer / summonDuration);
                float pulse = Mathf.PingPong(m_Timer * 6f, 0.4f) + 0.6f;
                Color c = magicCircleSprite.color;
                c.a = pulse * (m_HasSpawned ? (1f - (m_Timer - summonDuration) * 2f) : progress);
                magicCircleSprite.color = c;
            }

            if (!m_HasSpawned && m_Timer >= summonDuration)
            {
                SpawnEnemy();
            }

            // Kết thúc và dọn dẹp sau khi quái xuất hiện 0.5s
            if (m_HasSpawned && m_Timer >= summonDuration + 0.5f)
            {
                m_IsFinished = true;
                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
            }
        }

        private void SpawnEnemy()
        {
            m_HasSpawned = true;

            if (m_EnemyPrefabToSpawn != null)
            {
                m_SpawnedEnemy = Instantiate(m_EnemyPrefabToSpawn, transform.position, Quaternion.identity);
                m_OnSpawnedCallback?.Invoke(m_SpawnedEnemy);
            }
            else
            {
                Debug.LogWarning("[D1onkVienBinhSummonVFX] enemyPrefabToSpawn is null!");
            }
        }
    }
}
