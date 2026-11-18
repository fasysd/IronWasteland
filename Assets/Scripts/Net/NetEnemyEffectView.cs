using System;
using System.Collections.Generic;
using IronWasteland.Enemies;
using IronWasteland.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Vẽ hiệu ứng trạng thái gắn lên quái (cháy, dấu ấn…) do host gửi qua <see cref="NetEffectSync"/>.
    /// Chỉ hiển thị: client không tự quyết định hiệu ứng nào tồn tại, cũng không gây sát thương.
    /// Gắn component này lên root "Network" trong scene (một instance cho toàn trận).
    /// </summary>
    [DisallowMultipleComponent]
    public class NetEnemyEffectView : MonoBehaviour
    {
        [Serializable]
        public class EffectStyle
        {
            [Tooltip("Chuỗi con trong EffectId, ví dụ GreenFox_Burn")]
            public string match = "";

            [Tooltip("Màu vòng hiệu ứng")]
            public Color color = new Color(1f, 0.5f, 0.15f, 0.55f);

            [Tooltip("Đường kính vòng hiệu ứng (unit)")]
            [Min(0.05f)] public float diameter = 1.4f;

            [Tooltip("Lệch so với tâm quái")]
            public Vector3 offset = new Vector3(0f, 0.2f, 0f);

            [Tooltip("Tốc độ nhấp nháy alpha; 0 = tắt")]
            [Min(0f)] public float pulseSpeed = 3f;
        }

        [Header("Kiểu hiển thị theo EffectId")]
        [SerializeField] private List<EffectStyle> styles = new List<EffectStyle>();

        [Header("Cấu hình")]
        [SerializeField, Min(0.05f)] private float pollInterval = 0.1f;
        [SerializeField] private int sortingOrder = 8;
        [SerializeField] private string sortingLayer = "Default";

        private sealed class Entry
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public EffectStyle Style;
            public float Phase;
        }

        private readonly Dictionary<ulong, Dictionary<string, Entry>> m_Entries =
            new Dictionary<ulong, Dictionary<string, Entry>>();

        private readonly Dictionary<ulong, List<string>> m_Seen =
            new Dictionary<ulong, List<string>>();

        private readonly List<NetEffectInfo> m_Buffer = new List<NetEffectInfo>();
        private readonly List<ulong> m_StaleOwners = new List<ulong>();
        private readonly List<string> m_StaleEffects = new List<string>();

        private float m_PollTimer;

        private void Awake()
        {
            if (styles.Count == 0) AddDefaultStyles();
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
            {
                ClearAll();
                return;
            }

            m_PollTimer += Time.deltaTime;
            if (m_PollTimer >= pollInterval)
            {
                m_PollTimer = 0f;
                Poll(manager);
            }

            FollowTargets(manager);
        }

        private void Poll(NetworkManager manager)
        {
            NetEffectSync sync = NetEffectSync.Instance;
            if (sync == null || manager.SpawnManager == null) return;

            foreach (KeyValuePair<ulong, List<string>> pair in m_Seen) pair.Value.Clear();

            foreach (KeyValuePair<ulong, NetworkObject> pair in manager.SpawnManager.SpawnedObjects)
            {
                NetworkObject netObj = pair.Value;
                if (netObj == null || !netObj.IsSpawned) continue;
                if (!netObj.TryGetComponent(out EnemyBrain _)) continue;

                ulong netId = netObj.NetworkObjectId;
                int count = sync.CopyEnemyEffects(netId, m_Buffer);
                if (count == 0) continue;

                if (!m_Entries.TryGetValue(netId, out Dictionary<string, Entry> map))
                {
                    map = new Dictionary<string, Entry>();
                    m_Entries[netId] = map;
                }

                if (!m_Seen.TryGetValue(netId, out List<string> seen))
                {
                    seen = new List<string>();
                    m_Seen[netId] = seen;
                }

                for (int i = 0; i < count; i++)
                {
                    NetEffectInfo info = m_Buffer[i];
                    if (string.IsNullOrEmpty(info.EffectId)) continue;
                    seen.Add(info.EffectId);
                    EnsureEntry(netId, info.EffectId, map);
                }
            }

            RemoveStale(manager);
        }

        private void RemoveStale(NetworkManager manager)
        {
            m_StaleOwners.Clear();

            foreach (KeyValuePair<ulong, Dictionary<string, Entry>> ownerPair in m_Entries)
            {
                ulong netId = ownerPair.Key;
                Dictionary<string, Entry> map = ownerPair.Value;

                bool ownerAlive = manager.SpawnManager.SpawnedObjects.ContainsKey(netId);
                bool hasSeen = m_Seen.TryGetValue(netId, out List<string> seen);

                m_StaleEffects.Clear();
                foreach (KeyValuePair<string, Entry> effectPair in map)
                {
                    bool stillThere = ownerAlive && hasSeen && seen.Contains(effectPair.Key);
                    if (!stillThere) m_StaleEffects.Add(effectPair.Key);
                }

                foreach (string effectId in m_StaleEffects)
                {
                    if (map.TryGetValue(effectId, out Entry entry)) DestroyEntry(entry);
                    map.Remove(effectId);
                }

                if (map.Count == 0) m_StaleOwners.Add(netId);
            }

            foreach (ulong netId in m_StaleOwners)
            {
                m_Entries.Remove(netId);
                m_Seen.Remove(netId);
            }
        }

        private void EnsureEntry(ulong netId, string effectId, Dictionary<string, Entry> map)
        {
            if (map.TryGetValue(effectId, out Entry existing) && existing.Go != null) return;

            EffectStyle style = StyleFor(effectId);
            GameObject go = new GameObject("NetFx_" + effectId);
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteFactory.SoftCircle;
            renderer.color = style.color;
            renderer.sortingOrder = sortingOrder;
            if (!string.IsNullOrEmpty(sortingLayer)) renderer.sortingLayerName = sortingLayer;

            float extent = renderer.sprite != null ? renderer.sprite.bounds.size.x : 1f;
            if (extent > 0.0001f) go.transform.localScale = Vector3.one * (style.diameter / extent);

            map[effectId] = new Entry
            {
                Go = go,
                Renderer = renderer,
                Style = style,
                Phase = (netId % 97) * 0.37f
            };
        }

        private void FollowTargets(NetworkManager manager)
        {
            if (manager.SpawnManager == null || m_Entries.Count == 0) return;

            foreach (KeyValuePair<ulong, Dictionary<string, Entry>> ownerPair in m_Entries)
            {
                if (!manager.SpawnManager.SpawnedObjects.TryGetValue(ownerPair.Key, out NetworkObject netObj) || netObj == null)
                    continue;

                Vector3 center = netObj.transform.position;

                foreach (KeyValuePair<string, Entry> effectPair in ownerPair.Value)
                {
                    Entry entry = effectPair.Value;
                    if (entry.Go == null) continue;

                    entry.Go.transform.position = center + entry.Style.offset;

                    if (entry.Style.pulseSpeed > 0f)
                    {
                        Color color = entry.Style.color;
                        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * entry.Style.pulseSpeed + entry.Phase);
                        color.a *= pulse;
                        entry.Renderer.color = color;
                    }
                }
            }
        }

        private EffectStyle StyleFor(string effectId)
        {
            for (int i = 0; i < styles.Count; i++)
            {
                EffectStyle style = styles[i];
                if (style == null || string.IsNullOrEmpty(style.match)) continue;
                if (effectId.IndexOf(style.match, StringComparison.OrdinalIgnoreCase) >= 0) return style;
            }

            // Fallback: vòng cam mờ để vẫn thấy là quái đang có hiệu ứng lạ.
            return DefaultStyle;
        }

        private void ClearAll()
        {
            foreach (KeyValuePair<ulong, Dictionary<string, Entry>> ownerPair in m_Entries)
            {
                foreach (KeyValuePair<string, Entry> effectPair in ownerPair.Value)
                {
                    DestroyEntry(effectPair.Value);
                }
            }

            m_Entries.Clear();
            m_Seen.Clear();
        }

        private static void DestroyEntry(Entry entry)
        {
            if (entry == null || entry.Go == null) return;
            Destroy(entry.Go);
            entry.Go = null;
        }

        private void AddDefaultStyles()
        {
            styles.Add(new EffectStyle
            {
                match = "Burn",
                color = new Color(1f, 0.42f, 0.08f, 0.55f),
                diameter = 1.7f,
                offset = new Vector3(0f, 0.1f, 0f),
                pulseSpeed = 6f
            });

            styles.Add(new EffectStyle
            {
                match = "Mark",
                color = new Color(0.68f, 0.35f, 1f, 0.6f),
                diameter = 0.95f,
                offset = new Vector3(0f, 0.85f, 0f),
                pulseSpeed = 2.5f
            });

            styles.Add(new EffectStyle
            {
                match = "Tail",
                color = new Color(0.45f, 1f, 0.55f, 0.5f),
                diameter = 1.5f,
                offset = new Vector3(0f, 0.1f, 0f),
                pulseSpeed = 10f
            });
        }

        private static readonly EffectStyle DefaultStyle = new EffectStyle
        {
            match = "",
            color = new Color(1f, 0.62f, 0.2f, 0.5f),
            diameter = 1.2f,
            offset = new Vector3(0f, 0.2f, 0f),
            pulseSpeed = 4f
        };
    }
}
