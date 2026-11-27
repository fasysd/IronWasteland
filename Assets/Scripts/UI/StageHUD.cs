using TMPro;
using IronWasteland.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using IronWasteland.Stages;
using IronWasteland.Tanks;
using IronWasteland.Enemies;
using IronWasteland.Audio;

namespace IronWasteland.UI
{
    /// <summary>
    /// HUD trong man choi: ten man, dieu kien thang, so quai da diet, dong ho,
    /// va bang ket qua khi thang/thua.
    ///
    /// Nguyen tac: HUD KHONG quyet dinh gi ca - no chi nghe event cua StageDirector
    /// roi hien thi. Nho vay doi luat choi khong phai sua UI.
    ///
    /// Cach dung: gan component nay len object co Canvas (hoac con cua Canvas),
    /// keo StageDirector + cac TMP_Text vao Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class StageHUD : MonoBehaviour
    {
        [Header("Nguồn dữ liệu")]
        [SerializeField] private StageDirector director;

        [Header("Thanh trên")]
        [SerializeField] private TMP_Text stageTitle;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text killsText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text gemsText;

        [Header("Thanh Boss (tuỳ chọn — chỉ màn boss)")]
        [SerializeField] private GameObject bossPanel;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private TMP_Text bossHpText;
        [SerializeField] private Image bossFill;

        [Header("Cài đặt (ESC)")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button leaveButton;

        [Header("Bảng kết quả")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultDetail;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextButtonLabel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button selectButton;

        [Header("Hiển thị")]
        [Tooltip("Định dạng đồng hồ: phút:giây.")]
        [SerializeField] private bool showTimer = true;

        /// <summary>Hook cho self-check: Image của thanh máu boss (null nếu prefab chưa nối).</summary>
        internal Image BossFillImage => bossFill;

        private void Awake()
        {
            if (director == null) director = FindAnyObjectByType<StageDirector>();
            if (resultPanel != null) resultPanel.SetActive(false);

            ResolveBossPanel();
            ResolveSettingsPanel();
            WireButtons();
        }

        /// <summary>
        /// Popup Cai dat (ESC): hien de xem am luong + bang phim, KHONG tam dung tran dau
        /// (dung theo guide wireframe: "GAME KHONG TAM DUNG").
        /// </summary>
        private void ResolveSettingsPanel()
        {
            if (settingsPanel == null)
            {
                Transform found = transform.Find("SettingsPanel");
                if (found != null) settingsPanel = found.gameObject;
            }

            if (settingsPanel == null) return;

            if (masterSlider == null) masterSlider = FindDeep<Slider>(settingsPanel.transform, "MasterSlider");
            if (sfxSlider == null) sfxSlider = FindDeep<Slider>(settingsPanel.transform, "SfxSlider");
            if (resumeButton == null) resumeButton = FindDeep<Button>(settingsPanel.transform, "ResumeButton");
            if (leaveButton == null) leaveButton = FindDeep<Button>(settingsPanel.transform, "LeaveButton");

            settingsPanel.SetActive(false);

            if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (resumeButton != null) resumeButton.onClick.AddListener(ToggleSettings);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
        }

        private static T FindDeep<T>(Transform root, string namePart) where T : Component
        {
            T[] all = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0) return all[i];
            }
            return null;
        }

        private void ToggleSettings()
        {
            if (settingsPanel == null) return;

            // Dang xem bang ket qua thi khong mo cai dat chong len.
            if (resultPanel != null && resultPanel.activeSelf) return;

            bool show = !settingsPanel.activeSelf;
            settingsPanel.SetActive(show);

            if (show)
            {
                AudioManager am = AudioManager.Instance;
                if (am != null)
                {
                    if (masterSlider != null) masterSlider.SetValueWithoutNotify(am.MasterVolume);
                    if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(am.SfxVolume);
                }
            }
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.MasterVolume = value;
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SfxVolume = value;
        }

        private void OnLeaveClicked()
        {
            Time.timeScale = 1f;
            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive)
            {
                IronWasteland.Net.MatchFlow flow = FindAnyObjectByType<IronWasteland.Net.MatchFlow>();
                if (flow != null)
                {
                    flow.Leave();
                    return;
                }

                IronWasteland.Net.NetBootstrap bootstrap = FindAnyObjectByType<IronWasteland.Net.NetBootstrap>();
                if (bootstrap != null) bootstrap.Shutdown();
                else Unity.Netcode.NetworkManager.Singleton?.Shutdown();
            }

            SceneManager.LoadScene("StageSelect");
        }

