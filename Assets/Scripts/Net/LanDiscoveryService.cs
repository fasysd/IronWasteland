using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using IronWasteland.Net.Models;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Quảng bá phòng LAN bằng UDP broadcast và gom các phòng nghe được. Chạy trên object
    /// DontDestroyOnLoad của NetBootstrap nên phòng vẫn được phát khi host đã vào trận.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanDiscoveryService : MonoBehaviour
    {
        /// <summary>Cổng UDP riêng cho quảng bá phòng, không đụng cổng game 7777.</summary>
        public const int DiscoveryPort = 47777;

        private const float BroadcastInterval = 1f;
        private const double RoomLifetimeSeconds = 4.5;
        private const int MaxPlayers = 4;

        private struct SeenRoom
        {
            public LanRoomInfo Room;
            public DateTime SeenAtUtc;
        }

        private readonly object m_Lock = new object();
        private readonly Dictionary<string, SeenRoom> m_Seen = new Dictionary<string, SeenRoom>();
        private readonly object m_BroadcastLock = new object();

        private UdpClient m_Listener;
        private Thread m_ListenThread;
        private volatile bool m_Listening;
        private UdpClient m_Broadcaster;
        private LanRoomInfo m_HostRoom;
        private bool m_Hosting;
        private float m_BroadcastCooldown;

        /// <summary>True khi máy này đang phát phòng.</summary>
        public bool IsHosting => m_Hosting;

        /// <summary>Danh sách phòng còn sống, sắp theo tên; an toàn khi gọi từ main thread.</summary>
        public IReadOnlyList<LanRoomInfo> Rooms
        {
            get
            {
                List<LanRoomInfo> rooms;
                lock (m_Lock)
                {
                    DateTime deadline = DateTime.UtcNow.AddSeconds(-RoomLifetimeSeconds);
                    List<string> stale = new List<string>();
                    foreach (KeyValuePair<string, SeenRoom> pair in m_Seen)
                    {
                        if (pair.Value.SeenAtUtc < deadline) stale.Add(pair.Key);
                    }

                    for (int i = 0; i < stale.Count; i++) m_Seen.Remove(stale[i]);

                    rooms = new List<LanRoomInfo>(m_Seen.Count);
                    foreach (SeenRoom seen in m_Seen.Values) rooms.Add(seen.Room);
                }

                rooms.Sort((a, b) =>
                {
                    int byName = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                    return byName != 0 ? byName : string.Compare(a.Address, b.Address, StringComparison.Ordinal);
                });
                return rooms;
            }
        }

        private void Awake()
        {
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            StartListening();
        }

        private void Update()
        {
            if (!m_Hosting) return;
            m_BroadcastCooldown -= Time.unscaledDeltaTime;
            if (m_BroadcastCooldown > 0f) return;
            m_BroadcastCooldown = BroadcastInterval;
            BroadcastHostRoom();
        }

        /// <summary>Bắt đầu phát phòng của máy này; stageCode quyết định dòng ải trong danh sách.</summary>
        public void BeginHost(string stageCode, ushort port, int players = 1)
        {
            string machine = Environment.MachineName;
            if (string.IsNullOrWhiteSpace(machine)) machine = "MAY-HOST";
            string roomName = "PHÒNG CỦA " + machine;

            lock (m_Lock)
            {
                m_HostRoom = new LanRoomInfo(string.Empty, port, roomName, stageCode, players, MaxPlayers, false);
            }

            StartListening();
            m_Hosting = true;
            m_BroadcastCooldown = 0f;
            BroadcastHostRoom();
        }

        /// <summary>Cập nhật số người/đang trận để dòng phòng bên client luôn đúng.</summary>
        public void UpdateHost(int players, bool inMatch)
        {
            if (!m_Hosting) return;
            lock (m_Lock)
            {
                m_HostRoom = new LanRoomInfo(string.Empty, m_HostRoom.Port, m_HostRoom.Name, m_HostRoom.StageCode, Mathf.Clamp(players, 1, MaxPlayers), MaxPlayers, inMatch);
            }
        }

        /// <summary>Ngừng phát phòng nhưng vẫn giữ listener cho lần tạo phòng sau.</summary>
        public void EndHost()
        {
            lock (m_Lock)
            {
                // Xoá phòng của chính máy này khỏi danh sách đã nghe để host không thấy
                // và bấm nhầm phòng của mình ngay sau khi đóng phòng.
                string ownName = m_HostRoom.Name;
                int ownPort = m_HostRoom.Port;
                List<string> ownKeys = new List<string>();
                foreach (KeyValuePair<string, SeenRoom> pair in m_Seen)
                {
                    if (pair.Value.Room.Port == ownPort && string.Equals(pair.Value.Room.Name, ownName, StringComparison.Ordinal))
                    {
                        ownKeys.Add(pair.Key);
                    }
                }

                for (int i = 0; i < ownKeys.Count; i++) m_Seen.Remove(ownKeys[i]);
                m_Hosting = false;
            }
        }

        private void BroadcastHostRoom()
        {
            LanRoomInfo room;
            lock (m_Lock) room = m_HostRoom;
            if (string.IsNullOrEmpty(room.Name)) return;

            string payload = LanRoomProtocol.Encode(room);
            if (string.IsNullOrEmpty(payload)) return;
            byte[] data = Encoding.UTF8.GetBytes(payload);

            lock (m_BroadcastLock)
            {
                try
                {
                    if (m_Broadcaster == null)
                    {
                        m_Broadcaster = new UdpClient();
                        m_Broadcaster.EnableBroadcast = true;
                    }

                    m_Broadcaster.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[LanDiscovery] Không gửi được gói quảng bá: " + exception.Message);
                }
            }
        }

        private void StartListening()
        {
            if (m_Listening) return;

            try
            {
                m_Listener = new UdpClient();
                m_Listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                m_Listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                m_Listener.Client.ReceiveTimeout = 250;
                m_Listener.EnableBroadcast = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LanDiscovery] Không mở được cổng " + DiscoveryPort + " — chỉ còn cách nhập IP thủ công. " + exception.Message);
                m_Listener = null;
                return;
            }

            m_Listening = true;
            m_ListenThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "LanDiscoveryListener",
            };
            m_ListenThread.Start();
        }

        private void ListenLoop()
        {
            while (m_Listening)
            {
                try
                {
                    IPEndPoint remote = null;
                    byte[] data = m_Listener.Receive(ref remote);
                    if (data == null || data.Length == 0 || remote == null) continue;

                    string payload = Encoding.UTF8.GetString(data);
                    if (!LanRoomProtocol.TryDecode(payload, out LanRoomInfo room)) continue;
                    if (room.Port <= 0) continue;

                    string key = remote.Address + ":" + room.Port;
                    LanRoomInfo addressed = room.WithAddress(remote.Address.ToString());
                    if (IsOwnRoom(addressed)) continue;
                    lock (m_Lock)
                    {
                        m_Seen[key] = new SeenRoom { Room = addressed, SeenAtUtc = DateTime.UtcNow };
                    }
                }
                catch (SocketException exception)
                {
                    if (exception.SocketErrorCode != SocketError.TimedOut && m_Listening)
                    {
                        Debug.LogWarning("[LanDiscovery] Lỗi nhận gói: " + exception.Message);
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    if (m_Listening) Debug.LogWarning("[LanDiscovery] Lỗi nhận gói: " + exception.Message);
                }
            }
        }

        /// <summary>Gói quảng bá có đúng là phòng của máy này không (host tự nghe broadcast của mình).</summary>
        private bool IsOwnRoom(LanRoomInfo room)
        {
            if (!m_Hosting) return false;
            lock (m_Lock)
            {
                return m_HostRoom.Port == room.Port && string.Equals(m_HostRoom.Name, room.Name, StringComparison.Ordinal);
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        /// <summary>Đóng socket và dừng luồng nền khi object bị huỷ hoặc thoát game.</summary>
        public void Shutdown()
        {
            m_Hosting = false;
            m_Listening = false;

            try
            {
                m_Listener?.Close();
            }
            catch (Exception)
            {
                // Socket đã đóng thì bỏ qua.
            }

            lock (m_BroadcastLock)
            {
                try
                {
                    m_Broadcaster?.Close();
                }
                catch (Exception)
                {
                    // Socket đã đóng thì bỏ qua.
                }

                m_Broadcaster = null;
            }

            if (m_ListenThread != null && m_ListenThread.IsAlive)
            {
                m_ListenThread.Join(200);
            }

            // Chỉ xoá tham chiếu sau khi luồng nền đã dừng để nó không deref null.
            m_Listener = null;
            m_ListenThread = null;
            lock (m_Lock) m_Seen.Clear();
        }
    }
}
