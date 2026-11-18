using System;
using System.Collections.Generic;
using IronWasteland.Enemies;
using IronWasteland.Tanks.GreenFox;
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

        /// <summary>Prefab hiệu ứng thật để client dựng giống host (thay cho vòng tròn mờ).</summary>
        [Serializable]
        public class EffectPrefab
        {
            [Tooltip("Chuỗi con trong EffectId, ví dụ GreenFox_Burn")]
            public string match = "";

            [Tooltip("Prefab hiệu ứng thật (để trống thì dùng vòng tròn mờ)")]
            public GameObject prefab;

            [Tooltip("Prefab tự bám theo quái (cháy, ấn) — bật thì view không tự di chuyển nó")]
            public bool selfFollow = true;
        }

        [Header("Prefab hiệu ứng thật (client dựng giống host)")]
        [SerializeField] private List<EffectPrefab> effectPrefabs = new List<EffectPrefab>();

        [Header("Cấu hình")]
        [SerializeField, Min(0.05f)] private float pollInterval = 0.1f;
        [SerializeField] private int sortingOrder = 8;
        [SerializeField] private string sortingLayer = "Default";

        private sealed class Entry
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public EffectStyle Style;
            public GreenFox_BurnEffect Burn;
            public Sign Sign;
            public bool SelfFollow;
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
            // Host đã tự dựng prefab hiệu ứng gốc khi gây trạng thái; không vẽ ghost đè lên nữa.
            if (manager == null || !manager.IsListening || manager.IsServer)
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
                if (!netObj.TryGetComponent(out EnemyBrain enemyBrain)) continue;

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
                    EnsureEntry(netId, info, enemyBrain, map);
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

        private void EnsureEntry(ulong netId, NetEffectInfo info, EnemyBrain enemy, Dictionary<string, Entry> map)
        {
            string effectId = info.EffectId;
            float duration = info.TotalDuration > 0f ? info.TotalDuration : 8f;

            if (map.TryGetValue(effectId, out Entry existing) && existing.Go != null)
            {
                // Đòn mới trúng quái: host báo làm mới, chỉ gia hạn hiển thị.
                if (existing.Burn != null)
                {
                    existing.Burn.RefreshVisualOnly(enemy, duration);
                }
                else if (existing.Sign != null && enemy != null)
                {
                    existing.Sign.Refresh(duration);
                    existing.Sign.SetInitialStacks(Mathf.Max(1, info.Stacks));
                }
                return;
            }

            EffectPrefab prefabStyle = PrefabFor(effectId);
            if (prefabStyle != null && prefabStyle.prefab != null && enemy != null)
            {
                GameObject instance = Instantiate(prefabStyle.prefab, enemy.transform.position, Quaternion.identity);
                GreenFox_BurnEffect burn = instance.GetComponent<GreenFox_BurnEffect>();
                Sign sign = null;
                if (burn != null)
                {
                    burn.InitializeVisualOnly(enemy, duration);
                }
                else
                {
                    sign = instance.GetComponent<Sign>();
                    if (sign != null)
                    {
                        sign.SetTarget(enemy, duration, null);
                        sign.SetInitialStacks(Mathf.Max(1, info.Stacks));
                    }
                    else
                    {
                        Destroy(instance, duration);
                    }
                }

                map[effectId] = new Entry
                {
                    Go = instance,
                    Style = StyleFor(effectId),
                    Burn = burn,
                    Sign = sign,
                    SelfFollow = prefabStyle.selfFollow,
                    Phase = (netId % 97) * 0.37f
                };
                return;
            }

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
                SelfFollow = false,
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
                    if (entry.Go == null || entry.SelfFollow) continue;

                    entry.Go.transform.position = center + entry.Style.offset;

                    if (entry.Renderer != null && entry.Style.pulseSpeed > 0f)
                    {
                        Color color = entry.Style.color;
                        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * entry.Style.pulseSpeed + entry.Phase);
                        color.a *= pulse;
                        entry.Renderer.color = color;
                    }
                }
            }
        }

        private EffectPrefab PrefabFor(string effectId)
        {
            for (int i = 0; i < effectPrefabs.Count; i++)
            {
                EffectPrefab entry = effectPrefabs[i];
                if (entry == null || entry.prefab == null || string.IsNullOrEmpty(entry.match)) continue;
                if (effectId.IndexOf(entry.match, StringComparison.OrdinalIgnoreCase) >= 0) return entry;
            }

            return null;
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

            styles.Add(new EffectStyle
            {
                match = "Root",
                color = new Color(0.9f, 0.85f, 0.2f, 0.6f),
                diameter = 1.25f,
                offset = new Vector3(0f, 0.15f, 0f),
                pulseSpeed = 4f
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
