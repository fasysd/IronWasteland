using System.Collections.Generic;
using IronWasteland.Save;
using IronWasteland.Tanks;
using IronWasteland.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IronWasteland.Net
{
    /// <summary>
    /// Giao diện lobby co-op mới: danh sách phòng lọc theo đúng ải đang chọn, phòng chờ 4 người,
    /// sẵn sàng và bắt đầu trận. Chỉ điều khiển hiển thị; dữ liệu phiên vẫn lấy từ MatchFlow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoopLobbyController : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private NetBootstrap bootstrap;
        [SerializeField] private MatchFlow flow;
        [SerializeField] private CanvasGroup m_Root;
        [SerializeField] private GameObject m_BrowserView;
        [SerializeField] private GameObject m_RoomView;
        [SerializeField] private NetLobbyUI m_LegacyPanel;

        [Header("Nội dung chung")]
        [SerializeField] private TextMeshProUGUI m_StageCaption;
        [SerializeField] private TextMeshProUGUI m_Status;
        [SerializeField] private TextMeshProUGUI m_RoomCode;

        [Header("Danh sách phòng")]
        [SerializeField] private TextMeshProUGUI[] m_RoomNames = new TextMeshProUGUI[6];
        [SerializeField] private TextMeshProUGUI[] m_RoomDetails = new TextMeshProUGUI[6];
        [SerializeField] private GameObject m_EmptyHint;

        [Header("Phòng chờ")]
        [SerializeField] private TextMeshProUGUI[] m_SlotTitles = new TextMeshProUGUI[4];
        [SerializeField] private TextMeshProUGUI[] m_SlotDetails = new TextMeshProUGUI[4];
        [SerializeField] private TextMeshProUGUI m_ReadyLabel;
        [SerializeField] private TextMeshProUGUI m_StartLabel;
        [SerializeField] private TextMeshProUGUI m_RoomHint;

        [Header("Nhập mã phòng")]
        [SerializeField] private TextMeshProUGUI m_JoinCodeText;

        [Header("Scene")]
        [SerializeField] private string m_BackSceneName = "StageSelect";

        private const float DesignHeight = 1080f;

        private readonly List<NetRelayService.LobbyRoom> m_Rooms = new List<NetRelayService.LobbyRoom>();
        private GameController m_GameController;
        private bool m_Busy;
        private float m_LastQueryTime;
        private bool m_Shown;
        private bool m_StartSingleRequested;
        private string m_JoinCode = string.Empty;
        private bool m_JoinCodeFocused;
        private bool m_GarageOpen;
        private bool m_GarageLoading;
        private bool m_PickerScenePrepared;

        private void Awake()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<NetBootstrap>();
            if (flow == null) flow = FindAnyObjectByType<MatchFlow>();
            if (m_LegacyPanel == null) m_LegacyPanel = FindAnyObjectByType<NetLobbyUI>();
            if (m_GameController == null) m_GameController = FindAnyObjectByType<GameController>();

            if (m_Root == null) m_Root = GetComponent<CanvasGroup>();
            if (m_Root != null) m_Root.alpha = 0f;

            if (flow != null) flow.Changed += OnFlowChanged;
            NetRelayService.LobbyChanged += OnLobbyChanged;
            NetBootstrap.HostDisconnected += OnHostDisconnected;

            SetStageCaption();

            if (CoopEntry.OpenLobby)
            {
                CoopEntry.OpenLobby = false;
                ShowBrowser();
            }
            else if (CoopEntry.Mode == MatchMode.Single)
            {
                // Không StartHost() ngay trong Awake: NetBootstrap.Awake có thể chưa chạy,
                // khiến NetworkManager thiếu ConnectionApprovalCallback và host treo ở Lobby.
                m_StartSingleRequested = true;
            }
        }

        private void Start()
        {
            if (m_StartSingleRequested)
            {
                m_StartSingleRequested = false;
                StartSingle();
            }
        }

        private void OnDestroy()
        {
            if (flow != null) flow.Changed -= OnFlowChanged;
            NetRelayService.LobbyChanged -= OnLobbyChanged;
            NetBootstrap.HostDisconnected -= OnHostDisconnected;
        }

        /// <summary>Áp mã ải của phòng vừa tham gia và tra tên ải tương ứng để caption luôn đúng.</summary>
        private void ApplyStageCode(string stageCode)
        {
            if (string.IsNullOrWhiteSpace(stageCode)) return;

            CoopEntry.StageCode = stageCode.Trim();

            string stageName = flow != null ? flow.GetStageNameByCode(CoopEntry.StageCode) : string.Empty;
            if (!string.IsNullOrEmpty(stageName)) CoopEntry.StageName = stageName;
        }

        private void SetStageCaption()
        {
            if (m_StageCaption != null)
            {
                m_StageCaption.text = "ẢI " + CoopEntry.StageCode + "  /  " + CoopEntry.StageName;
            }
        }

        private void ShowBrowser()
        {
            m_Shown = true;
            m_JoinCodeFocused = false;
            RefreshJoinCode();
            if (m_Root != null) m_Root.alpha = 1f;
            SetView(true);
            if (m_LegacyPanel != null) m_LegacyPanel.PanelVisible = false;
            SetStageCaption();
            RefreshRooms();
        }

        private void SetView(bool browser)
        {
            if (m_BrowserView != null) m_BrowserView.SetActive(browser);
            if (m_RoomView != null) m_RoomView.SetActive(!browser);
        }

        private void ShowRoom()
        {
            m_Shown = true;
            m_JoinCodeFocused = false;
            if (m_Root != null) m_Root.alpha = 1f;
            SetView(false);
            if (m_LegacyPanel != null) m_LegacyPanel.PanelVisible = false;
            SetStatus("PHÒNG ĐÃ MỞ  ·  CHỜ ĐỒNG ĐỘI");
            RefreshRoom();
        }

        private void HideAll()
        {
            m_Shown = false;
            m_JoinCodeFocused = false;
            m_GarageOpen = false;
            m_GarageLoading = false;
            GarageFlow.OpenedFromLobby = false;
            if (m_Root != null) m_Root.alpha = 0f;
            if (m_LegacyPanel != null) m_LegacyPanel.PanelVisible = false;
        }

        private void StartSingle()
        {
            if (bootstrap == null || flow == null) return;

            PrepareLocalLoadout();
            PrepareOfflineTank();
            if (!bootstrap.StartHost())
            {
                HideAll();
                return;
            }

            StartCoroutine(StartSingleWhenHostReady());
        }

        /// <summary>
        /// StartHost() của Netcode có thể chưa bật IsServer ngay trong frame gọi,
        /// nên chờ host sẵn sàng rồi mới chốt màn và vào trận; tránh StartSingle() return sớm.
        /// </summary>
        private System.Collections.IEnumerator StartSingleWhenHostReady()
        {
            float elapsed = 0f;
            while (elapsed < 5f && (bootstrap.Manager == null || !bootstrap.Manager.IsServer))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (bootstrap.Manager == null || !bootstrap.Manager.IsServer)
            {
                HideAll();
                yield break;
            }

            flow.SelectStageByCode(CoopEntry.StageCode);
            flow.StartSingle();
            HideAll();
        }

        private void PrepareLocalLoadout()
        {
            if (bootstrap == null || SaveSystem.Instance == null) return;

            int tank = Mathf.Clamp(SaveSystem.Instance.LastSelectedTankIndex, 0, SkillCatalog.TankCount - 1);
            int level = Mathf.Max(TankDefinition.MinLevel, SaveSystem.Instance.GetTankLevel(tank));
            int equip = SaveSystem.Instance.GetEquippedAccessory(tank);
            int opt1 = SaveSystem.Instance.GetOptionSkill(tank, 0);
            int opt2 = SaveSystem.Instance.GetOptionSkill(tank, 1);

            bootstrap.SetLocalLoadout(new NetLoadout
            {
                TankIndex = (byte)tank,
                Level = level,
                EquipmentId = equip,
                OptionSkill1 = opt1,
                OptionSkill2 = opt2
            });
        }

        private void PrepareOfflineTank()
        {
            if (m_GameController == null) m_GameController = FindAnyObjectByType<GameController>();
            if (m_GameController != null) m_GameController.DestroyLocalTank();
        }

        private void Update()
        {
            if (!m_Shown) return;

            if (m_GarageOpen)
            {
                TickGarage();
            }
        }

        private void OnGUI()
        {
            if (!m_Shown || m_GarageOpen) return;

            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown) return;

            if (m_JoinCodeFocused && HandleJoinCodeKey(current))
            {
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Escape) GoBack();
        }

        /// <summary>Nút QUAY LẠI ở header.</summary>
        public void PressBack()
        {
            if (!m_Shown) return;
            GoBack();
        }

        /// <summary>Nút TẠO PHÒNG ở màn duyệt phòng.</summary>
        public void PressCreate()
        {
            if (!m_Shown || m_Busy) return;
            m_JoinCodeFocused = false;
            RefreshJoinCode();
            CreateRoom();
        }

        /// <summary>Nút TẢI LẠI ở màn duyệt phòng.</summary>
        public void PressRefresh()
        {
            if (!m_Shown) return;
            m_JoinCodeFocused = false;
            RefreshJoinCode();
            RefreshRooms();
        }

        /// <summary>Ô nhập mã phòng: bấm để bật chế độ gõ phím.</summary>
        public void PressJoinCodeField()
        {
            if (!m_Shown || m_BrowserView == null || !m_BrowserView.activeSelf) return;
            m_JoinCodeFocused = true;
            RefreshJoinCode();
        }

        /// <summary>Nút VÀO PHÒNG bằng mã.</summary>
        public void PressJoinCode()
        {
            if (!m_Shown || m_Busy) return;
            TryJoinByCode();
        }

        /// <summary>Bấm một dòng phòng trong danh sách để vào phòng đó.</summary>
        public void PressRoom(int index)
        {
            if (!m_Shown || m_Busy) return;
            if (index < 0 || index >= m_Rooms.Count) return;
            JoinRoom(m_Rooms[index]);
        }

        /// <summary>Nút NHÀ XE trong phòng chờ.</summary>
        public void PressGarage()
        {
            if (!m_Shown) return;
            OpenGarage();
        }

        /// <summary>Nút SẴN SÀNG trong phòng chờ.</summary>
        public void PressReady()
        {
            if (!m_Shown) return;
            if (flow != null) flow.RequestReady(!flow.LocalReady);
        }

        /// <summary>Nút BẮT ĐẦU CHIẾN ĐẤU trong phòng chờ.</summary>
        public void PressStart()
        {
            if (!m_Shown) return;
            TryStartMatch();
        }

        /// <summary>Nút RỜI PHÒNG trong phòng chờ.</summary>
        public void PressLeave()
        {
            if (!m_Shown) return;
            LeaveRoom();
        }

        private void TryJoinByCode()
        {
            if (m_Busy) return;

            string code = m_JoinCode.Trim().ToUpperInvariant();
            if (code.Length < 4)
            {
                SetStatus("NHẬP MÃ PHÒNG (ÍT NHẤT 4 KÝ TỰ)");
                return;
            }

            m_Busy = true;
            m_JoinCodeFocused = false;
            RefreshJoinCode();
            SetStatus("ĐANG VÀO PHÒNG…");
            JoinByCodeAsync(code);
        }

        private async void JoinByCodeAsync(string code)
        {
            string relayCode = await NetRelayService.JoinLobbyAndGetRelayCodeAsync(code);
            if (string.IsNullOrEmpty(relayCode))
            {
                m_Busy = false;
                SetStatus("KHÔNG VÀO ĐƯỢC PHÒNG: " + (NetRelayService.LastError ?? "LỖI KHÔNG XÁC ĐỊNH"));
                return;
            }

            ApplyStageCode(NetRelayService.LobbyStageCode);

            SetStageCaption();
            PrepareLocalLoadout();
            PrepareOfflineTank();

            if (bootstrap == null)
            {
                m_Busy = false;
                SetStatus("THIẾU NETWORK MANAGER.");
                return;
            }

            bootstrap.StartClientWithRelay(relayCode);
            StartCoroutine(WaitForSession(false));
        }

        private bool HandleJoinCodeKey(Event current)
        {
            if (current.keyCode == KeyCode.Backspace)
            {
                if (m_JoinCode.Length > 0)
                {
                    m_JoinCode = m_JoinCode.Substring(0, m_JoinCode.Length - 1);
                }

                RefreshJoinCode();
                return true;
            }

            if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
            {
                TryJoinByCode();
                return true;
            }

            if (current.keyCode == KeyCode.Escape)
            {
                m_JoinCodeFocused = false;
                RefreshJoinCode();
                return true;
            }

            if (current.control && current.keyCode == KeyCode.V)
            {
                AppendJoinCode(GUIUtility.systemCopyBuffer);
                return true;
            }

            if (current.character != '\0' && !char.IsControl(current.character))
            {
                AppendJoinCode(current.character.ToString());
                return true;
            }

            return false;
        }

        private void AppendJoinCode(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            for (int i = 0; i < value.Length && m_JoinCode.Length < 8; i++)
            {
                char c = char.ToUpperInvariant(value[i]);
                if (char.IsLetterOrDigit(c))
                {
                    m_JoinCode += c;
                }
            }

            RefreshJoinCode();
        }

        private void RefreshJoinCode()
        {
            if (m_JoinCodeText == null) return;

            if (string.IsNullOrEmpty(m_JoinCode))
            {
                m_JoinCodeText.text = m_JoinCodeFocused ? "|" : "NHẬP MÃ PHÒNG";
                m_JoinCodeText.color = new Color32(0x6F, 0x7D, 0x8C, 0xFF);
                return;
            }

            m_JoinCodeText.text = m_JoinCodeFocused ? m_JoinCode + "|" : m_JoinCode;
            m_JoinCodeText.color = new Color32(0xE6, 0xED, 0xF5, 0xFF);
        }

        private void OpenGarage()
        {
            if (m_GarageOpen || m_Busy) return;
            if (!Application.CanStreamedLevelBeLoaded("Garage"))
            {
                SetStatus("SCENE GARAGE CHƯA CÓ TRONG BUILD SETTINGS.");
                return;
            }

            m_GarageOpen = true;
            m_GarageLoading = true;
            m_PickerScenePrepared = false;
            GarageFlow.OpenedFromLobby = true;
            if (m_Root != null)
            {
                m_Root.alpha = 0f;
                m_Root.blocksRaycasts = false;
                m_Root.interactable = false;
            }

            SceneManager.LoadSceneAsync("Garage", LoadSceneMode.Additive);
        }

        private void TickGarage()
        {
            Scene garage = SceneManager.GetSceneByName("Garage");
            if (m_GarageLoading)
            {
                if (!garage.isLoaded) return;
                PrepareAdditiveScene(garage, 60);
                m_GarageLoading = false;
                return;
            }

            if (garage.isLoaded)
            {
                Scene picker = SceneManager.GetSceneByName("SkillPicker");
                if (picker.isLoaded)
                {
                    if (!m_PickerScenePrepared)
                    {
                        PrepareAdditiveScene(picker, 70, true);
                        m_PickerScenePrepared = true;
                    }
                }
                else
                {
                    m_PickerScenePrepared = false;
                }

                return;
            }

            m_GarageOpen = false;
            GarageFlow.OpenedFromLobby = false;
            if (m_Root != null)
            {
                m_Root.alpha = 1f;
                m_Root.blocksRaycasts = false;
                m_Root.interactable = false;
            }

            PrepareLocalLoadout();
            if (flow != null && SaveSystem.Instance != null)
            {
                int tank = Mathf.Clamp(SaveSystem.Instance.LastSelectedTankIndex, 0, SkillCatalog.TankCount - 1);
                int level = Mathf.Max(TankDefinition.MinLevel, SaveSystem.Instance.GetTankLevel(tank));
                int equip = SaveSystem.Instance.GetEquippedAccessory(tank);
                int opt1 = SaveSystem.Instance.GetOptionSkill(tank, 0);
                int opt2 = SaveSystem.Instance.GetOptionSkill(tank, 1);

                NetLoadout loadout = new NetLoadout
                {
                    TankIndex = (byte)tank,
                    Level = level,
                    EquipmentId = equip,
                    OptionSkill1 = opt1,
                    OptionSkill2 = opt2
                };
                flow.RequestLoadout(loadout);
            }

            RefreshRoom();
        }

        private static void PrepareAdditiveScene(Scene scene, int sortingOrder, bool keepCamera = false)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            bool cameraKept = false;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
                for (int c = 0; c < cameras.Length; c++)
                {
                    if (keepCamera && !cameraKept)
                    {
                        cameraKept = true;
                        cameras[c].enabled = true;
                        cameras[c].depth = 200f;
                    }
                    else
                    {
                        cameras[c].enabled = false;
                    }
                }

                AudioListener[] listeners = root.GetComponentsInChildren<AudioListener>(true);
                for (int l = 0; l < listeners.Length; l++)
                {
                    listeners[l].enabled = false;
                }

                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                for (int c = 0; c < canvases.Length; c++)
                {
                    canvases[c].sortingOrder = sortingOrder;
                }
            }
        }

        private void CreateRoom()
        {
            if (m_Busy || bootstrap == null) return;

            m_Busy = true;
            SetStatus("ĐANG TẠO PHÒNG…");
            PrepareLocalLoadout();
            PrepareOfflineTank();
            bootstrap.StartHostWithRelay(null, CoopEntry.StageCode);
            StartCoroutine(WaitForSession(true));
        }

        private void JoinRoom(NetRelayService.LobbyRoom room)
        {
            if (m_Busy || room == null) return;
            JoinRoomAsync(room);
        }

        private async void JoinRoomAsync(NetRelayService.LobbyRoom room)
        {
            m_Busy = true;
            SetStatus("ĐANG VÀO PHÒNG…");

            string relayCode = await NetRelayService.JoinLobbyByIdAndGetRelayCodeAsync(room.Id);
            if (string.IsNullOrEmpty(relayCode))
            {
                m_Busy = false;
                SetStatus("KHÔNG VÀO ĐƯỢC PHÒNG: " + (NetRelayService.LastError ?? "LỖI KHÔNG XÁC ĐỊNH"));
                return;
            }

            ApplyStageCode(room.StageCode);
            SetStageCaption();
            PrepareLocalLoadout();
            PrepareOfflineTank();
            bootstrap.StartClientWithRelay(relayCode);
            StartCoroutine(WaitForSession(false));
        }

        private System.Collections.IEnumerator WaitForSession(bool host)
        {
            float elapsed = 0f;
            while (elapsed < 12f)
            {
                if (bootstrap != null && bootstrap.IsRunning && flow != null && flow.PlayerCount > 0)
                {
                    if (host && flow.SelectStageByCode(CoopEntry.StageCode))
                    {
                        m_Busy = false;
                        ShowRoom();
                        yield break;
                    }

                    if (!host)
                    {
                        m_Busy = false;
                        ShowRoom();
                        yield break;
                    }
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            m_Busy = false;
            SetStatus("KHÔNG KẾT NỐI ĐƯỢC PHÒNG. KIỂM TRA MẠNG RỒI THỬ LẠI.");
        }

        private void RefreshRooms()
        {
            if (m_Busy) return;
            if (Time.unscaledTime - m_LastQueryTime < 1f) return;
            m_LastQueryTime = Time.unscaledTime;
            RefreshRoomsAsync();
        }

        private async void RefreshRoomsAsync()
        {
            SetStatus("ĐANG TẢI DANH SÁCH PHÒNG…");
            List<NetRelayService.LobbyRoom> rooms = await NetRelayService.QueryEligibleRoomsAsync(CoopEntry.StageCode);
            if (rooms == null)
            {
                SetStatus("KHÔNG TẢI ĐƯỢC PHÒNG: " + (NetRelayService.LastError ?? "LỖI KHÔNG XÁC ĐỊNH"));
                return;
            }

            m_Rooms.Clear();
            m_Rooms.AddRange(rooms);
            for (int i = 0; i < 6; i++)
            {
                bool has = i < m_Rooms.Count;
                if (m_RoomNames != null && i < m_RoomNames.Length && m_RoomNames[i] != null)
                {
                    Transform row = m_RoomNames[i].transform.parent;
                    if (row != null && row.name.StartsWith("RoomRowGroup"))
                    {
                        row.gameObject.SetActive(has);
                    }

                    m_RoomNames[i].text = has ? m_Rooms[i].Name : "—";
                    m_RoomNames[i].color = has ? new Color32(0xE6, 0xED, 0xF5, 0xFF) : new Color32(0x3B, 0x46, 0x54, 0xFF);
                }

                if (m_RoomDetails != null && i < m_RoomDetails.Length && m_RoomDetails[i] != null)
                {
                    m_RoomDetails[i].text = has
                        ? m_Rooms[i].Players + "/" + m_Rooms[i].MaxPlayers + " NGƯỜI  ·  " + m_Rooms[i].StageCode
                        : string.Empty;
                }
            }

            if (m_EmptyHint != null)
            {
                m_EmptyHint.SetActive(m_Rooms.Count == 0);
            }

            SetStatus(m_Rooms.Count == 0 ? "CHƯA CÓ PHÒNG NÀO CHO ẢI NÀY" : "TÌM THẤY " + m_Rooms.Count + " PHÒNG");
        }

        private void RefreshRoom()
        {
            if (flow == null || bootstrap == null) return;

            if (m_RoomCode != null)
            {
                m_RoomCode.text = "MÃ PHÒNG // " + (string.IsNullOrEmpty(NetRelayService.LobbyCode) ? "—" : NetRelayService.LobbyCode);
            }

            bool allReady = flow.PlayerCount > 0;
            for (int i = 0; i < 4; i++)
            {
                bool has = flow.TryGetPlayer(i, out ulong clientId, out byte tankIndex, out bool ready, out _, out _);
                if (has && !ready) allReady = false;

                if (m_SlotTitles != null && i < m_SlotTitles.Length && m_SlotTitles[i] != null)
                {
                    m_SlotTitles[i].text = has ? "P" + (i + 1) + " / " + (clientId == NetBootstrap.ServerClientId ? "HOST" : "KHÁCH") : "P" + (i + 1) + " / TRỐNG";
                    m_SlotTitles[i].color = has ? new Color32(0xE6, 0xED, 0xF5, 0xFF) : new Color32(0x4E, 0x5A, 0x69, 0xFF);
                }

                if (m_SlotDetails != null && i < m_SlotDetails.Length && m_SlotDetails[i] != null)
                {
                    m_SlotDetails[i].text = has ? TankName(tankIndex) + (ready ? "  ·  SẴN SÀNG" : "  ·  CHƯA SẴN SÀNG") : "CHƯA THAM GIA";
                }
            }

            if (m_ReadyLabel != null) m_ReadyLabel.text = flow.LocalReady ? "HỦY SẴN SÀNG" : "SẴN SÀNG";
            if (m_StartLabel != null) m_StartLabel.text = allReady ? "BẮT ĐẦU CHIẾN ĐẤU" : "CHỜ MỌI NGƯỜI SẴN SÀNG";
            if (m_RoomHint != null)
            {
                m_RoomHint.text = bootstrap.Manager != null && bootstrap.Manager.IsServer
                    ? (allReady ? "ĐỦ NGƯỜI — BẮT ĐẦU ĐƯỢC" : "CHỜ ĐỒNG ĐỘI SẴN SÀNG")
                    : "CHỜ HOST BẮT ĐẦU TRẬN";
            }
        }

        private void TryStartMatch()
        {
            if (flow == null || bootstrap == null) return;
            if (bootstrap.Manager == null || !bootstrap.Manager.IsServer) return;
            if (flow.PlayerCount == 0) return;

            for (int i = 0; i < flow.PlayerCount; i++)
            {
                if (!flow.TryGetPlayer(i, out _, out _, out bool ready) || !ready) return;
            }

            flow.StartMatch();
            HideAll();
        }

        private void LeaveRoom()
        {
            if (flow != null) flow.Leave();
            SetStatus(string.Empty);
            ShowBrowser();
        }

        private void OnHostDisconnected()
        {
            if (m_Shown && m_RoomView != null && m_RoomView.activeSelf)
            {
                ShowBrowser();
                SetStatus("CHỦ PHÒNG ĐÃ THOÁT PHÒNG");
                RefreshRooms();
            }
        }

        private void GoBack()
        {
            if (flow != null && bootstrap != null && bootstrap.IsRunning) flow.Leave();
            if (Application.CanStreamedLevelBeLoaded(m_BackSceneName))
            {
                SceneManager.LoadScene(m_BackSceneName);
            }
        }

        private void OnLobbyChanged()
        {
            if (!m_Shown || m_GarageOpen) return;
            if (m_RoomView != null && m_RoomView.activeSelf)
            {
                RefreshRoom();
            }
        }

        private void OnFlowChanged()
        {
            if (flow == null) return;

            if (flow.Phase == MatchPhase.Playing || flow.Phase == MatchPhase.Cleared || flow.Phase == MatchPhase.Failed)
            {
                if (m_Shown) HideAll();
                return;
            }

            if (flow.Phase == MatchPhase.Lobby && m_Shown && m_RoomView != null && m_RoomView.activeSelf)
            {
                RefreshRoom();
            }
        }

        private static string TankName(byte tankIndex)
        {
            NetBootstrap bootstrap = FindAnyObjectByType<NetBootstrap>();
            TankDefinition definition = bootstrap != null && bootstrap.Spawner != null
                ? bootstrap.Spawner.GetDefinition(tankIndex)
                : null;
            return definition != null ? definition.TankName : "XE " + (tankIndex + 1);
        }

        private void SetStatus(string value)
        {
            if (m_Status != null) m_Status.text = value;
        }

    }
}
