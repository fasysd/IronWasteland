using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace IronWasteland.Net
{
    /// <summary>
    /// Bọc Relay + Lobby của Unity Services cho phòng 4 người: đăng nhập ẩn danh, tạo/join allocation,
    /// tạo/join lobby kèm mã Relay. Không phụ thuộc NetworkManager để dùng lại được ở cả host lẫn client.
    /// </summary>
    public static class NetRelayService
    {
        /// <summary>Số người tối đa mỗi phòng (4 người chơi).</summary>
        public const int MaxPlayers = 4;

        /// <summary>Khoá dữ liệu lobby chứa mã Relay.</summary>
        private const string RelayDataKey = "relayJoinCode";

        /// <summary>Unity Services đã init + đăng nhập ẩn danh xong chưa.</summary>
        public static bool ServicesReady { get; private set; }

        /// <summary>Lỗi gần nhất để hiển thị lên UI (null nếu không có).</summary>
        public static string LastError { get; private set; }

        /// <summary>
        /// Profile đăng nhập ẩn danh hiện tại. Hai cửa sổ trên cùng một máy dùng chung profile mặc định
        /// nên Lobby coi là cùng một người chơi; đổi profile cho cửa sổ client để test join bằng mã phòng.
        /// </summary>
        public static string CurrentProfile
        {
            get
            {
                try { return AuthenticationService.Instance.Profile; }
                catch { return "(chua dang nhap)"; }
            }
        }

        /// <summary>Mã Relay host nhận được (dùng cho người chơi join trực tiếp).</summary>
        public static string RelayJoinCode { get; private set; }

        /// <summary>Mã phòng lobby (ngắn, dùng để bạn bè nhập vào UI).</summary>
        public static string LobbyCode { get; private set; }

        /// <summary>Id lobby đang tham gia (rỗng nếu chưa vào phòng nào).</summary>
        public static string LobbyId { get; private set; }

        /// <summary>True khi máy này là người tạo phòng (chỉ host mới giữ lobby sống bằng heartbeat).</summary>
        public static bool IsLobbyHost { get; private set; }

        /// <summary>Nhịp gửi heartbeat giữ lobby không bị Unity Services coi là rảnh.</summary>
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

        private static CancellationTokenSource s_HeartbeatCts;
        private static string s_HeartbeatLobbyId;

        /// <summary>Khởi tạo Unity Services và đăng nhập ẩn danh nếu chưa có phiên.</summary>
        /// <summary>
        /// Đổi profile đăng nhập ẩn danh (dùng để test nhiều "người chơi" trên cùng một máy).
        /// Phải đăng xuất trước; SDK sẽ tạo player id mới cho profile mới.
        /// </summary>
        public static async Task<bool> SwitchProfileAsync(string profileName)
        {
            LastError = null;
            if (string.IsNullOrWhiteSpace(profileName))
            {
                LastError = "Tên profile không được để trống.";
                return false;
            }

            try
            {
                await UnityServices.InitializeAsync();

                if (AuthenticationService.Instance.IsSignedIn)
                {
                    AuthenticationService.Instance.SignOut();
                }

                AuthenticationService.Instance.SwitchProfile(profileName.Trim());
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                ServicesReady = true;
                return true;
            }
            catch (Exception exception)
            {
                LastError = $"Đổi profile lỗi: {exception.Message}";
                ServicesReady = false;
                return false;
            }
        }

        public static async Task<bool> EnsureServicesAsync()
        {
            if (ServicesReady) return true;

            LastError = null;
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                ServicesReady = true;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                ServicesReady = false;
                return false;
            }
        }

        /// <summary>
        /// Tạo allocation Relay cho host (chừa 3 chỗ cho khách) và lấy mã join code.
        /// Trả null nếu lỗi; đọc <see cref="LastError"/> để biết lý do.
        /// </summary>
        public static async Task<RelayServerData?> CreateHostAllocationAsync(string region = null)
        {
            if (!await EnsureServicesAsync()) return null;

            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1, region);
                RelayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                return CreateRelayServerData(allocation);
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                RelayJoinCode = null;
                return null;
            }
        }

        /// <summary>Join allocation Relay bằng mã join code; trả null nếu lỗi.</summary>
        public static async Task<RelayServerData?> JoinAllocationAsync(string relayJoinCode)
        {
            if (!await EnsureServicesAsync()) return null;

            if (string.IsNullOrWhiteSpace(relayJoinCode))
            {
                LastError = "Mã Relay trống.";
                return null;
            }

            try
            {
                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode.Trim().ToUpperInvariant());
                return CreateRelayServerData(allocation);
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return null;
            }
        }

        /// <summary>Tạo lobby công khai gắn mã Relay vừa tạo. Host gọi sau khi đã có allocation.</summary>
        public static async Task<bool> CreateLobbyAsync(string lobbyName)
        {
            if (!ServicesReady)
            {
                LastError = "Unity Services chưa sẵn sàng.";
                return false;
            }

            if (string.IsNullOrEmpty(RelayJoinCode))
            {
                LastError = "Chưa có mã Relay để gắn vào lobby.";
                return false;
            }

            try
            {
                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    // Chỉ thành viên phòng mới đọc được mã Relay; người ngoài phải join bằng mã phòng
                    // rồi mới lấy được dữ liệu (không lộ mã Relay qua danh sách công khai).
                    Data = new Dictionary<string, DataObject>
                    {
                        { RelayDataKey, new DataObject(DataObject.VisibilityOptions.Member, RelayJoinCode) }
                    }
                };

                string name = string.IsNullOrWhiteSpace(lobbyName) ? "IronWasteland" : lobbyName;
                Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(name, MaxPlayers, options);
                LobbyCode = lobby.LobbyCode;
                LobbyId = lobby.Id;
                IsLobbyHost = true;
                StartHeartbeat(lobby.Id);
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return false;
            }
        }

        /// <summary>Join lobby bằng mã phòng và trả về mã Relay để kết nối transport.</summary>
        /// <summary>
        /// Tìm mã Relay của một phòng công khai mà KHÔNG join.
        /// Dùng cho test trên cùng một máy: hai tiến trình dùng chung danh tính Unity Services nên
        /// JoinLobbyByCodeAsync sẽ báo "player is already a member of the lobby".
        /// </summary>
        public static async Task<string> FindLobbyRelayCodeAsync(string lobbyCode)
        {
            if (!await EnsureServicesAsync()) return null;

            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                LastError = "Mã phòng trống.";
                return null;
            }

            try
            {
                QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions { Count = 20 });
                if (response == null || response.Results == null)
                {
                    LastError = "Không truy vấn được danh sách phòng.";
                    return null;
                }

                string wanted = lobbyCode.Trim();
                for (int i = 0; i < response.Results.Count; i++)
                {
                    Lobby lobby = response.Results[i];
                    if (lobby == null) continue;
                    if (!string.Equals(lobby.LobbyCode, wanted, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(lobby.Id, wanted, StringComparison.OrdinalIgnoreCase)) continue;

                    if (lobby.Data != null && lobby.Data.TryGetValue(RelayDataKey, out DataObject data) && data != null)
                    {
                        return data.Value;
                    }

                    LastError = "Phòng không chứa mã Relay.";
                    return null;
                }

                LastError = $"Không thấy phòng {wanted} trong danh sách công khai.";
                return null;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return null;
            }
        }

        /// <summary>Join phòng bằng mã và trả mã Relay (dùng cho người chơi thật, khác danh tính host).</summary>
        public static async Task<string> JoinLobbyAndGetRelayCodeAsync(string lobbyCode)
        {
            if (!await EnsureServicesAsync()) return null;

            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                LastError = "Mã phòng trống.";
                return null;
            }

            try
            {
                Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode.Trim());
                LobbyCode = lobby.LobbyCode;
                LobbyId = lobby.Id;
                IsLobbyHost = false;

                if (lobby.Data != null && lobby.Data.TryGetValue(RelayDataKey, out DataObject data) && data != null)
                {
                    return data.Value;
                }

                LastError = "Phòng không chứa mã Relay.";
                return null;
            }
            catch (Exception exception)
            {
                LastError = DescribeJoinError(exception);
                return null;
            }
        }

        /// <summary>Chuyển lỗi join lobby thành câu tiếng Việt dễ hiểu cho người chơi.</summary>
        private static string DescribeJoinError(Exception exception)
        {
            string message = exception.Message ?? string.Empty;
            string lower = message.ToLowerInvariant();

            if (lower.Contains("already a member"))
            {
                return "Tài khoản này đã ở trong phòng (2 cửa sổ trên cùng máy dùng chung profile). " +
                       "Đổi profile ở cửa sổ này rồi thử lại.";
            }

            if (lower.Contains("not found"))
            {
                return "Không tìm thấy phòng (có thể đã đóng hoặc mã sai).";
            }

            return "Vào phòng lỗi: " + message;
        }

        /// <summary>Bắt đầu gửi heartbeat giữ lobby sống (chỉ host gọi).</summary>
        private static void StartHeartbeat(string lobbyId)
        {
            StopHeartbeat();
            if (string.IsNullOrEmpty(lobbyId)) return;

            s_HeartbeatLobbyId = lobbyId;
            s_HeartbeatCts = new CancellationTokenSource();
            HeartbeatLoopAsync(s_HeartbeatCts.Token);
        }

        /// <summary>Dừng heartbeat khi rời phòng/đóng phòng.</summary>
        public static void StopHeartbeat()
        {
            if (s_HeartbeatCts == null) return;

            try { s_HeartbeatCts.Cancel(); }
            catch { /* token đã huỷ */ }

            s_HeartbeatCts.Dispose();
            s_HeartbeatCts = null;
            s_HeartbeatLobbyId = null;
        }

        /// <summary>Vòng lặp gửi heartbeat 15 giây/lần để lobby không bị coi là rảnh.</summary>
        private static async void HeartbeatLoopAsync(CancellationToken token)
        {
            string lobbyId = s_HeartbeatLobbyId;
            if (string.IsNullOrEmpty(lobbyId)) return;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(HeartbeatInterval, token);
                    if (token.IsCancellationRequested) break;
                    await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
                }
            }
            catch (OperationCanceledException)
            {
                // Rời phòng bình thường — không phải lỗi.
            }
            catch (Exception exception)
            {
                LastError = $"Heartbeat lobby lỗi: {exception.Message}";
            }
        }

        /// <summary>
        /// Rời lobby đúng cách: thành viên thì RemovePlayer, host thì DeleteLobby để không còn phòng treo
        /// trên Unity Services sau khi đóng game.
        /// </summary>
        public static async Task LeaveSessionAsync()
        {
            StopHeartbeat();

            string lobbyId = LobbyId;
            if (string.IsNullOrEmpty(lobbyId) || !ServicesReady)
            {
                ResetSession();
                return;
            }

            bool wasHost = IsLobbyHost;
            string error = null;
            try
            {
                if (wasHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                }
                else if (AuthenticationService.Instance.IsSignedIn)
                {
                    await LobbyService.Instance.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId);
                }
            }
            catch (Exception exception)
            {
                // Phòng có thể đã bị host xoá trước đó; chỉ ghi nhận để log/UI biết.
                error = $"Rời phòng lỗi: {exception.Message}";
            }
            finally
            {
                ResetSession();
                if (error != null) LastError = error;
            }
        }

        /// <summary>Rời phòng không cần chờ (dùng trong lúc Shutdown).</summary>
        public static void LeaveSession()
        {
            _ = LeaveSessionAsync();
        }

        /// <summary>Xoá trạng thái lobby/Relay khi đóng phòng để lần sau tạo mới sạch.</summary>
        public static void ResetSession()
        {
            StopHeartbeat();
            RelayJoinCode = null;
            LobbyCode = null;
            LobbyId = null;
            IsLobbyHost = false;
            LastError = null;
        }

        /// <summary>
        /// Dựng <see cref="RelayServerData"/> cho host từ allocation Relay.
        /// Bản UTP trong project chưa có constructor nhận thẳng Allocation nên phải truyền từng trường.
        /// </summary>
        private static RelayServerData CreateRelayServerData(Allocation allocation)
        {
            return new RelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.ConnectionData,
                allocation.ConnectionData,
                allocation.Key,
                isSecure: false);
        }

        /// <summary>Dựng <see cref="RelayServerData"/> cho client từ join allocation.</summary>
        private static RelayServerData CreateRelayServerData(JoinAllocation allocation)
        {
            return new RelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.ConnectionData,
                allocation.HostConnectionData,
                allocation.Key,
                isSecure: false);
        }
    }
}
