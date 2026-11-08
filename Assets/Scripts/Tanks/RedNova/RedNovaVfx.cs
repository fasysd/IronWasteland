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
        [SerializeField] private Sprite pierceBeam;
        [SerializeField] private Sprite laserBeam;
        [SerializeField] private Sprite laserImpact;
        [SerializeField] private Sprite executionShockwave;
        [SerializeField] private Sprite rushAfterimage;

        [Header("Cấu hình")]
        [Tooltip("Hệ số phóng aura so với sprite gốc.")]
        [SerializeField, Min(0.1f)] private float auraScale = 4.2f;
        [SerializeField] private int auraSortingOrder = 3;
        [SerializeField] private int beamSortingOrder = 9;
        [Tooltip("Thời gian hiện chùm laser mỗi phát (giây).")]
        [SerializeField, Min(0.02f)] private float laserDuration = 0.1f;
        [Tooltip("Độ dày của chùm laser (nhân theo trục dọc sprite).")]
        [SerializeField, Min(0.2f)] private float beamThickness = 3.6f;
        [Tooltip("Kích thước điểm chạm laser.")]
        [SerializeField, Min(0.2f)] private float impactScale = 2.6f;
        [Tooltip("Độ dài tối đa của tàn ảnh Nova Rush.")]
        [SerializeField, Min(0.2f)] private float rushStreakLength = 1.2f;

        private SpriteRenderer m_OverdriveAura;
        private SpriteRenderer m_ExecutionCore;
        private SpriteRenderer m_PierceOverlay;
        private SpriteRenderer m_PierceBeam;
        private SpriteRenderer m_LaserBeam;
        private SpriteRenderer m_LaserImpact;

        private float m_LaserHideTime;
        private float m_PierceHideTime;

        private void Awake()
        {
            m_OverdriveAura = CreateChild("VFX_OverdriveAura", overdriveAura, auraSortingOrder, auraScale);
            m_ExecutionCore = CreateChild("VFX_ExecutionCore", executionCore, auraSortingOrder, auraScale * 0.9f);
            m_PierceOverlay = CreateChild("VFX_PierceOverlay", pierceOverlay, auraSortingOrder + 1, auraScale * 0.7f);

            m_LaserBeam = CreateChild("VFX_LaserBeam", laserBeam, beamSortingOrder, 1f);
            m_LaserImpact = CreateChild("VFX_LaserImpact", laserImpact, beamSortingOrder + 1, 0.7f);
            m_PierceBeam = CreateChild("VFX_PierceBeam", pierceBeam, beamSortingOrder, 1f);
        }

        private void LateUpdate()
        {
            if (m_LaserHideTime > 0f && Time.time >= m_LaserHideTime)
            {
                m_LaserHideTime = 0f;
                SetActive(m_LaserBeam, false);
                SetActive(m_LaserImpact, false);
            }

            if (m_PierceHideTime > 0f && Time.time >= m_PierceHideTime)
            {
                m_PierceHideTime = 0f;
                SetActive(m_PierceBeam, false);
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
            if (!ShowBeam(m_LaserBeam, from, to, beamThickness))
                return;

            Vector2 delta = to - from;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            m_LaserImpact.transform.position = to;
            m_LaserImpact.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            m_LaserImpact.transform.localScale = Vector3.one * impactScale;

            SetActive(m_LaserImpact, true);
            m_LaserHideTime = Time.time + laserDuration;
        }

        /// <summary>
        /// Vẽ chùm xuyên của Nova Pierce dọc theo hướng bắn (sprite pierce_beam),
        /// thay cho việc đặt một vệt ngang lên giữa thân tank.
        /// </summary>
        public void ShowPierceBeam(Vector2 from, Vector2 to)
        {
            if (!ShowBeam(m_PierceBeam, from, to, beamThickness * 0.75f))
                return;

            m_PierceHideTime = Time.time + laserDuration * 1.5f;
        }

        /// <summary>Đặt chùm beam từ <paramref name="from"/> tới <paramref name="to"/> và bật nó.</summary>
        private bool ShowBeam(SpriteRenderer beam, Vector2 from, Vector2 to, float thickness)
        {
            if (beam == null || beam.sprite == null)
                return false;

            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.01f)
                return false;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            Transform beamTransform = beam.transform;
            beamTransform.position = (from + to) * 0.5f;
            beamTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            beamTransform.localScale = new Vector3(
                distance / SpriteWorldWidth(beam.sprite, 1f), thickness, 1f);

            SetActive(beam, true);
            return true;
        }

        // ---------- Hiệu ứng một lần ----------

        /// <summary>Sóng xung kích lan ra khi Execution Core hạ gục mục tiêu.</summary>
        public void PlayExecutionShockwave(Vector2 position)
        {
            PlayOneShot(executionShockwave, position, 0.9f, 5.0f, 0.4f);
        }

        /// <summary>Tàn ảnh dọc đường lướt của Nova Rush.</summary>
        public void PlayRushTrail(Vector2 from, Vector2 to)
        {
            PlayOneShot(rushAfterimage, from, 2.0f, 3.0f, 0.35f);
            PlayOneShot(rushAfterimage, Vector2.Lerp(from, to, 0.5f), 1.8f, 2.6f, 0.28f);
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
