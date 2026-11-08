using System.Collections;
using UnityEngine;

namespace IronWasteland.Tanks.WallTitan
{
    /// <summary>
    /// Hiển thị VFX cho Wall Titan: hào quang Stone Fortress, giáp đá, chốt neo khi
    /// Immovable Titan, màn chắn Guardian Wall, vết nứt + sóng xung kích của Earthquake,
    /// đá văng khi Earth Break / Earth Prison.
    /// Gắn cùng GameObject với TankBrain_WallTitan.
    /// Sprite lấy từ Assets/Art/Tank/VFX/WallTitan.
    /// </summary>
    [DisallowMultipleComponent]
    public class WallTitanVfx : MonoBehaviour
    {
        [Header("Sprite hiệu ứng (Assets/Art/Tank/VFX/WallTitan)")]
        [SerializeField] private Sprite fortressAura;
        [SerializeField] private Sprite stoneArmor;
        [SerializeField] private Sprite metallicGlow;
        [SerializeField] private Sprite anchorSpikes;
        [SerializeField] private Sprite guardianBarrier;
        [SerializeField] private Sprite guardianMist;
        [SerializeField] private Sprite prisonActive;
        [SerializeField] private Sprite prisonCollapse;
        [SerializeField] private Sprite earthquakeFissures;
        [SerializeField] private Sprite earthquakeShockwave;
        [SerializeField] private Sprite earthShards;
        [SerializeField] private Sprite earthBoulder;

        [Header("Cấu hình")]
        [SerializeField, Min(0.1f)] private float auraScale = 2.6f;
        [SerializeField] private int auraSortingOrder = 3;
        [SerializeField] private int fxSortingOrder = 8;
        [Tooltip("Thời gian tồn tại vết nứt Earthquake (giây).")]
        [SerializeField, Min(0.2f)] private float fissureDuration = 2.0f;

        private SpriteRenderer m_FortressAura;
        private SpriteRenderer m_StoneArmor;
        private SpriteRenderer m_MetallicGlow;
        private SpriteRenderer m_AnchorSpikes;
        private SpriteRenderer m_GuardianBarrier;
        private SpriteRenderer m_GuardianMist;

        private void Awake()
        {
            m_FortressAura = CreateChild("VFX_FortressAura", fortressAura, auraSortingOrder, auraScale);
            m_StoneArmor = CreateChild("VFX_StoneArmor", stoneArmor, auraSortingOrder + 1, auraScale * 0.9f);
            m_MetallicGlow = CreateChild("VFX_MetallicGlow", metallicGlow, auraSortingOrder, auraScale * 0.85f);
            m_AnchorSpikes = CreateChild("VFX_AnchorSpikes", anchorSpikes, auraSortingOrder + 2, auraScale * 1.1f);
            m_GuardianBarrier = CreateChild("VFX_GuardianBarrier", guardianBarrier, auraSortingOrder + 1, auraScale);
            m_GuardianMist = CreateChild("VFX_GuardianMist", guardianMist, auraSortingOrder, auraScale * 1.15f);
        }

        // ---------- Aura bám thân ----------

        /// <summary>Stone Fortress: hào quang + lớp giáp đá.</summary>
        public void SetFortressActive(bool active)
        {
            SetActive(m_FortressAura, active);
            SetActive(m_StoneArmor, active);
        }

        /// <summary>Immovable Titan: hào quang kim loại + chốt đá neo xuống đất.</summary>
        public void SetImmovableActive(bool active)
        {
            SetActive(m_MetallicGlow, active);
            SetActive(m_AnchorSpikes, active);
        }

        /// <summary>Guardian Wall: màn chắn + sương bao quanh.</summary>
        public void SetGuardianActive(bool active)
        {
            SetActive(m_GuardianBarrier, active);
            SetActive(m_GuardianMist, active);
        }

        /// <summary>Earth Guardian: lớp sương bảo vệ (không chiếm màn chắn của Guardian Wall).</summary>
        public void SetEarthGuardianActive(bool active) => SetActive(m_GuardianMist, active);

        // ---------- Hiệu ứng một lần ----------

        /// <summary>Earthquake: vết nứt lan trên đất + sóng xung kích tại tâm.</summary>
        public void ShowEarthquake(Vector2 position)
        {
            if (earthquakeFissures != null)
            {
                GameObject fissure = CreateEffectObject("VFX_EarthquakeFissures", earthquakeFissures, fxSortingOrder);
                fissure.transform.position = position;
                fissure.transform.localScale = Vector3.one * 2.2f;
                Destroy(fissure, fissureDuration);
            }

            PlayOneShot(earthquakeShockwave, position, 0.8f, 3.4f, 0.45f);
        }

        /// <summary>Earth Prison: vòng đá sập xuống khi kết thúc.</summary>
        public void PlayPrisonCollapse(Vector2 position)
        {
            PlayOneShot(prisonCollapse, position, 0.8f, 1.8f, 0.35f);
            PlayOneShot(earthShards, position, 0.7f, 1.6f, 0.3f);
        }

        /// <summary>Earth Break: đá văng tại điểm trúng.</summary>
        public void PlayEarthShards(Vector2 position)
        {
            PlayOneShot(earthShards, position, 0.6f, 1.5f, 0.28f);
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

            GameObject go = CreateEffectObject("VFX_OneShot", sprite, fxSortingOrder + 1);
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
    }
}
