using System.Collections.Generic;
using System.Text;
using IronWasteland.Net.Models;
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
    /// Giao diện phòng LAN: tự dò phòng bằng UDP broadcast, vào bằng IP host thủ công,
    /// phòng chờ 4 người, sẵn sàng và bắt đầu trận. Dữ liệu phiên vẫn lấy từ MatchFlow.
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
        [Tooltip("Dịch vụ tự dò phòng LAN; để trống sẽ tự tìm trong scene.")]
        [SerializeField] private LanDiscoveryService discovery;

        [Header("Nội dung chung")]
        [Tooltip("Dòng chữ ải đang chọn ở đầu màn.")]
        [SerializeField] private TextMeshProUGUI m_StageCaption;
        [Tooltip("Dòng trạng thái dưới danh sách phòng.")]
        [SerializeField] private TextMeshProUGUI m_Status;
        [Tooltip("Địa chỉ IP host hiện trong phòng chờ.")]
        [SerializeField] private TextMeshProUGUI m_RoomAddress;
        [Tooltip("Ghi chú hướng dẫn chia sẻ địa chỉ host.")]
        [SerializeField] private TextMeshProUGUI m_RoomNote;

        [Header("Danh sách phòng")]
        [Tooltip("Tên phòng trên 6 dòng danh sách.")]
        [SerializeField] private TextMeshProUGUI[] m_RoomNames = new TextMeshProUGUI[6];
        [Tooltip("Thông tin người/ải trên 6 dòng danh sách.")]
        [SerializeField] private TextMeshProUGUI[] m_RoomDetails = new TextMeshProUGUI[6];
        [Tooltip("Object gốc của 6 dòng phòng, bật/tắt theo số phòng tìm thấy.")]
        [SerializeField] private GameObject[] m_RoomRows = new GameObject[6];
        [Tooltip("Khối gợi ý khi chưa tìm thấy phòng LAN nào.")]
        [SerializeField] private GameObject m_EmptyHint;

        [Header("Phòng chờ")]
        [SerializeField] private TextMeshProUGUI[] m_SlotTitles = new TextMeshProUGUI[4];
        [SerializeField] private TextMeshProUGUI[] m_SlotDetails = new TextMeshProUGUI[4];
        [SerializeField] private TextMeshProUGUI m_ReadyLabel;
        [SerializeField] private TextMeshProUGUI m_StartLabel;
        [SerializeField] private TextMeshProUGUI m_RoomHint;

        [Header("Nhập IP host")]
        [Tooltip("Chữ hiển thị trong ô nhập IP host.")]
        [SerializeField] private TextMeshProUGUI m_AddressText;

        [Header("Scene")]
        [SerializeField] private string m_BackSceneName = "StageSelect";

        private const float DesignHeight = 1080f;

        private readonly List<LanRoomInfo> m_Rooms = new List<LanRoomInfo>();
        private GameController m_GameController;
        private bool m_Busy;
        private float m_LastQueryTime;
        private bool m_Shown;
        private bool m_StartSingleRequested;
        private string m_AddressInput = string.Empty;
        private string m_JoinedHostAddress = string.Empty;
        private bool m_AddressFocused;
        private bool m_GarageOpen;
        private bool m_GarageLoading;
        private bool m_PickerScenePrepared;

        private void Awake()
        {
            if (bootstrap == null) bootstrap = FindAnyObjectByType<NetBootstrap>();
            if (flow == null) flow = FindAnyObjectByType<MatchFlow>();
            if (discovery == null) discovery = FindAnyObjectByType<LanDiscoveryService>();
            if (m_GameController == null) m_GameController = FindAnyObjectByType<GameController>();

            if (m_Root == null) m_Root = GetComponent<CanvasGroup>();
            SetRootVisible(false);

            if (flow != null) flow.Changed += OnFlowChanged;
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
                return;
            }

            // Chơi lại: Netcode load lại scene này khi phiên vẫn chạy. Bản NetBootstrap trong
            // scene mới bị huỷ vì đã có Instance DontDestroyOnLoad, nên phải trỏ lại instance thật
            // rồi mới mở phòng chờ; nếu không CanvasGroup vẫn ẩn và cả màn chỉ còn nền xanh.
            ResolvePersistentSession();

            if (!m_Shown && bootstrap != null && bootstrap.IsRunning)
            {
                ShowRoom();
            }
            else if (!m_Shown)
            {
                ShowBrowser();
            }
        }

        /// <summary>Trỏ lại NetBootstrap/MatchFlow đang sống sau khi scene CoopLobby được load lại.</summary>
        private void ResolvePersistentSession()
        {
            NetBootstrap persistent = NetBootstrap.Instance;
            if (persistent == null) return;

            bootstrap = persistent;
            if (persistent.Manager != null && !persistent.Manager.IsServer && string.IsNullOrEmpty(m_JoinedHostAddress))
            {
                m_JoinedHostAddress = persistent.JoinAddress;
            }

            MatchFlow persistentFlow = persistent.GetComponent<MatchFlow>();
            if (persistentFlow == null || persistentFlow == flow) return;

            if (flow != null) flow.Changed -= OnFlowChanged;
            flow = persistentFlow;
            flow.Changed += OnFlowChanged;
        }

        private void OnDestroy()
        {
            if (flow != null) flow.Changed -= OnFlowChanged;
            NetBootstrap.HostDisconnected -= OnHostDisconnected;
        }

        /// <summary>Dịch vụ quảng bá phòng LAN; tự lấy từ NetBootstrap nếu builder chưa gán.</summary>
        private LanDiscoveryService Discovery
        {
            get
            {
                if (discovery == null && bootstrap != null) discovery = bootstrap.GetComponent<LanDiscoveryService>();
                if (discovery == null) discovery = FindAnyObjectByType<LanDiscoveryService>();
                return discovery;
            }
        }

        /// <summary>Áp mã ải của phòng LAN vừa tham gia và tra tên ải tương ứng để caption luôn đúng.</summary>
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
            m_AddressFocused = false;
            RefreshAddressInput();
            SetRootVisible(true);
            SetView(true);
            SetStageCaption();
            RefreshRooms();
        }

        private void SetView(bool browser)
        {
            if (m_BrowserView != null) m_BrowserView.SetActive(browser);
            if (m_RoomView != null) m_RoomView.SetActive(!browser);
        }

        /// <summary>Bật/tắt cả khung lobby; phải bật raycast thì Button mới nhận được chuột.</summary>
        private void SetRootVisible(bool visible)
        {
            if (m_Root == null) return;
            m_Root.alpha = visible ? 1f : 0f;
            m_Root.blocksRaycasts = visible;
            m_Root.interactable = visible;
        }

        private void ShowRoom()
        {
            m_Shown = true;
            m_AddressFocused = false;
            SetRootVisible(true);
            SetView(false);
            SetStatus("PHÒNG ĐÃ MỞ  ·  CHỜ ĐỒNG ĐỘI");
            RefreshRoom();
        }

        private void HideAll()
        {
            m_Shown = false;
            m_AddressFocused = false;
            m_GarageOpen = false;
            m_GarageLoading = false;
            GarageFlow.OpenedFromLobby = false;
            SetRootVisible(false);
        }

        private void StartSingle()
        {
            if (bootstrap == null || flow == null) return;

            PrepareLocalLoadout();
            PrepareOfflineTank();
            bootstrap.SetLanStageCode(CoopEntry.StageCode);
            if (!bootstrap.StartHost(false))
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
                return;
            }

            if (m_BrowserView != null && m_BrowserView.activeSelf)
            {
                RefreshRooms();
            }
        }

        private void OnGUI()
        {
            if (!m_Shown || m_GarageOpen) return;

            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown) return;

            if (m_AddressFocused && HandleAddressKey(current))
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
            m_AddressFocused = false;
            RefreshAddressInput();
            CreateRoom();
        }

        /// <summary>Nút TẢI LẠI ở màn duyệt phòng.</summary>
        public void PressRefresh()
        {
            if (!m_Shown) return;
            m_AddressFocused = false;
            RefreshAddressInput();
            RefreshRooms();
        }

        /// <summary>Ô nhập IP host: bấm để bật chế độ gõ phím.</summary>
        public void PressAddressField()
        {
            if (!m_Shown || m_BrowserView == null || !m_BrowserView.activeSelf) return;
            m_AddressFocused = true;
            RefreshAddressInput();
        }

        /// <summary>Nút VÀO PHÒNG bằng IP host.</summary>
        public void PressJoinAddress()
        {
            if (!m_Shown || m_Busy) return;
            TryJoinByAddress();
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

        /// <summary>Nút SAO CHÉP ĐỊA CHỈ trong phòng chờ.</summary>
        public void PressCopyAddress()
        {
            if (!m_Shown) return;

            bool isHost = bootstrap != null && bootstrap.Manager != null && bootstrap.Manager.IsServer;
            string address = isHost ? BuildHostAddressList() : m_JoinedHostAddress;
            if (string.IsNullOrEmpty(address)) address = BuildHostAddressList();
            GUIUtility.systemCopyBuffer = address;
            SetStatus(isHost ? "ĐÃ SAO CHÉP TẤT CẢ ĐỊA CHỈ HOST" : "ĐÃ SAO CHÉP ĐỊA CHỈ: " + address);
        }

        private void TryJoinByAddress()
        {
            if (m_Busy) return;

            ushort defaultPort = bootstrap != null ? bootstrap.HostPort : NetBootstrap.DefaultHostPort;
            if (!LanAddress.TryParse(m_AddressInput, defaultPort, out string host, out ushort port))
            {
                SetStatus("NHẬP IP HOST, VÍ DỤ 192.168.1.10:7777");
                return;
            }

            m_Busy = true;
            m_AddressFocused = false;
            RefreshAddressInput();
            SetStatus("ĐANG VÀO PHÒNG…");
            JoinByAddress(host, port);
        }

        private void JoinByAddress(string host, ushort port)
        {
            if (bootstrap == null)
            {
                m_Busy = false;
                SetStatus("THIẾU NETWORK MANAGER.");
                return;
            }

            SetStageCaption();
            PrepareLocalLoadout();
            PrepareOfflineTank();
            m_JoinedHostAddress = host + ":" + port;
            bootstrap.SetJoinTarget(host, port);

            if (!bootstrap.StartClient())
            {
                m_Busy = false;
                SetStatus("KHÔNG KẾT NỐI ĐƯỢC. KIỂM TRA IP HOST VÀ MẠNG LAN.");
                return;
            }

            StartCoroutine(WaitForSession(false));
        }

        /// <summary>Địa chỉ chính của máy host để hiện và sao chép.</summary>
        private string BuildHostAddress()
        {
            ushort port = bootstrap != null ? bootstrap.HostPort : NetBootstrap.DefaultHostPort;
            return LanHostAddresses.Primary() + ":" + port;
        }

        /// <summary>Liệt kê mọi IPv4 nội bộ kèm cổng để host đọc cho đồng đội.</summary>
        private string BuildHostAddressList()
        {
            ushort port = bootstrap != null ? bootstrap.HostPort : NetBootstrap.DefaultHostPort;
            IReadOnlyList<string> addresses = LanHostAddresses.GetLocalIPv4();
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < addresses.Count; i++)
            {
                if (i > 0) builder.Append('\n');
                builder.Append(addresses[i]).Append(':').Append(port);
            }

            return builder.ToString();
        }

        private bool HandleAddressKey(Event current)
        {
            if (current.keyCode == KeyCode.Backspace)
            {
                if (m_AddressInput.Length > 0)
                {
                    m_AddressInput = m_AddressInput.Substring(0, m_AddressInput.Length - 1);
                }

                RefreshAddressInput();
                return true;
            }

            if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
            {
                TryJoinByAddress();
                return true;
            }

            if (current.keyCode == KeyCode.Escape)
            {
                m_AddressFocused = false;
                RefreshAddressInput();
                return true;
            }

            if (current.control && current.keyCode == KeyCode.V)
            {
                AppendAddress(GUIUtility.systemCopyBuffer);
                return true;
            }

            if (current.character != '\0' && !char.IsControl(current.character))
            {
                AppendAddress(current.character.ToString());
                return true;
            }

            return false;
        }

        private void AppendAddress(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            for (int i = 0; i < value.Length && m_AddressInput.Length < 64; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c) || c == '.' || c == ':' || c == '-')
                {
                    m_AddressInput += c;
                }
            }

            RefreshAddressInput();
        }

        private void RefreshAddressInput()
        {
            if (m_AddressText == null) return;

            if (string.IsNullOrEmpty(m_AddressInput))
            {
                m_AddressText.text = m_AddressFocused ? "|" : "NHẬP IP HOST";
                m_AddressText.color = new Color32(0x6F, 0x7D, 0x8C, 0xFF);
                return;
            }

            m_AddressText.text = m_AddressFocused ? m_AddressInput + "|" : m_AddressInput;
            m_AddressText.color = new Color32(0xE6, 0xED, 0xF5, 0xFF);
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
            SetRootVisible(false);

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
            SetRootVisible(true);

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
            bootstrap.SetLanStageCode(CoopEntry.StageCode);

            if (!bootstrap.StartHost())
            {
                m_Busy = false;
                ushort firstPort = bootstrap.HostPort;
                ushort lastPort = (ushort)Mathf.Min(ushort.MaxValue, firstPort + NetBootstrap.HostPortFallbackCount - 1);
                SetStatus("KHÔNG MỞ ĐƯỢC PHÒNG. CÁC CỔNG " + firstPort + "–" + lastPort + " ĐỀU BẬN.");
                return;
            }

            StartCoroutine(WaitForSession(true));
        }

        private void JoinRoom(LanRoomInfo room)
        {
            if (m_Busy || bootstrap == null) return;

            m_Busy = true;
            SetStatus("ĐANG VÀO PHÒNG…");
            ApplyStageCode(room.StageCode);
            SetStageCaption();
            PrepareLocalLoadout();
            PrepareOfflineTank();
            m_JoinedHostAddress = room.DisplayAddress;
            bootstrap.SetJoinTarget(room.Address, (ushort)room.Port);

            if (!bootstrap.StartClient())
            {
                m_Busy = false;
                SetStatus("KHÔNG KẾT NỐI ĐƯỢC TỚI " + room.DisplayAddress + ".");
                return;
            }

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
            SetStatus("KHÔNG KẾT NỐI ĐƯỢC PHÒNG LAN. KIỂM TRA IP HOST VÀ MẠNG.");
        }

        private void RefreshRooms()
        {
            if (m_Busy) return;
            if (Time.unscaledTime - m_LastQueryTime < 1f) return;
            m_LastQueryTime = Time.unscaledTime;

            m_Rooms.Clear();
            LanDiscoveryService service = Discovery;
            if (service != null)
            {
                IReadOnlyList<LanRoomInfo> live = service.Rooms;
                for (int i = 0; i < live.Count; i++) m_Rooms.Add(live[i]);
            }

            IReadOnlyList<LanRoomRow> rows = LanRoomListModel.Build(m_Rooms);
            for (int i = 0; i < 6; i++)
            {
                bool has = i < rows.Count;
                if (m_RoomRows != null && i < m_RoomRows.Length && m_RoomRows[i] != null)
                {
                    m_RoomRows[i].SetActive(has);
                }

                if (m_RoomNames != null && i < m_RoomNames.Length && m_RoomNames[i] != null)
                {
                    m_RoomNames[i].text = has ? rows[i].Name : "—";
                    m_RoomNames[i].color = has ? new Color32(0xE6, 0xED, 0xF5, 0xFF) : new Color32(0x3B, 0x46, 0x54, 0xFF);
                }

                if (m_RoomDetails != null && i < m_RoomDetails.Length && m_RoomDetails[i] != null)
                {
                    m_RoomDetails[i].text = has ? rows[i].Details : string.Empty;
                }
            }

            if (m_EmptyHint != null)
            {
                m_EmptyHint.SetActive(rows.Count == 0);
            }

            SetStatus(LanRoomListModel.Status(rows.Count));
        }

        private void RefreshRoom()
        {
            if (flow == null || bootstrap == null) return;

            bool isHost = bootstrap.Manager != null && bootstrap.Manager.IsServer;
            if (m_RoomAddress != null)
            {
                m_RoomAddress.text = isHost ? BuildHostAddress() : m_JoinedHostAddress;
            }

            if (m_RoomNote != null)
            {
                m_RoomNote.text = isHost ? BuildHostAddressList() : "ĐANG KẾT NỐI TỚI HOST\n" + m_JoinedHostAddress;
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