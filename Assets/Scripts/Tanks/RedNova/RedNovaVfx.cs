using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.RedNova
{
    /// <summary>
    /// Hiển thị VFX cho Red Nova: aura bám thân (Overdrive / Execution Core / Nova Pierce),
    /// chùm laser nối nòng tới điểm chạm, sóng xung kích khi Execution Core hạ gục,
    /// tàn ảnh khi Nova Rush.
    /// Gắn cùng GameObject với TankBrain_RedNova.
    /// Sprite lấy từ Assets/Art/Tank/VFX/RedNova.
    /// </summary>
    [DisallowMultipleComponent]
    public class RedNovaVfx : MonoBehaviour
    {
        [Header("Sprite hiệu ứng (Assets/Art/Tank/VFX/RedNova)")]
        [SerializeField] private Sprite overdriveAura;
        [SerializeField] private Sprite executionCore;
        [SerializeField] private Sprite pierceOverlay;
        [SerializeField] private Sprite laserBeam;
        [SerializeField] private Sprite laserImpact;
        [SerializeField] private Sprite executionShockwave;
        [SerializeField] private Sprite rushAfterimage;

        [Header("Cấu hình")]
        [Tooltip("Hệ số phóng aura so với sprite gốc.")]
        [SerializeField, Min(0.1f)] private float auraScale = 2.4f;
        [SerializeField] private int auraSortingOrder = 3;
        [SerializeField] private int beamSortingOrder = 9;
        [Tooltip("Thời gian hiện chùm laser mỗi phát (giây).")]
        [SerializeField, Min(0.02f)] private float laserDuration = 0.1f;
        [Tooltip("Độ dài tối đa của tàn ảnh Nova Rush.")]
        [SerializeField, Min(0.2f)] private float rushStreakLength = 1.2f;

        private SpriteRenderer m_OverdriveAura;
        private SpriteRenderer m_ExecutionCore;
        private SpriteRenderer m_PierceOverlay;
        private SpriteRenderer m_LaserBeam;
        private SpriteRenderer m_LaserImpact;

        private float m_LaserHideTime;

        private void Awake()
        {
            m_OverdriveAura = CreateChild("VFX_OverdriveAura", overdriveAura, auraSortingOrder, auraScale);
            m_ExecutionCore = CreateChild("VFX_ExecutionCore", executionCore, auraSortingOrder, auraScale * 0.9f);
            m_PierceOverlay = CreateChild("VFX_PierceOverlay", pierceOverlay, auraSortingOrder + 1, auraScale * 0.7f);

            m_LaserBeam = CreateChild("VFX_LaserBeam", laserBeam, beamSortingOrder, 1f);
            m_LaserImpact = CreateChild("VFX_LaserImpact", laserImpact, beamSortingOrder + 1, 0.7f);
        }

        private void LateUpdate()
        {
            if (m_LaserHideTime > 0f && Time.time >= m_LaserHideTime)
            {
                m_LaserHideTime = 0f;
                SetActive(m_LaserBeam, false);
                SetActive(m_LaserImpact, false);
            }
        }

        // ---------- Aura bám thân ----------

        public void SetOverdriveActive(bool active) => SetActive(m_OverdriveAura, active);

        public void SetExecutionCoreActive(bool active) => SetActive(m_ExecutionCore, active);

        public void SetPierceActive(bool active) => SetActive(m_PierceOverlay, active);

        // ---------- Laser ----------

        /// <summary>Vẽ chùm laser từ nòng tới điểm chạm trong một khoảnh khắc.</summary>
        public void ShowLaserBeam(Vector2 from, Vector2 to)
        {
            if (m_LaserBeam == null)
                return;

            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.01f)
                return;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            Transform beam = m_LaserBeam.transform;
            beam.position = (from + to) * 0.5f;
            beam.rotation = Quaternion.Euler(0f, 0f, angle);
            beam.localScale = new Vector3(distance / SpriteWorldWidth(m_LaserBeam.sprite, 1f), 1f, 1f);

            m_LaserImpact.transform.position = to;
            m_LaserImpact.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            SetActive(m_LaserBeam, true);
            SetActive(m_LaserImpact, true);
            m_LaserHideTime = Time.time + laserDuration;
        }

        // ---------- Hiệu ứng một lần ----------

        /// <summary>Sóng xung kích lan ra khi Execution Core hạ gục mục tiêu.</summary>
        public void PlayExecutionShockwave(Vector2 position)
        {
            PlayOneShot(executionShockwave, position, 0.5f, 3.2f, 0.35f);
        }

        /// <summary>Tàn ảnh dọc đường lướt của Nova Rush.</summary>
        public void PlayRushTrail(Vector2 from, Vector2 to)
        {
            PlayOneShot(rushAfterimage, from, 1f, 1.6f, 0.30f);
            PlayOneShot(rushAfterimage, Vector2.Lerp(from, to, 0.5f), 0.9f, 1.4f, 0.22f);
        }

        // ---------- Tiện ích ----------

        private SpriteRenderer CreateChild(string name, Sprite sprite, int sortingOrder, float scale)
        {
            GameObject go = CreateEffectObject(name, sprite, sortingOrder);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * scale;
            go.SetActive(false);
            return go.GetComponent<SpriteRenderer>();
        }

        private GameObject CreateEffectObject(string name, Sprite sprite, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return go;
        }

        private void PlayOneShot(Sprite sprite, Vector2 position, float scale, float targetScale, float lifetime)
        {
            if (sprite == null)
                return;

            GameObject go = CreateEffectObject("VFX_OneShot", sprite, beamSortingOrder + 2);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;

            StartCoroutine(ScaleAndFadeRoutine(go, targetScale, lifetime));
        }

        private IEnumerator ScaleAndFadeRoutine(GameObject go, float targetScale, float lifetime)
        {
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            float elapsed = 0f;

            while (elapsed < lifetime && go != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);

                go.transform.localScale = Vector3.one * Mathf.Lerp(1f, targetScale, t);

                if (renderer != null)
                {
                    Color color = renderer.color;
                    color.a = 1f - t;
                    renderer.color = color;
                }

                yield return null;
            }

            if (go != null)
                Destroy(go);
        }

        private static void SetActive(SpriteRenderer renderer, bool active)
        {
            if (renderer != null && renderer.gameObject.activeSelf != active)
                renderer.gameObject.SetActive(active);
        }

        /// <summary>Chiều rộng world của sprite, dùng để kéo dài chùm laser.</summary>
        private static float SpriteWorldWidth(Sprite sprite, float fallback)
        {
            if (sprite == null || sprite.pixelsPerUnit <= 0f)
                return fallback;

            return sprite.rect.width / sprite.pixelsPerUnit;
        }
    }
}
