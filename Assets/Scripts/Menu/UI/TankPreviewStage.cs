using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Sân khấu xem xe: dựng prefab <see cref="IronWasteland.Tanks.TankView"/> thật ở một vị trí rất xa gốc tọa độ,
    /// quay bằng một camera riêng ra RenderTexture rồi hiển thị trong UI bằng RawImage.
    /// (Canvas Overlay không vẽ được SpriteRenderer, nên phải đi đường RenderTexture.)
    ///
    /// - <see cref="Show"/>: chế độ trực tiếp (live), xe có animation, dùng ở Main Menu / Nhà xe / Chọn ải.
    /// - <see cref="GetSnapshot"/>: ảnh tĩnh nhỏ của từng xe, dùng cho các hàng trong danh sách xe.
    /// Xe được quay mặt xuống dưới (giống mockup) bằng cách xoay camera, không đụng tới prefab.
    /// </summary>
    public sealed class TankPreviewStage : MonoBehaviour
    {
        private const float StageX = 5000f;
        private const float StageY = 5000f;
        private const int LiveSize = 1024;
        private const int SnapshotSize = 256;

        private Camera m_Camera;
        private RenderTexture m_LiveTexture;
        private GameObject m_Current;

        private readonly Dictionary<string, GameObject> m_Instances = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, RenderTexture> m_Snapshots = new Dictionary<string, RenderTexture>();

        public Texture LiveTexture => m_LiveTexture;

        private void Awake()
        {
            transform.position = new Vector3(StageX, StageY, 0f);

            m_LiveTexture = CreateTexture(LiveSize, "TankPreviewLive");

            var camGo = new GameObject("TankPreviewCamera");
            camGo.transform.SetParent(transform, false);

            m_Camera = camGo.AddComponent<Camera>();
            m_Camera.orthographic = true;
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = new Color(UITheme.PanelDark.r, UITheme.PanelDark.g, UITheme.PanelDark.b, 0f);
            m_Camera.cullingMask = ~(1 << 5); // bỏ layer UI
            m_Camera.allowHDR = false;
            m_Camera.allowMSAA = false;
            m_Camera.depth = -50f;
            m_Camera.nearClipPlane = 0.1f;
            m_Camera.farClipPlane = 100f;
            // Xoay camera +90 độ: xe (đầu nòng hướng +X) hiện ra quay mặt xuống dưới như mockup.
            m_Camera.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            m_Camera.targetTexture = m_LiveTexture;
            m_Camera.enabled = false;
        }

        private void OnDestroy()
        {
            if (m_LiveTexture != null) m_LiveTexture.Release();
            foreach (KeyValuePair<string, RenderTexture> pair in m_Snapshots)
                if (pair.Value != null) pair.Value.Release();
        }

        // ============================================================
        // TRỰC TIẾP
        // ============================================================

        /// <summary>Hiện xe lên <see cref="LiveTexture"/>. False nếu xe không có TankView để dựng.</summary>
        public bool Show(TankEntry entry)
        {
            GameObject instance = GetInstance(entry);
            if (instance == null)
            {
                Hide();
                return false;
            }

            if (m_Current != null && m_Current != instance) m_Current.SetActive(false);

            m_Current = instance;
            instance.SetActive(true);
            Frame(instance);

            m_Camera.targetTexture = m_LiveTexture;
            m_Camera.enabled = true;
            return true;
        }

        /// <summary>Tắt camera + ẩn xe (gọi khi màn hình đang xem bị ẩn để khỏi tốn render).</summary>
        public void Hide()
        {
            if (m_Current != null) m_Current.SetActive(false);
            m_Current = null;
            if (m_Camera != null) m_Camera.enabled = false;
        }

        // ============================================================
        // ẢNH TĨNH
        // ============================================================

        /// <summary>Ảnh tĩnh của xe (render một lần rồi nhớ lại). Null nếu không dựng được.</summary>
        public Texture GetSnapshot(TankEntry entry)
        {
            if (entry == null) return null;
            if (m_Snapshots.TryGetValue(entry.id, out RenderTexture cached)) return cached;

            GameObject instance = GetInstance(entry);
            if (instance == null)
            {
                m_Snapshots[entry.id] = null;
                return null;
            }

            GameObject previous = m_Current;
            bool cameraWasEnabled = m_Camera.enabled;
            RenderTexture previousTarget = m_Camera.targetTexture;

            if (previous != null && previous != instance) previous.SetActive(false);
            instance.SetActive(true);
            Frame(instance);

            RenderTexture rt = CreateTexture(SnapshotSize, "TankSnapshot_" + entry.id);
            m_Camera.targetTexture = rt;

            try
            {
                m_Camera.Render();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{nameof(TankPreviewStage)}] Không render được ảnh tĩnh của {entry.id}: {e.Message}");
                rt.Release();
                Destroy(rt);
                rt = null;
            }

            m_Camera.targetTexture = previousTarget;
            m_Camera.enabled = cameraWasEnabled;

            if (instance != previous) instance.SetActive(false);
            if (previous != null) previous.SetActive(true);

            m_Snapshots[entry.id] = rt;
            return rt;
        }

        // ============================================================
        // NỘI BỘ
        // ============================================================

        private GameObject GetInstance(TankEntry entry)
        {
            if (entry == null) return null;
            if (m_Instances.TryGetValue(entry.id, out GameObject existing) && existing != null) return existing;

            IronWasteland.Tanks.TankView prefab = TankViewBridge.GetViewPrefab(entry);
            if (prefab == null) return null;

            GameObject go = Instantiate(prefab.gameObject, transform);
            go.name = "Preview_" + entry.id;
            go.transform.localPosition = Vector3.zero;
            go.SetActive(false);

            m_Instances[entry.id] = go;
            return go;
        }

        /// <summary>Căn camera để lọt trọn xe (dựa trên SpriteRenderer; không thì dùng Renderer bất kỳ trừ particle).</summary>
        private void Frame(GameObject instance)
        {
            Bounds bounds = default;
            bool has = false;

            foreach (SpriteRenderer sr in instance.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!has) { bounds = sr.bounds; has = true; }
                else bounds.Encapsulate(sr.bounds);
            }

            if (!has)
            {
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!has) { bounds = r.bounds; has = true; }
                    else bounds.Encapsulate(r.bounds);
                }
            }

            Vector3 center = has ? bounds.center : instance.transform.position;
            float size = has ? Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.2f : 2f;

            m_Camera.orthographicSize = Mathf.Max(size, 0.25f);
            m_Camera.transform.position = new Vector3(center.x, center.y, transform.position.z - 10f);
        }

        private static RenderTexture CreateTexture(int size, string name)
        {
            var rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
            {
                name = name,
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
            };
            rt.Create();
            return rt;
        }
    }
}
