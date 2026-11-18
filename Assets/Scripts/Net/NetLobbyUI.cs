using IronWasteland.Stages;
using IronWasteland.Tanks;
using Unity.Netcode;
using UnityEngine;

namespace IronWasteland.Net
{
    /// <summary>
    /// UI tạm (IMGUI) cho luồng chơi: chọn chế độ → lobby (chọn màn, đổi tank, sẵn sàng) →
    /// vào trận → thắng/thua → chơi lại/thoát. UI thật sẽ thay sau, phần này chỉ để chạy được luồng.
    /// </summary>
    public class NetLobbyUI : MonoBehaviour
    {
        [Tooltip("Bootstrap mạng; để trống sẽ tự tìm trong scene.")]
        [SerializeField] private NetBootstrap bootstrap;

        [Tooltip("Điều phối phiên chơi; để trống sẽ tự tìm trong scene.")]
        [SerializeField] private MatchFlow flow;

        [Tooltip("Bật để hiện bảng điều khiển khi vào Play.")]
        [SerializeField] private bool showPanel = true;

        private GameController m_GameController;
        private string m_RelayJoinCodeInput = string.Empty;
        private string m_LobbyCodeInput = string.Empty;
        private string m_ProfileInput = "client1";

        /// <summary>Cách đã dùng để mở phòng gần nhất — cần cho nút "Tạo phòng mới".</summary>
        private enum HostMode { None, Single, Relay, Ip }

        private HostMode m_LastHostMode = HostMode.None;

        private void Awake()
        {
            if (bootstrap == null) bootstrap = GetComponent<NetBootstrap>();
            if (bootstrap == null) bootstrap = FindAnyObjectByType<NetBootstrap>();

            if (flow == null) flow = GetComponent<MatchFlow>();
            if (flow == null) flow = FindAnyObjectByType<MatchFlow>();
        }

        private void OnGUI()
        {
            if (!showPanel || bootstrap == null) return;

            if (flow == null)
            {
                GUILayout.BeginArea(new Rect(10f, 10f, 380f, 540f), GUI.skin.box);
                GUILayout.Label("IronWasteland - Online (thu nghiem)");
                GUILayout.Label("Thieu MatchFlow trong scene.");
                GUILayout.EndArea();
                return;
            }

            // Phiên đã chết vẫn phải hiện màn kết thúc, kể cả khi NGO đã ngắt kết nối,
            // để người chơi chủ động bấm "Về menu" thay vì bị đẩy thẳng ra menu.
            if (IsEndState())
            {
                DrawEndOverlay();
                return;
            }

            GUILayout.BeginArea(new Rect(10f, 10f, 380f, 540f), GUI.skin.box);
            GUILayout.Label("IronWasteland - Online (thu nghiem)");

            if (!bootstrap.IsRunning)
            {
                DrawConnectionMenu();
            }
            else
            {
                DrawSession();
            }

            GUILayout.EndArea();
        }

