using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using IronWasteland.Save;
using IronWasteland.Stages;
using IronWasteland.Tanks;
using IronWasteland.UiModels;

namespace IronWasteland.UI
{
    /// <summary>
    /// Điều khiển màn PHÂN RÃ PHỤ KIỆN. Màn dựng riêng, bố cục giống màn chọn phụ kiện
    /// nhưng thay vì trang bị thì đổi phụ kiện thành Ngọc: chọn một thẻ, chọn số lượng cần
    /// phân rã rồi bấm XÁC NHẬN PHÂN RÃ. Sau khi phân rã màn ở lại để người chơi làm tiếp.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AccessorySalvageController : MonoBehaviour
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

            [Tooltip("Nhóm badge và dấu tick, chỉ bật cho thẻ đang chọn.")]
            public GameObject SelectedGroup;

            [Tooltip("Nhãn trạng thái ở góc phải thẻ: LỰA CHỌN, ĐANG TRANG BỊ hoặc CHƯA SỞ HỮU.")]
            public TextMeshProUGUI ChoiceLabel;

            [Tooltip("Icon phụ kiện.")]
            public Image Icon;

            [Tooltip("Số thứ tự 01..20.")]
            public TextMeshProUGUI Number;

            [Tooltip("Nhóm phụ kiện.")]
            public TextMeshProUGUI Category;

            [Tooltip("Tên ngắn của phụ kiện.")]
            public TextMeshProUGUI Name;

            [Tooltip("Dòng vai trò dưới tên, kèm số bản sao đang có.")]
            public TextMeshProUGUI Role;

            [Tooltip("Các dòng chỉ số màu xanh.")]
            public TextMeshProUGUI Stats;

            [Tooltip("Mô tả hiệu ứng.")]
            public TextMeshProUGUI Description;

