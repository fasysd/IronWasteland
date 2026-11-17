using System.Collections.Generic;
using IronWasteland.Core.Effects;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>Thông tin một hiệu ứng trạng thái mà client nhận từ host (chỉ để hiển thị).</summary>
    public struct NetEffectInfo
    {
        public string EffectId;
        public int Stacks;
        public float TotalDuration;
        public float RemainingAtReceive;
        public double ServerTimeAtReceive;
    }

    /// <summary>
    /// Đồng bộ danh sách hiệu ứng trạng thái (ActiveEffects) từ host xuống client bằng named message.
    /// Client chỉ lưu để hiển thị, không áp hiệu ứng lên brain (tránh client tự buff/debuff).
    /// Khiên đi kèm theo <see cref="PlayerNetState"/> (NetworkVariable) vì chỉ cần một con số tổng.
    /// </summary>
    public class NetEffectSync : MonoBehaviour
    {
        private const string MessageName = "IW.Effect";
        private const int MaxIdBytes = 48;
        public const byte OpApply = 0;
        public const byte OpRemove = 1;

        private static NetEffectSync s_Instance;

        /// <summary>Instance trên root Network; null khi scene không gắn component này.</summary>
        public static NetEffectSync Instance => s_Instance;

        private bool m_Registered;
        private readonly Dictionary<ulong, Dictionary<string, NetEffectInfo>> m_Effects =
            new Dictionary<ulong, Dictionary<string, NetEffectInfo>>();

        private void Awake()
        {
            s_Instance = this;
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
            Unregister();
        }

        private void Update()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null) return;

            if (!m_Registered && manager.IsListening && manager.CustomMessagingManager != null)
            {
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, HandleMessage);
                m_Registered = true;
            }
            else if (m_Registered && !manager.IsListening)
            {
                Unregister();
            }
        }

        private void Unregister()
        {
            if (!m_Registered) return;
            m_Registered = false;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.CustomMessagingManager == null) return;
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
        }

        /// <summary>Host gửi trạng thái một hiệu ứng cho toàn phòng; client chỉ ghi nhận để vẽ.</summary>
        public void Broadcast(ulong ownerClientId, byte op, StatusEffect effect)
        {
            if (effect == null) return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || manager.CustomMessagingManager == null) return;

            FixedString64Bytes id = new FixedString64Bytes(Trim(effect.EffectId));
            using FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(op);
            writer.WriteValueSafe(ownerClientId);
            writer.WriteValueSafe(id);
            writer.WriteValueSafe(effect.CurrentStacks);
            writer.WriteValueSafe(effect.Duration);
            writer.WriteValueSafe(effect.RemainingTime);
            writer.WriteValueSafe(manager.ServerTime.Time);
            manager.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer);
        }

        private void HandleMessage(ulong senderClientId, FastBufferReader reader)
        {
            // Chỉ nhận từ host — client khác không được giả hiệu ứng.
            if (senderClientId != NetworkManager.ServerClientId) return;

            reader.ReadValueSafe(out byte op);
            reader.ReadValueSafe(out ulong ownerClientId);
            reader.ReadValueSafe(out FixedString64Bytes id);
            reader.ReadValueSafe(out int stacks);
            reader.ReadValueSafe(out float totalDuration);
            reader.ReadValueSafe(out float remaining);
            reader.ReadValueSafe(out double serverTime);

            string effectId = id.ToString();
            if (string.IsNullOrEmpty(effectId)) return;

            if (!m_Effects.TryGetValue(ownerClientId, out Dictionary<string, NetEffectInfo> list))
            {
                list = new Dictionary<string, NetEffectInfo>();
                m_Effects[ownerClientId] = list;
            }

            if (op == OpRemove)
            {
                list.Remove(effectId);
                return;
            }

            list[effectId] = new NetEffectInfo
            {
                EffectId = effectId,
                Stacks = stacks,
                TotalDuration = totalDuration,
                RemainingAtReceive = remaining,
                ServerTimeAtReceive = serverTime
            };
        }

        /// <summary>Số giây còn lại ước tính tại thời điểm hiện tại (client tự trừ theo ServerTime).</summary>
        public static float EstimateRemaining(NetEffectInfo info)
        {
            NetworkManager manager = NetworkManager.Singleton;
            double now = manager != null ? manager.ServerTime.Time : info.ServerTimeAtReceive;
            double elapsed = now - info.ServerTimeAtReceive;
            return Mathf.Max(0f, info.RemainingAtReceive - (float)elapsed);
        }

        /// <summary>Danh sách hiệu ứng đang hiển thị của một xe (rỗng nếu chưa có).</summary>
        public IReadOnlyCollection<NetEffectInfo> GetEffects(ulong ownerClientId)
        {
            if (!m_Effects.TryGetValue(ownerClientId, out Dictionary<string, NetEffectInfo> list))
                return System.Array.Empty<NetEffectInfo>();

            List<NetEffectInfo> result = new List<NetEffectInfo>(list.Count);
            foreach (KeyValuePair<string, NetEffectInfo> pair in list)
            {
                NetEffectInfo info = pair.Value;
                if (EstimateRemaining(info) <= 0f && info.TotalDuration > 0f) continue;
                result.Add(info);
            }

            return result;
        }

        private static string Trim(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length <= MaxIdBytes) return value;
            return value.Substring(0, MaxIdBytes);
        }
    }
}