        private void DrawConnectionMenu()
        {
            GUILayout.Label("Chua vao phong");

            GUILayout.Label("--- Test ky nang (bien the Optional) ---");
            if (GUILayout.Button("Optional 1: bien the " + (bootstrap.LocalOptionSkill1 + 1) + "/3 (bam de doi)"))
            {
                bootstrap.SetLocalOptionSkills((bootstrap.LocalOptionSkill1 + 1) % 3, bootstrap.LocalOptionSkill2);
            }

            if (GUILayout.Button("Optional 2: bien the " + (bootstrap.LocalOptionSkill2 + 1) + "/3 (bam de doi)"))
            {
                bootstrap.SetLocalOptionSkills(bootstrap.LocalOptionSkill1, (bootstrap.LocalOptionSkill2 + 1) % 3);
            }

            GUILayout.Label("--- Test nhieu nguoi tren 1 may ---");
            GUILayout.Label("Profile: " + NetRelayService.CurrentProfile);
            m_ProfileInput = GUILayout.TextField(m_ProfileInput ?? string.Empty);
            if (GUILayout.Button("Doi profile test (vd: client1)"))
            {
                SwitchProfileThenHint(m_ProfileInput);
            }

            GUILayout.Label("--- Choi mot minh ---");
            if (GUILayout.Button("Choi don (host 1 nguoi)"))
            {
                m_LastHostMode = HostMode.Single;
                PrepareOfflineTank();
                bootstrap.SetLocalTankIndex(flow.LocalTankIndex);
                if (bootstrap.StartHost()) flow.StartSingle();
            }

            GUILayout.Label("--- Co-op qua Relay ---");
            if (GUILayout.Button("Mo phong co-op (Relay)"))
            {
                m_LastHostMode = HostMode.Relay;
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

            GUILayout.Label("--- IP truc tiep (debug) ---");
            if (GUILayout.Button("Mo phong IP (Host)"))
            {
                m_LastHostMode = HostMode.Ip;
                PrepareOfflineTank();
                bootstrap.StartHost();
            }

            if (GUILayout.Button("Vao phong IP (Join)"))
            {
                PrepareOfflineTank();
                bootstrap.StartClient();
            }

            DrawServiceInfo();
        }

        private void DrawSession()
        {
            GUILayout.Label("Phien: " + PhaseLabel(flow.Phase) + " | " + (flow.Mode == MatchMode.Single ? "choi don" : "co-op"));

            if (!string.IsNullOrEmpty(NetRelayService.RelayJoinCode))
            {
                GUILayout.Label("Ma Relay: " + NetRelayService.RelayJoinCode);
            }

            if (!string.IsNullOrEmpty(NetRelayService.LobbyCode))
            {
                GUILayout.Label("Ma phong: " + NetRelayService.LobbyCode);
            }

            if (flow.Phase == MatchPhase.Lobby)
            {
                DrawLobby();
            }
            else if (flow.Phase == MatchPhase.Playing)
            {
                StageDefinition stage = flow.SelectedStage;
                if (stage != null) GUILayout.Label("Man: " + stage.StageCode + " - " + stage.StageName);
            }
            else
            {
                DrawResultPanel();
            }

            if (flow.Phase == MatchPhase.Lobby || flow.Phase == MatchPhase.Playing)
            {
                if (GUILayout.Button("Thoat phong")) flow.Leave();
            }

            if (!string.IsNullOrEmpty(NetRelayService.LastError))
            {
                GUILayout.Label("Loi: " + NetRelayService.LastError);
            }
        }

        private void DrawLobby()
        {
            StageDefinition stage = flow.SelectedStage;
            if (stage != null)
            {
                GUILayout.Label("Man: " + stage.StageCode + " - " + stage.StageName +
                                " (" + stage.KillTarget + " mang, " + stage.TotalEnemies + " quai)");
            }

            if (IsHost() && GUILayout.Button("Doi man")) flow.CycleStage();

            GUILayout.Label("--- Nguoi choi ---");
            for (int i = 0; i < flow.PlayerCount; i++)
            {
                if (!flow.TryGetPlayer(i, out ulong clientId, out byte tankIndex, out bool ready,
                        out byte option1, out byte option2)) continue;

                string tankName = TankName(tankIndex);
                string suffix = clientId == (bootstrap.Manager != null ? bootstrap.Manager.LocalClientId : 0UL)
                    ? " (ban)"
                    : string.Empty;
                GUILayout.Label($"{(clientId == NetBootstrap.ServerClientId ? "Host" : "Client")}{suffix}: {tankName} | opt {option1 + 1}/{option2 + 1} | {(ready ? "San sang" : "Chua san sang")}");
            }

            if (GUILayout.Button("Optional 1 cua ban: bien the " + (flow.LocalOption1 + 1) + "/3 (bam de doi)"))
            {
                flow.RequestOptionSkills((byte)((flow.LocalOption1 + 1) % 3), flow.LocalOption2);
            }

            if (GUILayout.Button("Optional 2 cua ban: bien the " + (flow.LocalOption2 + 1) + "/3 (bam de doi)"))
            {
                flow.RequestOptionSkills(flow.LocalOption1, (byte)((flow.LocalOption2 + 1) % 3));
            }

            if (GUILayout.Button("Doi tank: " + TankName(flow.LocalTankIndex)))
            {
                int count = bootstrap.Spawner != null ? bootstrap.Spawner.TankCount : 0;
                if (count > 0)
                {
                    byte next = (byte)((flow.LocalTankIndex + 1) % count);
                    bootstrap.SetLocalTankIndex(next);
                    flow.RequestTank(next);
                }
            }

            if (GUILayout.Button(flow.LocalReady ? "Huy san sang" : "San sang"))
            {
                flow.RequestReady(!flow.LocalReady);
            }

            if (IsHost())
            {
                bool allReady = AllReady();
                GUI.enabled = allReady;
                if (GUILayout.Button(allReady ? "Bat dau tran" : "Cho moi nguoi san sang"))
                {
                    flow.StartMatch();
                }

                GUI.enabled = true;
            }
        }

        /// <summary>Đang ở trạng thái kết thúc (phiên sập hoặc trận đã có kết quả) để hiện màn kết thúc.</summary>
        private bool IsEndState()
        {
            return bootstrap.SessionEnded
                || bootstrap.ConnectionLost
                || flow.Phase == MatchPhase.Cleared
                || flow.Phase == MatchPhase.Failed;
        }

        /// <summary>Màn kết thúc toàn màn hình: nền mờ + hộp giữa màn hình.</summary>
        private void DrawEndOverlay()
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            bool ended = bootstrap.SessionEnded || bootstrap.ConnectionLost;
            float width = 560f;
            float height = ended ? 240f : 330f;
            Rect box = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(box, GUI.skin.box);
            if (ended)
            {
                DrawEndedPanel();
            }
            else
            {
                DrawResultPanel();
            }

            GUILayout.EndArea();
        }