            [Tooltip("Chữ PHÂN RÃ ở chân thẻ, ẩn khi thẻ đang được chọn.")]
            public TextMeshProUGUI Footer;
        }

        /// <summary>Màu chữ của nút xác nhận khi bấm được.</summary>
        private static readonly Color ConfirmEnabledColor = new Color32(0x0B, 0x12, 0x18, 0xFF);

        /// <summary>Màu chữ của nút xác nhận khi bị khoá.</summary>
        private static readonly Color ConfirmDisabledColor = new Color32(0x6A, 0x76, 0x82, 0xFF);

        /// <summary>Độ mờ của thẻ khi người chơi không còn bản sao nào.</summary>
        private const float NotOwnedAlpha = 0.35f;

        [Header("Dữ liệu")]
        [Tooltip("Danh mục 20 phụ kiện dùng chung với Nhà xe.")]
        [SerializeField] private AccessoryCatalog m_Catalog;

        [Header("Tham chiếu giao diện")]
        [Tooltip("Dòng đường dẫn trên tiêu đề: <tên xe> / PHỤ KIỆN / NHÀ XE.")]
        [SerializeField] private TextMeshProUGUI m_Breadcrumb;

        [Tooltip("Dòng phụ đề TỔNG ... PHỤ KIỆN, cũng là chỗ hiện thông báo sau khi phân rã.")]
        [SerializeField] private TextMeshProUGUI m_Subtitle;

        [Tooltip("Số ngọc hiện có ở góc phải tiêu đề.")]
        [SerializeField] private TextMeshProUGUI m_GemsText;

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

        [Header("Ô số lượng")]
        [Tooltip("Chữ hiển thị số lượng đang chọn.")]
        [SerializeField] private TextMeshProUGUI m_QuantityText;

        [Tooltip("Dòng NHẬN: X NGỌC hoặc lý do không phân rã được.")]
        [SerializeField] private TextMeshProUGUI m_SalvageHint;

        [Tooltip("Chữ trên nút xác nhận, đổi thành XÁC NHẬN PHÂN RÃ ×N.")]
        [SerializeField] private TextMeshProUGUI m_ConfirmText;

        [Tooltip("Nền nút xác nhận, mờ đi khi không phân rã được.")]
        [SerializeField] private Image m_ConfirmButtonImage;

        [Tooltip("Chỗ hiện thông báo ngắn; để trống thì dùng luôn dòng phụ đề.")]
        [SerializeField] private TextMeshProUGUI m_ToastText;

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

        [Header("Vùng kéo và cuộn danh sách (gốc trên-trái, hệ 1920x1080)")]
        [Tooltip("Vùng nhìn thấy của danh sách, dùng để bắt đầu kéo và cuộn.")]
        [SerializeField] private Rect m_ViewportArea = new Rect(96f, 276f, 1728f, 596f);

        [Header("Scene đích")]
        [Tooltip("Scene Nhà xe để quay lại khi màn này được chạy riêng lẻ.")]
        [SerializeField] private string m_BackSceneName = "Garage";

        private Canvas m_Canvas;
        private int m_TankIndex;
        private int m_SelectedIndex;
        private int m_Quantity = 1;
        private int m_HoveredIndex = -1;
        private float m_Scroll;
        private float m_MaxScroll;
        private bool m_Dragging;
        private bool m_DragMoved;
        private float m_DragStartX;
        private float m_DragStartScroll;
        private Vector2 m_PointerScreen;
        private bool m_HasGuiPointer;
        private bool m_SuppressCardClick;
        private float m_ToastTimer;

        /// <summary>Mép trên gốc của khối mô tả trong thẻ, chốt ở lần đầu để không bị đẩy dần xuống.</summary>
        private float m_DescriptionBaseTop = float.NaN;
        private int m_GemsCache = -1;

        /// <summary>Chỉ số phụ kiện đang chọn để phân rã.</summary>
        public int SelectedIndex => m_SelectedIndex;

        /// <summary>Số lượng đang chọn để phân rã.</summary>
        public int Quantity => m_Quantity;

        private void Awake()
        {
            m_Canvas = GetComponent<Canvas>();
            if (m_Canvas == null)
            {
                m_Canvas = GetComponentInParent<Canvas>();
            }

            m_TankIndex = Mathf.Clamp(SaveSystem.Instance.LastSelectedTankIndex, 0, SkillCatalog.TankCount - 1);
            if (m_Catalog == null || m_Catalog.Count <= 0)
            {
                Debug.LogError("[AccessorySalvageController] Danh mục phụ kiện rỗng, không thể dựng thẻ.");
                return;
            }

            m_SelectedIndex = PickInitialIndex();
            m_Quantity = 1;
            RefreshHeader();
            RefreshCards();
            RefreshQuantity();
            RefreshGems();
            SetScroll(0f, true);
            EnsureVisible(m_SelectedIndex);
        }

        private void Update()
        {
            UpdateHover(DesignPointerPosition(PointerPosition));
            UpdateToast();
            RefreshGems();
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
                        m_SuppressCardClick = false;
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
                            m_SuppressCardClick = true;
                        }

                        SetScroll(m_DragStartScroll - delta, true);
                    }

                    break;
                case EventType.MouseUp:
                    if (current.button == 0)
                    {
                        m_PointerScreen = ToScreenPoint(current.mousePosition);
                        m_HasGuiPointer = true;
                        m_Dragging = false;
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
                        ConfirmSalvage();
                    }
                    else if (current.keyCode == KeyCode.LeftArrow)
                    {
                        ChangeQuantity(-1);
                    }
                    else if (current.keyCode == KeyCode.RightArrow)
                    {
                        ChangeQuantity(1);
                    }
                    else if (current.keyCode == KeyCode.UpArrow)
                    {
                        SelectRelative(-1);
                    }
                    else if (current.keyCode == KeyCode.DownArrow)
                    {
                        SelectRelative(1);
                    }
                    else if (current.keyCode == KeyCode.M)
                    {
                        SelectMaxQuantity();
                    }

                    break;
            }
        }

        /// <summary>
        /// Chọn sẵn thẻ đầu tiên còn phân rã được (còn tồn kho và không xe nào trang bị)
        /// để người chơi mở màn là bấm được ngay.
        /// </summary>
        private int PickInitialIndex()
        {
            for (int i = 0; i < m_Catalog.Count; i++)
            {
                AccessoryCatalog.Entry entry = m_Catalog.Get(i);
                if (entry != null && SaveSystem.Instance.GetAccessoryQuantity(i) > 0)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>Cập nhật đường dẫn và phụ đề theo xe đang chọn.</summary>
        private void RefreshHeader()
        {
            if (m_Breadcrumb != null)
            {
                m_Breadcrumb.text = SkillCatalog.GetTankName(m_TankIndex) + " / PHỤ KIỆN / NHÀ XE";
            }

            if (m_Subtitle != null && m_ToastTimer <= 0f)
            {
                m_Subtitle.text = BuildSubtitle();
            }
        }

        /// <summary>Chuỗi phụ đề mặc định của màn.</summary>
        private string BuildSubtitle()
        {
            int count = m_Catalog != null ? m_Catalog.Count : 0;
            return "TỔNG " + count.ToString("00") + " PHỤ KIỆN  ·  MỖI LẦN CHỈ PHÂN RÃ 1 LOẠI";
        }

        /// <summary>Cập nhật số ngọc hiển thị ở góc phải, chỉ ghi khi giá trị đổi.</summary>
        private void RefreshGems()
        {
            int gems = SaveSystem.Instance.Gems;
            if (gems == m_GemsCache)
            {
                return;
            }

            m_GemsCache = gems;
            SetText(m_GemsText, "NGỌC: " + gems);
        }

        /// <summary>Đổ dữ liệu danh mục và trạng thái tồn kho vào 20 thẻ.</summary>
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

                int owned = SaveSystem.Instance.GetAccessoryQuantity(i);
                // Chỉ món thực sự còn trong kho mới tính là đang trang bị.
                bool equipped = owned > 0 && IsEquipped(i);
                bool selected = i == m_SelectedIndex;
                float alpha = owned > 0 ? 1f : NotOwnedAlpha;

                SetText(card.Number, (i + 1).ToString("00"));
                SetText(card.Category, entry.Category);
                SetText(card.Name, entry.DisplayName);
                SetText(card.Role, entry.Role + "  ·  ×" + owned);
                SetText(card.Stats, BuildStats(entry.Stats));
                SetText(card.Description, entry.Description);
                LayoutCardText(card);
                SetText(card.Footer, "PHÂN RÃ  ›");
                SetText(card.ChoiceLabel, equipped ? "ĐANG TRANG BỊ" : owned > 0 ? "LỰA CHỌN" : SalvageQuoteModel.NotOwnedHint);

                SetAlpha(card.Number, alpha);
                SetAlpha(card.Category, alpha);
                SetAlpha(card.Name, alpha);
                SetAlpha(card.Role, alpha);
                SetAlpha(card.Stats, alpha);
                SetAlpha(card.Description, alpha);
                SetAlpha(card.ChoiceLabel, alpha);

                if (card.Icon != null)
                {
                    card.Icon.sprite = entry.Icon;
                    card.Icon.enabled = entry.Icon != null;
                    Color iconColor = card.Icon.color;
                    iconColor.a = alpha;
                    card.Icon.color = iconColor;
                }

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
                    card.Footer.gameObject.SetActive(!selected && owned > 0);
                }

                if (card.Hover != null)
                {
                    card.Hover.enabled = false;
                }
            }

            m_HoveredIndex = -1;
        }

        /// <summary>Giữ khối mô tả ngay dưới khối chỉ số, chỉ đẩy xuống khi chỉ số chiếm nhiều dòng.</summary>
        private void LayoutCardText(CardView card)
        {
            if (card.Stats == null || card.Description == null)
            {
                return;
            }

            RectTransform statsRect = card.Stats.rectTransform;
            RectTransform descriptionRect = card.Description.rectTransform;
            if (float.IsNaN(m_DescriptionBaseTop))
            {
                // Chốt mép trên gốc do builder đặt để các lần làm mới sau không trôi dần xuống.
                m_DescriptionBaseTop = -descriptionRect.anchoredPosition.y;
            }

            card.Stats.ForceMeshUpdate();
            float statsHeight = Mathf.Max(card.Stats.fontSize * 1.3f, card.Stats.preferredHeight);
            float descriptionTop = Mathf.Max(m_DescriptionBaseTop, -statsRect.anchoredPosition.y + statsHeight + 10f);
            float descriptionHeight = Mathf.Max(48f, 566f - descriptionTop);

            card.Description.overflowMode = TextOverflowModes.Ellipsis;
            descriptionRect.anchoredPosition = new Vector2(descriptionRect.anchoredPosition.x, -descriptionTop);
            descriptionRect.sizeDelta = new Vector2(descriptionRect.sizeDelta.x, descriptionHeight);
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

        /// <summary>Cập nhật ô số lượng, dòng NHẬN và chữ trên nút xác nhận.</summary>
        private void RefreshQuantity()
        {
            AccessoryCatalog.Entry entry = CurrentEntry();
            if (entry == null)
            {
                return;
            }

            int owned = SaveSystem.Instance.GetAccessoryQuantity(m_SelectedIndex);
            bool equipped = owned > 0 && IsEquipped(m_SelectedIndex);
            SalvageQuote quote = SalvageQuoteModel.Build(owned, entry.SalvageValue, m_Quantity, equipped);
            m_Quantity = Mathf.Max(1, quote.Quantity);

            SetText(m_QuantityText, m_Quantity.ToString());
            SetText(m_SalvageHint, quote.Hint);
            if (m_SalvageHint != null)
            {
                m_SalvageHint.color = quote.CanSalvage ? new Color32(0xFF, 0xD5, 0x4F, 0xFF) : new Color32(0x8C, 0x9A, 0xA8, 0xFF);
            }

            SetText(m_ConfirmText, quote.CanSalvage ? "XÁC NHẬN PHÂN RÃ  ×" + quote.Quantity : "XÁC NHẬN PHÂN RÃ");
            if (m_ConfirmText != null)
            {
                m_ConfirmText.color = quote.CanSalvage ? ConfirmEnabledColor : ConfirmDisabledColor;
            }

            if (m_ConfirmButtonImage != null)
            {
                m_ConfirmButtonImage.color = quote.CanSalvage
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.42f);
            }
        }

        /// <summary>Phụ kiện đang được bất kỳ xe nào trang bị hay không.</summary>
        private bool IsEquipped(int index)
        {
            for (int tank = 0; tank < SkillCatalog.TankCount; tank++)
            {
                if (SaveSystem.Instance.GetEquippedAccessory(tank) == index)
                {
                    return true;
                }
            }

            return false;
        }

        private AccessoryCatalog.Entry CurrentEntry()
        {
            return m_Catalog != null ? m_Catalog.Get(m_SelectedIndex) : null;
        }

        /// <summary>Chọn một thẻ phụ kiện để phân rã, đưa số lượng về 1.</summary>
        private void Select(int index)
        {
            if (m_Catalog == null || index < 0 || index >= m_Catalog.Count)
            {
                return;
            }

            m_SelectedIndex = index;
            m_Quantity = 1;
            RefreshCards();
            RefreshQuantity();
            EnsureVisible(m_SelectedIndex);
        }

        /// <summary>Chọn thẻ kế bên bằng phím mũi tên lên xuống.</summary>
        private void SelectRelative(int delta)
        {
            if (m_Catalog == null || m_Catalog.Count == 0)
            {
                return;
            }

            Select(Mathf.Clamp(m_SelectedIndex + delta, 0, m_Catalog.Count - 1));
        }

        /// <summary>Đổi số lượng cần phân rã, kẹp theo tồn kho và giới hạn 99.</summary>
        private void ChangeQuantity(int delta)
        {
            AccessoryCatalog.Entry entry = CurrentEntry();
            if (entry == null || IsEquipped(m_SelectedIndex))
            {
                return;
            }

            int owned = SaveSystem.Instance.GetAccessoryQuantity(m_SelectedIndex);
            int max = Mathf.Min(owned, SalvageQuoteModel.MaxQuantity);
            if (max < 1)
            {
                return;
            }

            m_Quantity = Mathf.Clamp(m_Quantity + delta, 1, max);
            RefreshQuantity();
        }

        /// <summary>Đưa số lượng về toàn bộ tồn kho (nút MAX).</summary>
        private void SelectMaxQuantity()
        {
            AccessoryCatalog.Entry entry = CurrentEntry();
            if (entry == null || IsEquipped(m_SelectedIndex))
            {
                return;
            }

            int owned = SaveSystem.Instance.GetAccessoryQuantity(m_SelectedIndex);
            m_Quantity = Mathf.Clamp(owned, 1, SalvageQuoteModel.MaxQuantity);
            RefreshQuantity();
        }

        /// <summary>Trừ tồn kho, cộng ngọc rồi làm mới màn; giữ nguyên màn để phân rã tiếp.</summary>
        private void ConfirmSalvage()
        {
            AccessoryCatalog.Entry entry = CurrentEntry();
            if (entry == null)
            {
                return;
            }

            int owned = SaveSystem.Instance.GetAccessoryQuantity(m_SelectedIndex);
            bool equipped = owned > 0 && IsEquipped(m_SelectedIndex);
            SalvageQuote quote = SalvageQuoteModel.Build(owned, entry.SalvageValue, m_Quantity, equipped);
            if (!quote.CanSalvage || !SaveSystem.Instance.ConsumeAccessory(m_SelectedIndex, quote.Quantity))
            {
                return;
            }

            int totalGems = 0;
            for (int i = 0; i < quote.Quantity; i++)
            {
                totalGems += UnityEngine.Random.Range(SalvageQuoteModel.MinGemsPerAccessory, SalvageQuoteModel.MaxGemsPerAccessory + 1);
            }

            SaveSystem.Instance.AddGems(totalGems);
            ShowToast("ĐÃ PHÂN RÃ ×" + quote.Quantity + "  ·  +" + totalGems + " NGỌC");
            m_Quantity = 1;
            RefreshGems();
            RefreshCards();
            RefreshQuantity();
        }

        /// <summary>Hiện thông báo ngắn rồi tự trả dòng phụ đề về mặc định.</summary>
        private void ShowToast(string message)
        {
            m_ToastTimer = 2.5f;
            if (m_ToastText != null)
            {
                SetText(m_ToastText, message);
                return;
            }

            SetText(m_Subtitle, message);
        }

        private void UpdateToast()
        {
            if (m_ToastTimer <= 0f)
            {
                return;
            }

            m_ToastTimer -= Time.unscaledDeltaTime;
            if (m_ToastTimer > 0f)
            {
                return;
            }

            m_ToastTimer = 0f;
            if (m_ToastText != null)
            {
                SetText(m_ToastText, string.Empty);
            }

            if (m_Subtitle != null)
            {
                m_Subtitle.text = BuildSubtitle();
            }
        }

        /// <summary>Nút X ở góc trên phải gọi vào đây.</summary>
        public void PressClose()
        {
            Close();
        }

        /// <summary>Nút HỦY BỎ gọi vào đây.</summary>
        public void PressCancel()
        {
            Close();
        }

        /// <summary>Nút XÁC NHẬN PHÂN RÃ gọi vào đây.</summary>
        public void PressConfirm()
        {
            ConfirmSalvage();
        }

        /// <summary>Nút trừ số lượng gọi vào đây.</summary>
        public void PressMinus()
        {
            ChangeQuantity(-1);
        }

        /// <summary>Nút cộng số lượng gọi vào đây.</summary>
        public void PressPlus()
        {
            ChangeQuantity(1);
        }

        /// <summary>Nút MAX gọi vào đây.</summary>
        public void PressMax()
        {
            SelectMaxQuantity();
        }

        /// <summary>Thẻ phụ kiện gọi vào đây kèm chỉ số; bỏ qua nếu vừa kéo danh sách.</summary>
        public void SelectCard(int index)
        {
            if (m_SuppressCardClick)
            {
                return;
            }

            Select(index);
        }

        /// <summary>Đóng màn phân rã, quay về Nhà xe.</summary>
        private void Close()
        {
            if (SceneManager.sceneCount > 1)
            {
                SceneManager.UnloadSceneAsync(gameObject.scene);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(m_BackSceneName))
            {
                Debug.LogError("[AccessorySalvageController] Không tải được scene " + m_BackSceneName);
                return;
            }

            SceneManager.LoadScene(m_BackSceneName);
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
                    m_Cards[i].Hover.enabled = i == m_HoveredIndex && i != m_SelectedIndex;
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

        private static void SetAlpha(TextMeshProUGUI target, float alpha)
        {
            if (target != null)
            {
                target.alpha = alpha;
            }
        }
    }
}