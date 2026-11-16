using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// Lobby tối giản bằng IMGUI để thử mạng: bấm Host/Join, xem trạng thái, đóng phòng.
    /// Đây là UI tạm của giai đoạn 1; UI thật sẽ thay sau khi luồng mạng ổn định.
    /// </summary>
    public class NetLobbyUI : MonoBehaviour
    {
        [Tooltip("Bootstrap mạng; để trống sẽ tự tìm trong scene.")]
        [SerializeField] private NetBootstrap bootstrap;

        [Tooltip("Bật để hiện bảng điều khiển khi vào Play.")]
        [SerializeField] private bool showPanel = true;

        private GameController m_GameController;
        private string m_RelayJoinCodeInput = string.Empty;
        private string m_LobbyCodeInput = string.Empty;

        private void Awake()
        {
            if (bootstrap == null) bootstrap = GetComponent<NetBootstrap>();
            if (bootstrap == null) bootstrap = FindAnyObjectByType<NetBootstrap>();
        }

        private void OnGUI()
        {
            if (!showPanel || bootstrap == null) return;

            GUILayout.BeginArea(new Rect(10f, 10f, 360f, 300f), GUI.skin.box);
            GUILayout.Label("IronWasteland - Online (thu nghiem)");
            GUILayout.Label(bootstrap.IsRunning ? "Dang chay phien mang" : "Chua ket noi");

            if (!bootstrap.IsRunning)
            {
                GUILayout.Label("--- Relay (Unity Services) ---");
                if (GUILayout.Button("Mo phong qua Relay"))
                {
                    PrepareOfflineTank();
                    bootstrap.StartHostWithRelay();
                }

                m_RelayJoinCodeInput = GUILayout.TextField(m_RelayJoinCodeInput ?? string.Empty);
                if (GUILayout.Button("Vao phong bang ma Relay"))
                {
                    PrepareOfflineTank();
                    bootstrap.StartClientWithRelay(m_RelayJoinCodeInput);
                }

                m_LobbyCodeInput = GUILayout.TextField(m_LobbyCodeInput ?? string.Empty);
                if (GUILayout.Button("Vao phong bang ma phong (Lobby)"))
                {
                    PrepareOfflineTank();
                    JoinLobbyThenClient(m_LobbyCodeInput);
                }

                GUILayout.Label("--- IP truc tiep ---");
                if (GUILayout.Button("Mo phong IP (Host)"))
                {
                    PrepareOfflineTank();
                    bootstrap.StartHost();
                }

                if (GUILayout.Button("Vao phong IP (Join)"))
                {
                    PrepareOfflineTank();
                    bootstrap.StartClient();
                }
            }
            else if (GUILayout.Button("Dong phong"))
            {
                bootstrap.Shutdown();
            }

            if (!string.IsNullOrEmpty(NetRelayService.RelayJoinCode))
            {
                GUILayout.Label("Ma Relay: " + NetRelayService.RelayJoinCode);
            }

            if (!string.IsNullOrEmpty(NetRelayService.LobbyCode))
            {
                GUILayout.Label("Ma phong: " + NetRelayService.LobbyCode);
            }

            if (!string.IsNullOrEmpty(NetRelayService.LastError))
            {
                GUILayout.Label("Loi: " + NetRelayService.LastError);
            }

            GUILayout.EndArea();
        }

        /// <summary>Join lobby bằng mã phòng, lấy mã Relay rồi mới kết nối transport.</summary>
        private async void JoinLobbyThenClient(string lobbyCode)
        {
            string relayCode = await NetRelayService.JoinLobbyAndGetRelayCodeAsync(lobbyCode);
            if (string.IsNullOrEmpty(relayCode)) return;

            bootstrap.StartClientWithRelay(relayCode);
        }

        /// <summary>
        /// Huỷ tank offline do GameController tự tạo để phòng chơi chỉ còn tank do mạng spawn.
        /// GameController vẫn bật để nhận BindTank khi xe của mình được host dựng.
        /// </summary>
        private void PrepareOfflineTank()
        {
            if (m_GameController == null) m_GameController = FindAnyObjectByType<GameController>();
            if (m_GameController == null) return;

            m_GameController.DestroyLocalTank();
        }
    }
}
