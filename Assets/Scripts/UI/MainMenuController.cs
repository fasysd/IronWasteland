using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Progression;
using IronWasteland.Tanks;
using IronWasteland.UiModels;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IronWasteland.UI
{
    /// <summary>
    /// Điều khiển màn hình Main Menu: hiệu ứng vào màn, hover/selected cho bốn mục điều hướng,
    /// hiệu ứng nền trôi nhẹ và chuyển sang scene chiến đấu.
    /// Toàn bộ hình ảnh đã dựng sẵn trong prefab/scene, script chỉ bật tắt và biến đổi transform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Khối gốc")]
        [SerializeField]
        [Tooltip("Nhóm CanvasGroup dùng để làm hiệu ứng mờ dần khi vào màn.")]
        private CanvasGroup m_Root;

        [SerializeField]
        [Tooltip("Ảnh nền toàn màn hình, dùng cho hiệu ứng trôi nhẹ.")]
        private RectTransform m_Background;

        [Header("Bốn mục điều hướng")]
        [SerializeField]
        [Tooltip("Bốn nút điều hướng theo thứ tự: chọn màn chơi, nhà xe, cài đặt, thoát game.")]
        private Image[] m_NavItems = new Image[4];

        [SerializeField]
        [Tooltip("Sprite trạng thái thường của từng mục điều hướng.")]
        private Sprite[] m_NavSpritesNormal = new Sprite[4];

        [SerializeField]
        [Tooltip("Sprite trạng thái đang chọn của từng mục điều hướng.")]
        private Sprite[] m_NavSpritesSelected = new Sprite[4];

        [Header("Chuyển cảnh")]
        [SerializeField]
        [Tooltip("Tên scene chọn ải được mở khi bấm CHỌN MÀN CHƠI.")]
        private string m_StageSelectSceneName = "StageSelect";

        [SerializeField]
        [Tooltip("Tên scene cài đặt được mở khi bấm CÀI ĐẶT.")]
        private string m_SettingSceneName = "Setting";

        [SerializeField]
        [Tooltip("Tên scene Garage được mở khi bấm NHÀ XE & NÂNG CẤP.")]
        private string m_GarageSceneName = "Garage";

        [Header("Xe đang chọn trong Garage")]
        [SerializeField]
        [Tooltip("Ảnh lớn của xe đang chọn ở khu trưng bày bên phải.")]
        private Image m_TankPortrait;

        [SerializeField]
        [Tooltip("Tên xe đang chọn.")]
        private TextMeshProUGUI m_TankName;

        [SerializeField]
        [Tooltip("Dòng CURRENT UNIT ở góc trên khu trưng bày, hiện tên xe đang chọn.")]
        private TextMeshProUGUI m_TankCaption;

        [SerializeField]
        [Tooltip("Số cấp hiện tại của xe, canh theo ô CẤP ĐỘ.")]
        private TextMeshProUGUI m_TankLevel;

        [SerializeField]
        [Tooltip("Hậu tố mẫu số của khung CẤP ĐỘ, ví dụ / 90.")]
        private TextMeshProUGUI m_TankLevelMax;

        [SerializeField]
        [Tooltip("Bốn giá trị chỉ số đang hiện: ATK, HP, EN, SPD.")]
        private TextMeshProUGUI[] m_TankStatValues = new TextMeshProUGUI[4];

        [SerializeField]
        [Tooltip("Roster xe dùng chung với Garage, thứ tự trùng với chỉ số LastTankIndex.")]
        private TankRoster m_Roster;

        [SerializeField]
        [Tooltip("Hai pill vai trò dưới tên xe; pill thứ hai ẩn khi xe chỉ có một vai trò.")]
        private RoleTagView[] m_RoleTags = new RoleTagView[2];

        private const float RoleTagStartX = 1087f;
        private const float RoleTagGap = 10f;

        private const int SectionCampaign = 0;
        private const int SectionGarage = 1;
        private const int SectionSettings = 2;
        private const int SectionQuit = 3;

        private Vector2[] m_NavBasePositions;
        private Vector2 m_BackgroundBasePosition;
        private int m_SelectedIndex = SectionCampaign;
        private int m_HoveredIndex = -1;
        private int m_PressedIndex = -1;
        private int m_HighlightedIndex = -1;

        /// <summary>Vị trí chuột đọc từ sự kiện OnGUI, quy về pixel màn hình của game.</summary>
        private Vector2 m_PointerScreen;

        /// <summary>Đã nhận được vị trí chuột từ OnGUI hay chưa.</summary>
        private bool m_HasGuiPointer;

        /// <summary>OnGUI báo vừa nhấn / vừa nhả chuột trái, chờ Update xử lý.</summary>
        private bool m_GuiPointerDown;
        private bool m_GuiPointerUp;
        private float m_PressTimer;
        private bool m_IsLeaving;

        private void Awake()
        {
            if (m_Root != null)
            {
                m_Root.alpha = 0f;
                m_Root.interactable = false;
                m_Root.blocksRaycasts = false;
            }

            m_NavBasePositions = new Vector2[m_NavItems.Length];
            for (int i = 0; i < m_NavItems.Length; i++)
            {
                if (m_NavItems[i] == null)
                {
                    continue;
                }

                m_NavBasePositions[i] = m_NavItems[i].rectTransform.anchoredPosition;
                m_NavItems[i].rectTransform.anchoredPosition = m_NavBasePositions[i] + new Vector2(-90f, 0f);
                SetNavVisual(i, false);
            }

            if (m_Background != null)
            {
                m_BackgroundBasePosition = m_Background.anchoredPosition;
            }

            ApplySelection(m_SelectedIndex);
            ApplyTankPreview(SkillLoadout.LastTankIndex);
        }

        private void Start()
        {
            StartCoroutine(PlayIntro());
        }

        private void Update()
        {
            UpdateHover();
            UpdateSelectedPulse();
            UpdateBackground();
            UpdatePress();
        }

        /// <summary>Đọc sự kiện chuột của cửa sổ game qua IMGUI — nguồn ổn định khi cửa sổ game không giữ focus.</summary>
        private void OnGUI()
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            switch (current.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    UpdateGuiPointer(current.mousePosition);
                    break;
                case EventType.MouseDown:
                    if (current.button != 0)
                    {
                        return;
                    }

                    UpdateGuiPointer(current.mousePosition);
                    m_GuiPointerDown = true;
                    break;
                case EventType.MouseUp:
                    if (current.button != 0)
                    {
                        return;
                    }

                    UpdateGuiPointer(current.mousePosition);
                    m_GuiPointerUp = true;
                    break;
            }
        }

        /// <summary>Quy vị trí chuột từ không gian GUI (gốc trên-trái) sang pixel màn hình (gốc dưới-trái).</summary>
        private void UpdateGuiPointer(Vector2 guiPosition)
        {
            m_PointerScreen = new Vector2(guiPosition.x, Screen.height - guiPosition.y);
            m_HasGuiPointer = true;
        }

        /// <summary>Hiệu ứng mờ dần và trượt từng mục điều hướng vào màn.</summary>
        private IEnumerator PlayIntro()
        {
            const float fadeDuration = 0.45f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (m_Root != null)
                {
                    m_Root.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                }

                yield return null;
            }

            if (m_Root != null)
            {
                m_Root.alpha = 1f;
                m_Root.interactable = true;
                m_Root.blocksRaycasts = true;
            }

            for (int i = 0; i < m_NavItems.Length; i++)
            {
                if (m_NavItems[i] != null)
                {
                    StartCoroutine(SlideNavItem(i, 0.07f * i));
                }
            }
        }

        /// <summary>Trượt một mục điều hướng từ trái vào đúng vị trí thiết kế.</summary>
        private IEnumerator SlideNavItem(int index, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            RectTransform rect = m_NavItems[index].rectTransform;
            Vector2 from = m_NavBasePositions[index] + new Vector2(-90f, 0f);
            Vector2 to = m_NavBasePositions[index];
            const float duration = 0.3f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                rect.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                yield return null;
            }

            rect.anchoredPosition = to;
        }

        /// <summary>Vị trí chuột theo pixel màn hình, ưu tiên sự kiện OnGUI để vẫn nhận được khi cửa sổ game không giữ focus.</summary>
        private Vector2 PointerPosition
        {
            get
            {
                if (m_HasGuiPointer)
                {
                    return m_PointerScreen;
                }

#if ENABLE_INPUT_SYSTEM
                Mouse mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        /// <summary>Chuột vừa nhấn xuống trong khung hình này.</summary>
        private bool PointerDownThisFrame
        {
            get
            {
                if (m_GuiPointerDown)
                {
                    m_GuiPointerDown = false;
                    return true;
                }

#if ENABLE_INPUT_SYSTEM
                Mouse mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        /// <summary>Chuột vừa nhả ra trong khung hình này.</summary>
        private bool PointerUpThisFrame
        {
            get
            {
                if (m_GuiPointerUp)
                {
                    m_GuiPointerUp = false;
                    return true;
                }

#if ENABLE_INPUT_SYSTEM
                Mouse mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasReleasedThisFrame;
#else
                return Input.GetMouseButtonUp(0);
#endif
            }
        }

        /// <summary>Tìm mục đang được trỏ chuột và cập nhật hiệu ứng nhấc lên nhẹ.</summary>
        private void UpdateHover()
        {
            int hovered = -1;
            Vector3 mouse = PointerPosition;
            for (int i = 0; i < m_NavItems.Length; i++)
            {
                if (m_NavItems[i] == null)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(m_NavItems[i].rectTransform, mouse, null))
                {
                    hovered = i;
                    break;
                }
            }

            m_HoveredIndex = hovered;
            UpdateNavHighlight(m_HoveredIndex >= 0 ? m_HoveredIndex : m_SelectedIndex);

            for (int i = 0; i < m_NavItems.Length; i++)
            {
                if (m_NavItems[i] == null || i >= m_NavBasePositions.Length)
                {
                    continue;
                }

                bool isHovered = i == m_HoveredIndex;
                Vector2 target = m_NavBasePositions[i] + (isHovered ? new Vector2(10f, 0f) : Vector2.zero);
                float targetScale = isHovered ? 1.012f : 1f;

                if (i == m_PressedIndex && m_PressTimer > 0f)
                {
                    targetScale = 0.988f;
                }

                RectTransform rect = m_NavItems[i].rectTransform;
                rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, 1f - Mathf.Pow(0.001f, Time.unscaledDeltaTime));
                float scale = Mathf.Lerp(rect.localScale.x, targetScale, 1f - Mathf.Pow(0.001f, Time.unscaledDeltaTime));
                rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>Nhấp nháy rất nhẹ cho mục đang phát sáng vàng để màn hình có nhịp sống.</summary>
        private void UpdateSelectedPulse()
        {
            if (m_HighlightedIndex < 0 || m_HighlightedIndex >= m_NavItems.Length || m_NavItems[m_HighlightedIndex] == null)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.1f);
            Color highlightColor = Color.white;
            highlightColor.a = Mathf.Lerp(0.9f, 1f, pulse);
            m_NavItems[m_HighlightedIndex].color = highlightColor;
        }

        /// <summary>Nền trôi rất chậm để màn hình không bị tĩnh.</summary>
        private void UpdateBackground()
        {
            if (m_Background == null)
            {
                return;
            }

            float time = Time.unscaledTime;
            Vector2 offset = new Vector2(Mathf.Sin(time * 0.14f) * 10f, Mathf.Cos(time * 0.11f) * 6f);
            m_Background.anchoredPosition = Vector2.Lerp(m_Background.anchoredPosition, m_BackgroundBasePosition + offset, Time.unscaledDeltaTime * 1.5f);
            float scale = 1.02f;
            m_Background.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Xử lý nhấn/ thả chuột trên các mục điều hướng.</summary>
        private void UpdatePress()
        {
            if (m_IsLeaving)
            {
                return;
            }

            if (PointerDownThisFrame)
            {
                m_PressedIndex = m_HoveredIndex;
                m_PressTimer = 0.12f;
            }

            if (m_PressTimer > 0f)
            {
                m_PressTimer -= Time.unscaledDeltaTime;
            }

            if (PointerUpThisFrame && m_PressedIndex >= 0)
            {
                int released = m_HoveredIndex;
                int pressed = m_PressedIndex;
                m_PressedIndex = -1;
                if (released == pressed && released >= 0)
                {
                    ActivateSection(released);
                }
            }
        }

        /// <summary>Chuyển trạng thái chọn sang mục được bấm.</summary>
        private void ActivateSection(int index)
        {
            ApplySelection(index);

            switch (index)
            {
                case SectionCampaign:
                    StartCoroutine(EnterScene(m_StageSelectSceneName));
                    break;
                case SectionGarage:
                    StartCoroutine(EnterScene(m_GarageSceneName));
                    break;
                case SectionSettings:
                    StartCoroutine(EnterScene(m_SettingSceneName));
                    break;
                case SectionQuit:
                    QuitGame();
                    break;
            }
        }

        /// <summary>Cập nhật khu trưng bày theo xe đang chọn trong Garage, đọc thẳng TankRoster + PlayerProgress.</summary>
        private void ApplyTankPreview(int index)
        {
            if (m_Roster == null)
            {
                return;
            }

            IReadOnlyList<TankDefinition> tanks = m_Roster.Tanks;
            if (tanks.Count == 0)
            {
                return;
            }

            TankDefinition tank = tanks[Mathf.Clamp(index, 0, tanks.Count - 1)];
            if (tank == null)
            {
                return;
            }

            int level = PlayerProgress.HasTankLevel(tank.name) ? PlayerProgress.GetTankLevel(tank.name) : tank.StartingLevel;
            LevelFrame frame = LevelFrameModel.Build(level, TankDefinition.MaxLevel);

            if (m_TankPortrait != null)
            {
                m_TankPortrait.sprite = tank.MenuPortrait;
                m_TankPortrait.preserveAspect = true;
            }

            if (m_TankName != null)
            {
                m_TankName.text = tank.TankName;
            }

            if (m_TankCaption != null)
            {
                m_TankCaption.text = "CURRENT UNIT // " + tank.TankName;
            }

            if (m_TankLevel != null)
            {
                m_TankLevel.text = frame.LevelText;
            }

            if (m_TankLevelMax != null)
            {
                m_TankLevelMax.text = frame.MaxText;
            }

            ApplyRoleTags(tank);

            // Bốn ô chỉ số trên dải stats, đúng thứ tự ATK, HP, EN, SPD và cùng cấp với khung CẤP ĐỘ.
            TankStats stats = tank.GetStatsForLevel(level);
            float[] values = { stats.Attack, stats.MaxHealth, stats.MaxEnergy, stats.MoveSpeed };
            for (int i = 0; i < m_TankStatValues.Length; i++)
            {
                if (m_TankStatValues[i] == null)
                {
                    continue;
                }

                int value = i < values.Length ? Mathf.RoundToInt(values[i]) : 0;
                m_TankStatValues[i].text = value.ToString();
            }
        }

        /// <summary>Đổ hai pill vai trò từ RoleTagModel và xếp nối tiếp từ mốc thiết kế 1087px.</summary>
        private void ApplyRoleTags(TankDefinition tank)
        {
            if (m_RoleTags == null || m_RoleTags.Length == 0)
            {
                return;
            }

            Color32 accent = tank.RosterAccent;
            uint accentArgb = ((uint)accent.r << 16) | ((uint)accent.g << 8) | accent.b;
            RoleTag[] tags = RoleTagModel.Build(tank.RolePrimary, tank.RoleSecondary, accentArgb);

            float cursor = RoleTagStartX;
            for (int i = 0; i < m_RoleTags.Length; i++)
            {
                if (m_RoleTags[i] == null)
                {
                    continue;
                }

                if (i >= tags.Length)
                {
                    m_RoleTags[i].Hide();
                    continue;
                }

                uint argb = tags[i].Argb;
                Color32 color = new Color32((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, 0xFF);
                float width = m_RoleTags[i].Bind(tags[i].Text, color);
                m_RoleTags[i].SetLeft(cursor);
                cursor += width + RoleTagGap;
            }
        }

        /// <summary>Đổi sprite thường/đang chọn cho các mục điều hướng.</summary>
        private void ApplySelection(int index)
        {
            m_SelectedIndex = Mathf.Clamp(index, 0, m_NavItems.Length - 1);
            UpdateNavHighlight(m_HoveredIndex >= 0 ? m_HoveredIndex : m_SelectedIndex);
        }

        /// <summary>Dời sprite vàng sang mục đang được trỏ chuột hoặc mục đang chọn.</summary>
        private void UpdateNavHighlight(int index)
        {
            if (index == m_HighlightedIndex)
            {
                return;
            }

            m_HighlightedIndex = index;
            for (int i = 0; i < m_NavItems.Length; i++)
            {
                SetNavVisual(i, i == m_HighlightedIndex);
            }
        }

        private void SetNavVisual(int index, bool selected)
        {
            if (index < 0 || index >= m_NavItems.Length || m_NavItems[index] == null)
            {
                return;
            }

            Sprite[] source = selected ? m_NavSpritesSelected : m_NavSpritesNormal;
            if (source != null && index < source.Length && source[index] != null)
            {
                m_NavItems[index].sprite = source[index];
            }
        }

        private IEnumerator EnterScene(string sceneName)
        {
            m_IsLeaving = true;
            const float duration = 0.35f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (m_Root != null)
                {
                    m_Root.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                }

                yield return null;
            }

            if (!string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogWarning("[MainMenu] Không tìm thấy scene: " + sceneName);
                m_IsLeaving = false;
                if (m_Root != null)
                {
                    m_Root.alpha = 1f;
                }
            }
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