        /// <summary>Phiên đã chết: host hết client (SessionEnded) hoặc client mất host (ConnectionLost).</summary>
        private void DrawEndedPanel()
        {
            GUILayout.Label(bootstrap.SessionEnded ? "PHIEN DA KET THUC" : "MAT KET NOI TOI HOST");

            if (bootstrap.SessionEnded)
            {
                GUILayout.Label("Phong da dong vi khong con nguoi choi khac. Hay tao phong moi de choi tiep.");
            }
            else
            {
                GUILayout.Label("Host da thoat hoac mat ket noi. Hay ve menu de tim phong khac.");
            }

            if (bootstrap.SessionEnded && IsHost())
            {
                if (GUILayout.Button("Tao phong moi")) RehostSession();
            }

            if (GUILayout.Button("Ve menu")) flow.Leave();
        }

        /// <summary>Màn kết thúc trận: thắng/thua + thống kê ngắn + lựa chọn chơi lại/tạo phòng mới/thoát.</summary>
        private void DrawResultPanel()
        {
            GUILayout.Label(flow.Phase == MatchPhase.Cleared ? "KET QUA: THANG" : "KET QUA: THUA");

            StageManager stage = bootstrap.Stage;
            int kills = 0;
            int target = 0;
            float elapsed = 0f;

            if (IsHost() && stage != null)
            {
                kills = stage.Kills;
                target = stage.KillTarget;
                elapsed = stage.Elapsed;
            }
            else
            {
                // Client không mô phỏng ải: đọc số liệu đã sync từ NetStageEvents thay vì StageManager local.
                NetStageEvents events = NetStageEvents.Instance;
                if (events != null && events.HasStageState)
                {
                    kills = events.StageKills;
                    target = events.StageKillTarget;
                    elapsed = events.StageElapsed;
                }
                else if (stage != null)
                {
                    kills = stage.Kills;
                    target = stage.KillTarget;
                    elapsed = stage.Elapsed;
                }
            }

            GUILayout.Label($"Ha guc: {kills}/{target}   |   Thoi gian: {Mathf.FloorToInt(elapsed)}s");

            StageDefinition definition = flow.SelectedStage;
            if (definition != null)
            {
                GUILayout.Label("Man: " + definition.StageCode + " - " + definition.StageName);
            }

            if (IsHost())
            {
                if (GUILayout.Button("Choi lai (ve lobby)")) flow.Replay();
                if (GUILayout.Button("Tao phong moi")) RehostSession();
            }
            else
            {
                if (GUILayout.Button("Xin host choi lai")) flow.RequestReplay();
            }

            if (GUILayout.Button("Thoat phong")) flow.Leave();
        }

        /// <summary>Tạo lại phòng: giữ đúng loại phòng lần trước (Relay mặc định, IP nếu từng dùng IP).</summary>
        private void RehostSession()
        {
            bootstrap.Rehost(m_LastHostMode != HostMode.Ip);
        }

        private bool AllReady()
        {
            if (flow.PlayerCount == 0) return false;

            for (int i = 0; i < flow.PlayerCount; i++)
            {
                if (!flow.TryGetPlayer(i, out _, out _, out bool ready) || !ready) return false;
            }

            return true;
        }

        private bool IsHost()
        {
            return bootstrap.Manager != null && bootstrap.Manager.IsServer;
        }

        private string TankName(byte tankIndex)
        {
            TankDefinition definition = bootstrap.Spawner != null ? bootstrap.Spawner.GetDefinition(tankIndex) : null;
            return definition != null ? definition.TankName : "Tank " + tankIndex;
        }

        private static string PhaseLabel(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Lobby: return "lobby";
                case MatchPhase.Playing: return "dang choi";
                case MatchPhase.Cleared: return "thang";
                case MatchPhase.Failed: return "thua";
                default: return phase.ToString();
            }
        }

        private void DrawServiceInfo()
        {
            if (!string.IsNullOrEmpty(NetRelayService.LastError))
            {
                GUILayout.Label("Loi: " + NetRelayService.LastError);
            }
        }

        /// <summary>Join lobby bằng mã phòng, lấy mã Relay rồi mới kết nối transport.</summary>
        private async void JoinLobbyThenClient(string lobbyCode)
        {
            string relayCode = await NetRelayService.JoinLobbyAndGetRelayCodeAsync(lobbyCode);
            if (string.IsNullOrEmpty(relayCode)) return;

            bootstrap.StartClientWithRelay(relayCode);
        }

        /// <summary>Đổi profile đăng nhập ẩn danh để test 2 "người chơi" trên cùng một máy.</summary>
        private async void SwitchProfileThenHint(string profile)
        {
            bool ok = await NetRelayService.SwitchProfileAsync(profile);
            if (ok)
            {
                Debug.Log($"[NetLobbyUI] Đổi profile sang '{NetRelayService.CurrentProfile}' thành công.");
            }
            else
            {
                Debug.LogWarning($"[NetLobbyUI] Đổi profile thất bại: {NetRelayService.LastError}");
            }
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
