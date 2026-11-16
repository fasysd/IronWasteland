using System;
using System.Collections.Generic;
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

        /// <summary>Mã Relay host nhận được (dùng cho người chơi join trực tiếp).</summary>
        public static string RelayJoinCode { get; private set; }

        /// <summary>Mã phòng lobby (ngắn, dùng để bạn bè nhập vào UI).</summary>
        public static string LobbyCode { get; private set; }

        /// <summary>Id lobby đang tham gia (rỗng nếu chưa vào phòng nào).</summary>
        public static string LobbyId { get; private set; }

        /// <summary>Khởi tạo Unity Services và đăng nhập ẩn danh nếu chưa có phiên.</summary>
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
                    // MVP chưa có đăng nhập người chơi: để Public để phòng hiện trong danh sách công khai
                    // và người khác đọc được mã Relay. Khi làm tài khoản thì đổi lại Member + join bằng mã phòng.
                    Data = new Dictionary<string, DataObject>
                    {
                        { RelayDataKey, new DataObject(DataObject.VisibilityOptions.Public, RelayJoinCode) }
                    }
                };

                string name = string.IsNullOrWhiteSpace(lobbyName) ? "IronWasteland" : lobbyName;
                Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(name, MaxPlayers, options);
                LobbyCode = lobby.LobbyCode;
                LobbyId = lobby.Id;
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

                if (lobby.Data != null && lobby.Data.TryGetValue(RelayDataKey, out DataObject data) && data != null)
                {
                    return data.Value;
                }

                LastError = "Phòng không chứa mã Relay.";
                return null;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                return null;
            }
        }

        /// <summary>Xoá trạng thái lobby/Relay khi đóng phòng để lần sau tạo mới sạch.</summary>
        public static void ResetSession()
        {
            RelayJoinCode = null;
            LobbyCode = null;
            LobbyId = null;
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
