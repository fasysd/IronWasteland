using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Danh mục visual kỹ năng dùng chung giữa host và client.
    /// Host chỉ gửi Id (int), client tra cùng asset để dựng đúng prefab — VFX không cần NetworkObject.
    /// Asset chuẩn nằm ở Assets/Resources/NetSkillVisualCatalog.asset.
    /// </summary>
    [CreateAssetMenu(fileName = "NetSkillVisualCatalog", menuName = "Iron Wasteland/Net/Skill Visual Catalog")]
    public sealed class NetSkillVisualCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Id ổn định do công cụ sinh catalog gán; host/client phải dùng cùng asset.")]
            public int Id;

            [Tooltip("Prefab visual (VFX_...) không cần NetworkObject.")]
            public GameObject Prefab;

            [Tooltip("Thời gian giữ bản sao trên client nếu host không gửi lệnh thu hồi (0 = mặc định 30 giây).")]
            public float Lifetime;
        }

        private const string ResourceName = "NetSkillVisualCatalog";

        [SerializeField] private Entry[] m_Entries = Array.Empty<Entry>();

        private static readonly Dictionary<int, Entry> s_Lookup = new Dictionary<int, Entry>(64);
        private static readonly Dictionary<GameObject, Entry> s_ByPrefab = new Dictionary<GameObject, Entry>(64);
        private static NetSkillVisualCatalog s_Instance;
        private static bool s_Loaded;

        /// <summary>Asset danh mục dùng chung (null nếu chưa tạo hoặc chưa nằm trong Resources).</summary>
        public static NetSkillVisualCatalog Instance
        {
            get
            {
                if (!s_Loaded)
                {
                    s_Loaded = true;
                    s_Instance = Resources.Load<NetSkillVisualCatalog>(ResourceName);
                    RebuildLookup();
                }

                return s_Instance;
            }
        }

        /// <summary>Tải lại lookup sau khi asset thay đổi trong Editor.</summary>
        public static void RebuildLookup()
        {
            s_Lookup.Clear();
            s_ByPrefab.Clear();
            if (s_Instance == null || s_Instance.m_Entries == null) return;

            Entry[] entries = s_Instance.m_Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry.Id == 0 || entry.Prefab == null) continue;
                if (!s_Lookup.ContainsKey(entry.Id)) s_Lookup.Add(entry.Id, entry);
                if (!s_ByPrefab.ContainsKey(entry.Prefab)) s_ByPrefab.Add(entry.Prefab, entry);
            }
        }

        /// <summary>Tra prefab theo Id; trả false nếu catalog thiếu hoặc Id lạ.</summary>
        public static bool TryGet(int id, out GameObject prefab, out float lifetime)
        {
            prefab = null;
            lifetime = 0f;
            if (id == 0) return false;

            if (Instance == null) return false;
            if (!s_Lookup.TryGetValue(id, out Entry entry))
            {
                RebuildLookup();
                if (!s_Lookup.TryGetValue(id, out entry)) return false;
            }

            prefab = entry.Prefab;
            lifetime = Mathf.Max(0f, entry.Lifetime);
            return prefab != null;
        }

        /// <summary>Id của prefab (0 nếu chưa có trong catalog).</summary>
        public static int FindId(GameObject prefab)
        {
            if (prefab == null || Instance == null) return 0;
            if (!s_ByPrefab.TryGetValue(prefab, out Entry entry))
            {
                RebuildLookup();
                if (!s_ByPrefab.TryGetValue(prefab, out entry)) return 0;
            }

            return entry.Id;
        }

        /// <summary>Lifetime cấu hình sẵn cho prefab (0 nếu không có hoặc bằng 0).</summary>
        public static float GetLifetime(GameObject prefab)
        {
            if (prefab == null || Instance == null) return 0f;
            if (!s_ByPrefab.TryGetValue(prefab, out Entry entry))
            {
                RebuildLookup();
                if (!s_ByPrefab.TryGetValue(prefab, out entry)) return 0f;
            }

            return Mathf.Max(0f, entry.Lifetime);
        }

        /// <summary>Xoá cache để lần truy cập sau tải lại asset vừa sinh trong Editor.</summary>
        public static void InvalidateCache()
        {
            s_Loaded = false;
            s_Instance = null;
            s_Lookup.Clear();
            s_ByPrefab.Clear();
        }

        /// <summary>Dùng cho công cụ Editor: ghi danh sách entry mới.</summary>
        public void SetEntries(Entry[] entries)
        {
            m_Entries = entries ?? Array.Empty<Entry>();
            RebuildLookup();
        }

        /// <summary>Dùng cho công cụ Editor: đọc danh sách entry hiện tại.</summary>
        public Entry[] GetEntries() => m_Entries ?? Array.Empty<Entry>();

        private void OnEnable()
        {
            if (this == s_Instance) RebuildLookup();
        }
    }
}