        /// <summary>
        /// Tu tim thanh Boss trong prefab theo ten neu Inspector chua gan.
        /// Giu HUD chay duoc ca khi prefab duoc dung lai tu code.
        /// </summary>
        private void ResolveBossPanel()
        {
            if (bossPanel == null)
            {
                Transform found = transform.Find("TopCenter/BossPanel");
                if (found != null) bossPanel = found.gameObject;
            }

            if (bossPanel == null) return;

            if (bossNameText == null) bossNameText = bossPanel.GetComponentInChildren<TMP_Text>(true);

            if (bossFill == null)
            {
                Image[] images = bossPanel.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i].name.IndexOf("BossBar", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        bossFill = images[i];
                        break;
                    }
                }
            }

            bossPanel.SetActive(false);
        }

        private void WireButtons()
        {
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(OnNextClicked);
            }
            if (retryButton != null)
            {
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(OnRetryClicked);
            }
            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnSelectClicked);
            }
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(ToggleSettings);
            }
        }

        private void OnEnable()
        {
            if (director == null) return;
            director.StageStarted += OnStageStarted;
            director.KillCountChanged += OnKillsChanged;
            director.StageCleared += OnStageCleared;
            director.StageFailed += OnStageFailed;
        }

        private void OnDisable()
        {
            if (director == null) return;
            director.StageStarted -= OnStageStarted;
            director.KillCountChanged -= OnKillsChanged;
            director.StageCleared -= OnStageCleared;
            director.StageFailed -= OnStageFailed;
        }

        private float m_ObjectiveRefreshTimer;
        private EnemyBrain m_Boss;
        private float m_BossScanTimer;

        private void Update()
        {
            if (director == null) return;

            // ESC mo/dong popup Cai dat (game KHONG tam dung, chi phu lop mo).
            if (UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ToggleSettings();
            }

            // Muc tieu kieu "Sinh ton" tien theo thoi gian ma khong co su kien giet quai,
            // nen phai lam moi dinh ky (0,2 s) chu khong chi dua vao OnKillsChanged.
            m_ObjectiveRefreshTimer -= Time.deltaTime;
            if (m_ObjectiveRefreshTimer <= 0f)
            {
                m_ObjectiveRefreshTimer = 0.2f;
                RefreshObjectives();
            }

            if (timerText != null && showTimer)
                timerText.text = FormatTime(director.Elapsed);

            if (waveText != null)
            {
                int total = Mathf.Max(1, director.TotalWaves);
                // WavesFired đếm theo thời gian; WaveIndex là vị trí khai báo nên có thể nhảy ngược.
                int current = Mathf.Clamp(director.WavesFired, 1, total);
                waveText.text = $"Đợt {current} / {total}";
            }

            if (killsText != null)
            {
                if (director.KillTarget > 0)
                    killsText.text = $"Đã diệt: {director.Kills}/{director.KillTarget}";
                else
                    killsText.text = $"Đã diệt: {director.Kills}";
            }

            if (gemsText != null && SaveSystem.Instance != null) gemsText.text = $"Ngọc: {SaveSystem.Instance.Gems}";

            RefreshBossPanel();
        }

        /// <summary>
        /// Hien thanh mau cua boss dang song (neu man co boss).
        /// Quet lai moi 1 s vi boss duoc tao bang prefab quai co san, khong co event rieng.
        /// </summary>
        private void RefreshBossPanel()
        {
            if (bossPanel == null) return;

            if (m_Boss == null || !m_Boss.IsAlive)
            {
                m_BossScanTimer -= Time.deltaTime;
                if (m_BossScanTimer > 0f)
                {
                    bossPanel.SetActive(false);
                    return;
                }

                m_BossScanTimer = 1f;
                m_Boss = FindBoss();
            }

            if (m_Boss == null)
            {
                bossPanel.SetActive(false);
                return;
            }

            bossPanel.SetActive(true);
            if (bossNameText != null)
                bossNameText.text = string.IsNullOrEmpty(m_Boss.DisplayName) ? m_Boss.name : m_Boss.DisplayName;

            if (bossHpText != null && m_Boss.Stats != null && m_Boss.Stats.MaxHealth > 0f)
            {
                bossHpText.text = $"{GroupDigits(Mathf.CeilToInt(m_Boss.CurrentHealth))} / " +
                                  $"{GroupDigits(Mathf.CeilToInt(m_Boss.Stats.MaxHealth))} HP";
            }

            if (bossFill != null && m_Boss.Stats != null && m_Boss.Stats.MaxHealth > 0f)
                bossFill.fillAmount = Mathf.Clamp01(m_Boss.CurrentHealth / m_Boss.Stats.MaxHealth);
        }

        /// <summary>20.250 thay vì 20250 (dấu chấm ngăn nghìn kiểu Việt Nam).</summary>
        private static string GroupDigits(int value)
        {
            return value.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
        }

        private static EnemyBrain FindBoss()
        {
            BossTag[] tags = Object.FindObjectsByType<BossTag>(FindObjectsInactive.Exclude);
            for (int i = 0; i < tags.Length; i++)
            {
                EnemyBrain brain = tags[i].GetComponent<EnemyBrain>();
                if (brain != null && brain.IsAlive) return brain;
            }
            return null;
        }

        private void OnStageStarted(StageDirector d)
        {
            if (d.Definition != null)
            {
                if (stageTitle != null) stageTitle.text = $"{d.Definition.StageCode} - {d.Definition.StageName}";
            }

            if (resultPanel != null) resultPanel.SetActive(false);
            RefreshObjectives();
        }

        private void OnKillsChanged(int kills)
        {
            RefreshObjectives();
        }

        private void RefreshObjectives()
        {
            if (objectiveText == null || director == null) return;

            var objectives = director.Objectives;
            if (objectives == null || objectives.Count == 0)
            {
                objectiveText.text = string.Empty;
                return;
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < objectives.Count; i++)
            {
                StageObjective objective = objectives[i];
                if (objective == null) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(objective.Label);
                if (objective.IsComplete) sb.Append("  [x]");
            }
            objectiveText.text = sb.ToString();
        }

        private void OnStageCleared(StageDirector d, StageRewardResult reward)
        {
            StageDefinition def = d.Definition;
            string code = def != null ? def.StageCode : string.Empty;
            string next = def != null ? def.NextStageCode : string.Empty;

            // Ghi tien do: cong ngoc + phu kien + mo khoa man ke.
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.MarkCleared(code, reward.Gems, d.Elapsed, next);
                if (reward.DroppedAccessoryIndices != null)
                {
                    for (int i = 0; i < reward.DroppedAccessoryIndices.Length; i++)
                    {
                        SaveSystem.Instance.AddAccessory(reward.DroppedAccessoryIndices[i], 1);
                    }
                }
            }

            // Bang ket qua theo wireframe: thoi gian, so quai diet, % mau con lai, ngoc, phu kien.
            string stageName = def != null ? def.StageName : string.Empty;
            float hpPercent = 0f;
            GameController gc = FindAnyObjectByType<GameController>();
            if (gc != null && gc.PlayerTank != null && gc.PlayerTank.Stats != null && gc.PlayerTank.Stats.MaxHealth > 0f)
                hpPercent = Mathf.Clamp01(gc.PlayerTank.CurrentHealth / gc.PlayerTank.Stats.MaxHealth) * 100f;

            ShowResult(
                "CHIẾN THẮNG",
                $"Ải {code} - {stageName}\n" +
                $"Thời gian hoàn thành: {FormatTime(d.Elapsed)}\n" +
                $"Quái đã diệt: {d.Kills}\n" +
                $"Máu còn lại: {hpPercent:0}%\n\n" +
                $"Ngọc Ma Pháp: +{reward.Gems}   (tổng {(SaveSystem.Instance != null ? SaveSystem.Instance.Gems : 0)})\n" +
                $"Phụ kiện rơi: {reward.AccessoryCount}");

            if (nextButton != null)
            {
                // Man truoc co the da an nut nay khi thua -> hien lai truoc khi doi nhan.
                nextButton.gameObject.SetActive(true);
                bool hasNext = HasPlayableNext(next);
                nextButton.interactable = true;
                if (nextButtonLabel != null) nextButtonLabel.text = hasNext ? "Màn tiếp theo" : "Về màn chọn";
            }
        }

        /// <summary>
        /// Man ke co ton tai va da nam trong Build Settings hay khong.
        /// Neu chua (vi du dang lam dan 1-1 -> 1-2), nut "Man tiep theo" se quay ve man chon
        /// thay vi LoadScene mot scene khong ton tai (Unity se nem loi).
        /// </summary>
        private static bool HasPlayableNext(string nextCode)
        {
            if (string.IsNullOrEmpty(nextCode)) return false;
            return Application.CanStreamedLevelBeLoaded(SaveSystem.SceneNameFor(nextCode));
        }

        private void OnStageFailed(StageDirector d, string reason)
        {
            ShowResult("THẤT BẠI", reason);

            // Thua thi khong co "man tiep theo": an nut do di, chi con "Choi lai" va "Ve man chon".
            if (nextButton != null) nextButton.gameObject.SetActive(false);

            // Nếu trong phiên mạng co-op mà không phải server (client), ẩn nút Chơi lại để client về chọn màn
            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive)
            {
                bool isServer = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer;
                if (!isServer && retryButton != null)
                {
                    retryButton.gameObject.SetActive(false);
                }
            }
        }

        private void ShowResult(string title, string detail)
        {
            // Bang ket qua la lop cao nhat: dong popup cai dat de hai lop khong chong nhau.
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(true);
            if (resultTitle != null) resultTitle.text = title;
            if (resultDetail != null) resultDetail.text = detail;
        }

        private void OnNextClicked()
        {
            if (director == null) { OnSelectClicked(); return; }

            StageDefinition def = director.Definition;
            string next = def != null ? def.NextStageCode : string.Empty;
            if (director.State != StageRunState.Cleared || !HasPlayableNext(next))
            {
                OnSelectClicked();
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(SaveSystem.SceneNameFor(next));
        }

        private void OnRetryClicked()
        {
            Time.timeScale = 1f;
            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive)
            {
                IronWasteland.Net.MatchFlow flow = FindAnyObjectByType<IronWasteland.Net.MatchFlow>();
                if (flow != null && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
                {
                    flow.Replay();
                    return;
                }
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OnSelectClicked()
        {
            Time.timeScale = 1f;
            if (IronWasteland.Tanks.TankBrain.NetworkSessionActive)
            {
                IronWasteland.Net.MatchFlow flow = FindAnyObjectByType<IronWasteland.Net.MatchFlow>();
                if (flow != null)
                {
                    flow.Leave();
                    return;
                }

                IronWasteland.Net.NetBootstrap bootstrap = FindAnyObjectByType<IronWasteland.Net.NetBootstrap>();
                if (bootstrap != null) bootstrap.Shutdown();
                else Unity.Netcode.NetworkManager.Singleton?.Shutdown();
            }
            SceneManager.LoadScene("StageSelect");
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
