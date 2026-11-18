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

            GUILayout.BeginArea(new Rect(10f, 10f, 380f, 540f), GUI.skin.box);
            GUILayout.Label("IronWasteland - Online (thu nghiem)");

            if (flow == null)
            {
                GUILayout.Label("Thieu MatchFlow trong scene.");
                GUILayout.EndArea();
                return;
            }

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

            GUILayout.Label("--- Choi mot minh ---");
            if (GUILayout.Button("Choi don (host 1 nguoi)"))
            {
                PrepareOfflineTank();
                bootstrap.SetLocalTankIndex(flow.LocalTankIndex);
                if (bootstrap.StartHost()) flow.StartSingle();
            }

            GUILayout.Label("--- Co-op qua Relay ---");
            if (GUILayout.Button("Mo phong co-op (Relay)"))
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

            GUILayout.Label("--- IP truc tiep (debug) ---");
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
                GUILayout.Label(flow.Phase == MatchPhase.Cleared ? "KET QUA: THANG" : "KET QUA: THUA");
                if (flow.TryGetPlayer(0, out _, out _, out _) && IsHost())
                {
                    if (GUILayout.Button("Choi lai (ve lobby)")) flow.Replay();
                }
                else if (GUILayout.Button("Xin host choi lai")) flow.RequestReplay();
            }

            if (GUILayout.Button("Thoat phong")) flow.Leave();

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
