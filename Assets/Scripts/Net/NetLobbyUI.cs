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

        private void Awake()
        {
            if (bootstrap == null) bootstrap = GetComponent<NetBootstrap>();
            if (bootstrap == null) bootstrap = FindAnyObjectByType<NetBootstrap>();
        }

        private void OnGUI()
        {
            if (!showPanel || bootstrap == null) return;

            GUILayout.BeginArea(new Rect(10f, 10f, 300f, 150f), GUI.skin.box);
            GUILayout.Label("IronWasteland - Online (thu nghiem)");
            GUILayout.Label(bootstrap.IsRunning ? "Dang chay phien mang" : "Chua ket noi");

            if (!bootstrap.IsRunning)
            {
                if (GUILayout.Button("Mo phong (Host)"))
                {
                    PrepareOfflineTank();
                    bootstrap.StartHost();
                }

                if (GUILayout.Button("Vao phong (Join)"))
                {
                    PrepareOfflineTank();
                    bootstrap.StartClient();
                }
            }
            else if (GUILayout.Button("Dong phong"))
            {
                bootstrap.Shutdown();
            }

            GUILayout.EndArea();
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
