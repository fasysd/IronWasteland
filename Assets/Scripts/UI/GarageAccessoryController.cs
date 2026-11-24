using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using IronWasteland.Save;
using IronWasteland.Tanks;

namespace IronWasteland.UI
{
    /// <summary>
    /// Điều khiển màn CHỌN PHỤ KIỆN TRANG BỊ. Màn này được nạp chồng lên Nhà xe,
    /// cho phép kéo ngang để xem toàn bộ 20 phụ kiện, chọn một phụ kiện cho xe hiện tại
    /// rồi lưu lại bằng nút XÁC NHẬN TRANG BỊ.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarageAccessoryController : MonoBehaviour
    {
        /// <summary>Tham chiếu hiển thị của một thẻ phụ kiện.</summary>
        [Serializable]
        public sealed class CardView
        {
            [Tooltip("Gốc của thẻ, dùng để ẩn khi danh mục ít hơn số thẻ dựng sẵn.")]
            public GameObject Root;

            [Tooltip("Ảnh nền thẻ, đổi giữa bản thường và bản đang chọn.")]
            public Image Background;

            [Tooltip("Viền sáng khi rê chuột.")]
            public Image Hover;

            [Tooltip("Nhóm badge ĐANG DÙNG và dấu tick, chỉ bật cho thẻ đang chọn.")]
            public GameObject SelectedGroup;

            [Tooltip("Nhãn LỰA CHỌN ở góc phải thẻ, ẩn khi thẻ đang được dùng.")]
            public TextMeshProUGUI ChoiceLabel;

            [Tooltip("Icon phụ kiện.")]
            public Image Icon;

            [Tooltip("Số thứ tự 01..20.")]
            public TextMeshProUGUI Number;

            [Tooltip("Nhóm phụ kiện.")]
            public TextMeshProUGUI Category;

            [Tooltip("Tên ngắn của phụ kiện.")]
            public TextMeshProUGUI Name;

            [Tooltip("Dòng vai trò dưới tên.")]
            public TextMeshProUGUI Role;

            [Tooltip("Các dòng chỉ số màu xanh.")]
            public TextMeshProUGUI Stats;

            [Tooltip("Mô tả hiệu ứng.")]
            public TextMeshProUGUI Description;

            [Tooltip("Chữ TRANG BỊ ở chân thẻ, ẩn khi thẻ đang được dùng.")]
            public TextMeshProUGUI Footer;
        }

        [Header("Dữ liệu")]
        [Tooltip("Danh mục 20 phụ kiện dùng chung với Nhà xe.")]
        [SerializeField] private AccessoryCatalog m_Catalog;

        [Header("Tham chiếu giao diện")]
        [Tooltip("Dòng đường dẫn trên tiêu đề: <tên xe> / PHỤ KIỆN / NHÀ XE.")]
        [SerializeField] private TextMeshProUGUI m_Breadcrumb;

        [Tooltip("Dòng phụ đề TỔNG ... PHỤ KIỆN.")]
        [SerializeField] private TextMeshProUGUI m_Subtitle;

        [Tooltip("20 thẻ phụ kiện xếp từ trái sang phải.")]
        [SerializeField] private CardView[] m_Cards = new CardView[20];

        [Tooltip("Vùng nội dung chứa 20 thẻ, dịch ngang khi kéo.")]
        [SerializeField] private RectTransform m_Content;

        [Tooltip("Vùng nhìn thấy của danh sách thẻ.")]
        [SerializeField] private RectTransform m_Viewport;

        [Tooltip("Thanh báo vị trí cuộn ngang.")]
        [SerializeField] private Image m_ScrollThumb;

        [Tooltip("Ảnh nền thẻ thường.")]
        [SerializeField] private Sprite m_CardNormalSprite;

        [Tooltip("Ảnh nền thẻ đang chọn.")]
        [SerializeField] private Sprite m_CardSelectedSprite;

        [Header("Bố cục danh sách")]
        [Tooltip("Khoảng cách ngang giữa hai thẻ.")]
        [SerializeField] private float m_CardPitch = 290.4f;

        [Tooltip("Chiều rộng một thẻ.")]
        [SerializeField] private float m_CardWidth = 276f;

        [Tooltip("Toạ độ X của thanh cuộn.")]
        [SerializeField] private float m_ScrollTrackX = 96f;

        [Tooltip("Chiều dài thanh cuộn.")]
        [SerializeField] private float m_ScrollTrackWidth = 1728f;

        [Tooltip("Toạ độ Y của thanh cuộn.")]
        [SerializeField] private float m_ScrollTrackY = 882f;

        [Header("Vùng bấm theo toạ độ thiết kế (gốc trên-trái, hệ 1920x1080)")]
        [Tooltip("Vùng bấm nút X ở góc trên phải.")]
        [SerializeField] private Rect m_CloseArea = new Rect(1786f, 160f, 52f, 52f);

        [Tooltip("Vùng bấm nút HỦY BỎ ở chân panel.")]
        [SerializeField] private Rect m_CancelArea = new Rect(1210f, 900f, 290f, 52f);

        [Tooltip("Vùng bấm nút XÁC NHẬN TRANG BỊ.")]
        [SerializeField] private Rect m_ConfirmArea = new Rect(1526f, 900f, 296f, 52f);

        [Tooltip("Vùng nhìn thấy của danh sách, dùng để bắt đầu kéo và bấm thẻ.")]
        [SerializeField] private Rect m_ViewportArea = new Rect(96f, 276f, 1728f, 596f);

        [Header("Scene đích")]
        [Tooltip("Scene Nhà xe để quay lại khi màn này được chạy riêng lẻ.")]
        [SerializeField] private string m_BackSceneName = "Garage";

        private Canvas m_Canvas;
        private int m_TankIndex;
        private int m_SavedIndex;
        private int m_PendingIndex;
        private int m_HoveredIndex = -1;
        private float m_Scroll;
        private float m_MaxScroll;
        private bool m_Dragging;
        private bool m_DragMoved;
        private float m_DragStartX;
        private float m_DragStartScroll;
        private Vector2 m_PointerScreen;
        private bool m_HasGuiPointer;
        private bool m_GuiPointerDown;
        private bool m_GuiPointerUp;
        private bool m_Pressed;

        /// <summary>Chỉ số xe đang chỉnh phụ kiện.</summary>
        public int TankIndex => m_TankIndex;

        /// <summary>Chỉ số phụ kiện đang chờ xác nhận.</summary>
        public int PendingIndex => m_PendingIndex;

        private void Awake()
        {
            m_Canvas = GetComponent<Canvas>();
            if (m_Canvas == null)
            {
                m_Canvas = GetComponentInParent<Canvas>();
            }

            m_TankIndex = Mathf.Clamp(SaveSystem.Instance.LastSelectedTankIndex, 0, SkillCatalog.TankCount - 1);
            int count = m_Catalog != null ? m_Catalog.Count : 0;
            if (count <= 0)
            {
                Debug.LogError("[GarageAccessoryController] Danh mục phụ kiện rỗng, không thể dựng thẻ.");
                return;
            }

            m_SavedIndex = SaveSystem.Instance.GetEquippedAccessory(m_TankIndex);
            m_PendingIndex = m_SavedIndex;
            RefreshHeader();
            RefreshCards();
            SetScroll(0f, false);
            EnsureVisible(m_PendingIndex);
        }

        private void Update()
        {
            if (m_GuiPointerDown)
            {
                m_GuiPointerDown = false;
                m_Pressed = true;
            }

            if (m_GuiPointerUp)
            {
                m_GuiPointerUp = false;
                if (m_Pressed)
                {
                    m_Pressed = false;
                    HandleClick(DesignPointerPosition(PointerPosition));
                }
            }

            UpdateHover(DesignPointerPosition(PointerPosition));
        }

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
                    m_PointerScreen = ToScreenPoint(current.mousePosition);
                    m_HasGuiPointer = true;
                    break;
                case EventType.MouseDown:
                    if (current.button == 0)
                    {
                        Vector2 design = DesignPointerPosition(ToScreenPoint(current.mousePosition));
                        m_PointerScreen = ToScreenPoint(current.mousePosition);
                        m_HasGuiPointer = true;
                        m_GuiPointerDown = true;
                        if (m_ViewportArea.Contains(design))
                        {
                            m_Dragging = true;
                            m_DragMoved = false;
                            m_DragStartX = design.x;
                            m_DragStartScroll = m_Scroll;
                        }
                    }

                    break;
                case EventType.MouseDrag:
                    if (current.button == 0 && m_Dragging)
                    {
                        Vector2 design = DesignPointerPosition(ToScreenPoint(current.mousePosition));
                        m_PointerScreen = ToScreenPoint(current.mousePosition);
                        m_HasGuiPointer = true;
                        float delta = design.x - m_DragStartX;
                        if (Mathf.Abs(delta) > 6f)
                        {
                            m_DragMoved = true;
                        }

                        SetScroll(m_DragStartScroll - delta, true);
                    }

                    break;
                case EventType.MouseUp:
                    if (current.button == 0)
                    {
                        m_PointerScreen = ToScreenPoint(current.mousePosition);
                        m_HasGuiPointer = true;
                        if (m_Dragging && m_DragMoved)
                        {
                            m_Pressed = false;
                            m_GuiPointerDown = false;
                        }

                        m_Dragging = false;
                        m_GuiPointerUp = true;
                    }

                    break;
                case EventType.ScrollWheel:
                    Vector2 wheelPoint = DesignPointerPosition(ToScreenPoint(current.mousePosition));
                    if (m_ViewportArea.Contains(wheelPoint))
                    {
                        SetScroll(m_Scroll - current.delta.y * 90f, true);
                        current.Use();
                    }

                    break;
                case EventType.KeyDown:
                    if (current.keyCode == KeyCode.Escape)
                    {
                        Close();
                    }
                    else if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
                    {
                        Confirm();
                    }
                    else if (current.keyCode == KeyCode.LeftArrow)
                    {
                        SelectRelative(-1);
                    }
                    else if (current.keyCode == KeyCode.RightArrow)
                    {
                        SelectRelative(1);
                    }

                    break;
            }
        }

        /// <summary>Cập nhật tiêu đề theo xe đang chọn và số lượng phụ kiện.</summary>
        private void RefreshHeader()
        {
            if (m_Breadcrumb != null)
            {
                m_Breadcrumb.text = SkillCatalog.GetTankName(m_TankIndex) + " / PHỤ KIỆN / NHÀ XE";
            }

            if (m_Subtitle != null && m_Catalog != null)
            {
                m_Subtitle.text = "TỔNG " + m_Catalog.Count.ToString("00") + " PHỤ KIỆN  ·  MỖI XE CHỈ TRANG BỊ ĐƯỢC 1 PHỤ KIỆN";
            }
        }

        /// <summary>Đổ dữ liệu danh mục vào 20 thẻ và cập nhật trạng thái chọn.</summary>
        private void RefreshCards()
        {
            int count = m_Catalog != null ? m_Catalog.Count : 0;
            for (int i = 0; i < m_Cards.Length; i++)
            {
                CardView card = m_Cards[i];
                if (card == null)
                {
                    continue;
                }

                bool exists = i < count;
                if (card.Root != null)
                {
                    card.Root.SetActive(exists);
                }

                if (!exists)
                {
                    continue;
                }

                AccessoryCatalog.Entry entry = m_Catalog.Get(i);
                if (entry == null)
                {
                    continue;
                }

                SetText(card.Number, (i + 1).ToString("00"));
                SetText(card.Category, entry.Category);
                SetText(card.Name, entry.DisplayName);
                SetText(card.Role, entry.Role);
                SetText(card.Stats, BuildStats(entry.Stats));
                SetText(card.Description, entry.Description);
                int ownedQty = SaveSystem.Instance.GetAccessoryQuantity(i);
                bool isOwned = ownedQty > 0;
                string footerText = isOwned ? "TRANG BỊ  ›" : "CHƯA CÓ";
                SetText(card.Footer, footerText);
                SetText(card.ChoiceLabel, isOwned ? $"X{ownedQty}" : "CHƯA CÓ");
                if (card.Icon != null)
                {
                    card.Icon.sprite = entry.Icon;
                    card.Icon.enabled = entry.Icon != null;
                    card.Icon.color = isOwned ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                }

                bool selected = i == m_PendingIndex;
                if (card.Background != null)
                {
                    card.Background.sprite = selected ? m_CardSelectedSprite : m_CardNormalSprite;
                }

                if (card.SelectedGroup != null)
                {
                    card.SelectedGroup.SetActive(selected);
                }

                if (card.ChoiceLabel != null)
                {
                    card.ChoiceLabel.gameObject.SetActive(!selected);
                }

                if (card.Footer != null)
                {
                    card.Footer.gameObject.SetActive(!selected);
                }

                if (card.Hover != null)
                {
                    card.Hover.enabled = false;
                }
            }

            m_HoveredIndex = -1;
        }

        /// <summary>Nối các dòng chỉ số thành một khối chữ nhiều dòng.</summary>
        private static string BuildStats(string[] stats)
        {
            if (stats == null || stats.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < stats.Length; i++)
            {
                if (string.IsNullOrEmpty(stats[i]))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append("• ").Append(stats[i]);
            }

            return builder.ToString();
        }

        /// <summary>Chọn một thẻ phụ kiện để chờ xác nhận.</summary>
        private void Select(int index)
        {
            if (m_Catalog == null || index < 0 || index >= m_Catalog.Count || index == m_PendingIndex)
            {
                return;
            }

            m_PendingIndex = index;
            RefreshCards();
            EnsureVisible(m_PendingIndex);
        }

        /// <summary>Chọn thẻ kế bên bằng phím mũi tên.</summary>
        private void SelectRelative(int delta)
        {
            if (m_Catalog == null || m_Catalog.Count == 0)
            {
                return;
            }

            int next = Mathf.Clamp(m_PendingIndex + delta, 0, m_Catalog.Count - 1);
            Select(next);
        }

        /// <summary>Lưu lựa chọn rồi đóng màn chọn phụ kiện.</summary>
        private void Confirm()
        {
            if (m_Catalog == null || m_Catalog.Count == 0)
            {
                return;
            }

            if (m_PendingIndex >= 0 && !SaveSystem.Instance.HasAccessory(m_PendingIndex))
            {
                Debug.LogWarning($"[GarageAccessoryController] Chưa sở hữu phụ kiện #{m_PendingIndex}. Không thể trang bị.");
                return;
            }

            SaveSystem.Instance.SetEquippedAccessory(m_TankIndex, m_PendingIndex);
            SaveSystem.Instance.LastSelectedTankIndex = m_TankIndex;
            m_SavedIndex = m_PendingIndex;
            Close();
        }

        /// <summary>Đóng màn mà không lưu thay đổi.</summary>
        private void Close()
        {
            if (SceneManager.sceneCount > 1)
            {
                SceneManager.UnloadSceneAsync(gameObject.scene);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(m_BackSceneName))
            {
                Debug.LogError("[GarageAccessoryController] Không tải được scene " + m_BackSceneName);
                return;
            }

            SceneManager.LoadScene(m_BackSceneName);
        }

        /// <summary>Xử lý cú bấm tại một điểm trong hệ toạ độ thiết kế.</summary>
        private void HandleClick(Vector2 designPosition)
        {
            if (m_CloseArea.Contains(designPosition) || m_CancelArea.Contains(designPosition))
            {
                Close();
                return;
            }

            if (m_ConfirmArea.Contains(designPosition))
            {
                Confirm();
                return;
            }

            if (!m_ViewportArea.Contains(designPosition))
            {
                return;
            }

            float localX = designPosition.x - m_ViewportArea.x + m_Scroll;
            int index = Mathf.FloorToInt(localX / m_CardPitch);
            if (index < 0 || index >= m_Cards.Length)
            {
                return;
            }

            if (localX - index * m_CardPitch > m_CardWidth)
            {
                return;
            }

            Select(index);
        }

        /// <summary>Cập nhật viền sáng của thẻ đang rê chuột.</summary>
        private void UpdateHover(Vector2 designPosition)
        {
            int hovered = -1;
            if (!m_Dragging && m_ViewportArea.Contains(designPosition))
            {
                float localX = designPosition.x - m_ViewportArea.x + m_Scroll;
                int index = Mathf.FloorToInt(localX / m_CardPitch);
                if (index >= 0 && index < m_Cards.Length && localX - index * m_CardPitch <= m_CardWidth)
                {
                    hovered = index;
                }
            }

            if (hovered == m_HoveredIndex)
            {
                return;
            }

            m_HoveredIndex = hovered;
            for (int i = 0; i < m_Cards.Length; i++)
            {
                if (m_Cards[i] != null && m_Cards[i].Hover != null)
                {
                    m_Cards[i].Hover.enabled = i == m_HoveredIndex && i != m_PendingIndex;
                }
            }
        }

        /// <summary>Cuộn danh sách tới vị trí mong muốn.</summary>
        private void SetScroll(float value, bool updateThumb)
        {
            float viewWidth = m_Viewport != null ? m_Viewport.rect.width : m_ViewportArea.width;
            float contentWidth = m_Content != null ? m_Content.rect.width : viewWidth;
            m_MaxScroll = Mathf.Max(0f, contentWidth - viewWidth);
            m_Scroll = Mathf.Clamp(value, 0f, m_MaxScroll);
            if (m_Content != null)
            {
                m_Content.anchoredPosition = new Vector2(-m_Scroll, 0f);
            }

            if (updateThumb || m_ScrollThumb != null)
            {
                RefreshThumb(viewWidth, contentWidth);
            }
        }

        /// <summary>Đưa thẻ đang chọn vào vùng nhìn thấy.</summary>
        private void EnsureVisible(int index)
        {
            if (m_Viewport == null)
            {
                return;
            }

            float viewWidth = m_Viewport.rect.width;
            float cardX = index * m_CardPitch;
            float target = m_Scroll;
            if (cardX < m_Scroll)
            {
                target = cardX;
            }
            else if (cardX + m_CardWidth > m_Scroll + viewWidth)
            {
                target = cardX + m_CardWidth - viewWidth;
            }

            if (!Mathf.Approximately(target, m_Scroll))
            {
                SetScroll(target, true);
            }
        }

        /// <summary>Cập nhật độ dài và vị trí thanh cuộn theo tỉ lệ nội dung.</summary>
        private void RefreshThumb(float viewWidth, float contentWidth)
        {
            if (m_ScrollThumb == null)
            {
                return;
            }

            m_ScrollThumb.enabled = m_MaxScroll > 0.5f;
            if (!m_ScrollThumb.enabled)
            {
                return;
            }

            float trackWidth = m_ScrollTrackWidth;
            float thumbWidth = Mathf.Clamp(trackWidth * viewWidth / Mathf.Max(1f, contentWidth), 120f, trackWidth);
            float progress = m_MaxScroll > 0.001f ? m_Scroll / m_MaxScroll : 0f;
            RectTransform rect = m_ScrollThumb.rectTransform;
            rect.sizeDelta = new Vector2(thumbWidth, rect.sizeDelta.y);
            rect.anchoredPosition = new Vector2(m_ScrollTrackX + progress * (trackWidth - thumbWidth), -m_ScrollTrackY);
        }

        /// <summary>Đổi điểm chuột của IMGUI (gốc trên-trái) sang toạ độ màn hình của Unity (gốc dưới-trái).</summary>
        private static Vector2 ToScreenPoint(Vector2 guiPosition)
        {
            return new Vector2(guiPosition.x, Screen.height - guiPosition.y);
        }

        /// <summary>Đổi điểm chuột trên màn hình sang toạ độ thiết kế 1920x1080 (gốc trên-trái).</summary>
        private Vector2 DesignPointerPosition(Vector2 screenPosition)
        {
            float scale = m_Canvas != null ? m_Canvas.scaleFactor : 1f;
            if (scale <= 0f)
            {
                scale = 1f;
            }

            return new Vector2(screenPosition.x / scale, (Screen.height - screenPosition.y) / scale);
        }

        private Vector2 PointerPosition
        {
            get
            {
                if (m_HasGuiPointer)
                {
                    return m_PointerScreen;
                }

#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
