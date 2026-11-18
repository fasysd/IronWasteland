using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Hàm dựng uGUI bằng code cho các màn hình Menu.
    ///
    /// Quy ước tọa độ: mọi màn hình thiết kế trên khung 1920x1080, gốc ở GÓC TRÊN TRÁI,
    /// y tăng xuống dưới. <see cref="Place"/> đặt một RectTransform theo (x, y, rộng, cao)
    /// tính từ góc trên trái của cha. Nhờ vậy số liệu trong code đọc thẳng được từ mockup.
    /// </summary>
    public static class UIKit
    {
        // ============================================================
        // SPRITE SINH TẠI CHỖ (vòng tròn / hình tròn)
        // ============================================================

        private static Sprite s_Ring;
        private static Sprite s_Circle;

        /// <summary>Vòng tròn rỗng (dùng cho Image kiểu Filled Radial360).</summary>
        public static Sprite RingSprite => s_Ring != null ? s_Ring : (s_Ring = MakeCircleSprite(256, 0.9f));

        /// <summary>Hình tròn đặc.</summary>
        public static Sprite CircleSprite => s_Circle != null ? s_Circle : (s_Circle = MakeCircleSprite(128, 0f));

        private static Sprite MakeCircleSprite(int size, float innerRatio)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float outer = size * 0.5f - 1f;
            float inner = outer * innerRatio;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = Mathf.Clamp01(outer - d + 0.5f);
                    if (innerRatio > 0f) a *= Mathf.Clamp01(d - inner + 0.5f);

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // ============================================================
        // RECT
        // ============================================================

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Đặt theo (x, y, w, h) tính từ góc TRÊN TRÁI của cha.</summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.Destroy(t.GetChild(i).gameObject);
        }

        public static string Pad2(int n) => n.ToString("00");

        // ============================================================
        // PANEL
        // ============================================================

        /// <summary>
        /// Khối màu (có thể có viền). Viền dùng Outline nên màu nền nên là màu ĐẶC (alpha = 1),
        /// nếu không viền sẽ lộ xuyên qua nền.
        /// </summary>
        public static Image PanelAt(
            Transform parent, string name, float x, float y, float w, float h,
            Color fill, Color? border = null, float borderSize = 1f, bool raycast = false)
        {
            RectTransform rt = NewRect(name, parent);
            Place(rt, x, y, w, h);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = fill;
            img.raycastTarget = raycast;

            if (border.HasValue)
            {
                var outline = rt.gameObject.AddComponent<Outline>();
                outline.effectColor = border.Value;
                outline.effectDistance = new Vector2(borderSize, borderSize);
                outline.useGraphicAlpha = true;
            }

            return img;
        }

        /// <summary>Đổi màu viền của một panel đã tạo bằng <see cref="PanelAt"/> (có viền).</summary>
        public static void SetBorder(Image panel, Color color)
        {
            if (panel == null) return;
            var outline = panel.GetComponent<Outline>();
            if (outline != null) outline.effectColor = color;
        }

        /// <summary>Bốn góc chữ L (khung kiểu HUD) bám theo 4 góc của cha.</summary>
        public static void Brackets(Transform parent, Color color, float length = 24f, float thickness = 2f, float inset = 0f)
        {
            for (int cx = 0; cx < 2; cx++)
            {
                for (int cy = 0; cy < 2; cy++)
                {
                    var corner = new Vector2(cx, cy);
                    var offset = new Vector2(cx == 0 ? inset : -inset, cy == 0 ? inset : -inset);
                    CornerBar(parent, corner, offset, new Vector2(length, thickness), color);
                    CornerBar(parent, corner, offset, new Vector2(thickness, length), color);
                }
            }
        }

        private static void CornerBar(Transform parent, Vector2 corner, Vector2 offset, Vector2 size, Color color)
        {
            RectTransform rt = NewRect("Bracket", parent);
            rt.anchorMin = corner;
            rt.anchorMax = corner;
            rt.pivot = corner;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;

            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        // ============================================================
        // CHỮ
        // ============================================================

        public static TextMeshProUGUI Label(
            Transform parent, string text, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left,
            FontStyles style = FontStyles.Normal,
            bool wrap = false, bool autoFit = false, bool display = false)
        {
            RectTransform rt = NewRect("Label", parent);
            Place(rt, x, y, w, h);

            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            // display = true: font tiêu đề (Anton). Anton chỉ có một độ đậm nên bỏ qua FontStyles.Bold.
            TMP_FontAsset font = display && UITheme.DisplayFont != null ? UITheme.DisplayFont : UITheme.Font;
            if (font != null) t.font = font;

            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = display ? FontStyles.Normal : style;
            t.raycastTarget = false;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            // KHÔNG dùng Ellipsis/Truncate: nếu chiều cao dòng của font (Anton rất cao) lớn hơn khung thì TMP cắt mất
            // cả dòng, nhãn biến mất hoàn toàn. Chữ dài thì dùng autoFit để co lại.
            t.overflowMode = TextOverflowModes.Overflow;

            if (autoFit)
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = Mathf.Max(8f, size * 0.45f);
                t.fontSizeMax = size;
            }

            return t;
        }

        /// <summary>Thẻ nhỏ có viền màu + chữ cùng màu (DPS, SUPPORT, ĐANG CHỌN...). Trả về label.</summary>
        public static TextMeshProUGUI ChipAt(
            Transform parent, string text, float x, float y, float w, float h, Color color, float size = 12f)
        {
            Image panel = PanelAt(parent, "Chip", x, y, w, h, Color.Lerp(UITheme.PanelDark, color, 0.14f), color, 1f);
            TextMeshProUGUI label = Label(panel.transform, text, 0, 0, w, h, size, color,
                TextAlignmentOptions.Center, FontStyles.Bold, false, true);
            Stretch(label.rectTransform, 4f, 0f, 4f, 0f);
            return label;
        }

        // ============================================================
        // NÚT
        // ============================================================

        public static Button Btn(
            Transform parent, string name, float x, float y, float w, float h,
            ButtonLook look, UnityAction onClick)
        {
            RectTransform rt = NewRect(name, parent);
            Place(rt, x, y, w, h);

            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = true;

            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, 1.5f);
            outline.useGraphicAlpha = true;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var fx = rt.gameObject.AddComponent<UIButtonFx>();
            fx.SetLook(look);

            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>Nút có một dòng chữ căn giữa.</summary>
        public static Button TextBtn(
            Transform parent, string label, float x, float y, float w, float h,
            ButtonLook look, float size, UnityAction onClick, FontStyles style = FontStyles.Bold)
        {
            Button b = Btn(parent, "Btn_" + label, x, y, w, h, look, onClick);
            TextMeshProUGUI t = Label(b.transform, label, 0, 0, w, h, size, look.Text,
                TextAlignmentOptions.Center, style, false, true);
            Stretch(t.rectTransform, 8f, 0f, 8f, 0f);
            return b;
        }

        /// <summary>Đổi kiểu nút. Nếu <paramref name="updateLabel"/> thì đổi luôn màu chữ đầu tiên của nút.</summary>
        public static void SetLook(Button button, ButtonLook look, bool updateLabel = false)
        {
            if (button == null) return;

            var fx = button.GetComponent<UIButtonFx>();
            if (fx != null) fx.SetLook(look);

            if (updateLabel)
            {
                var t = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (t != null) t.color = look.Text;
            }
        }

        /// <summary>Bật/tắt nút (nền xám + chữ mờ).</summary>
        public static void SetEnabled(Button button, bool enabled)
        {
            if (button == null) return;

            button.interactable = enabled;

            var fx = button.GetComponent<UIButtonFx>();
            if (fx != null) fx.Refresh();

            var t = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null) t.alpha = enabled ? 1f : 0.45f;
        }

        // ============================================================
        // THANH TIẾN ĐỘ
        // ============================================================

        /// <summary>Thanh ngang. Trả về Image "fill"; đặt tỉ lệ bằng <see cref="SetBar"/>.</summary>
        public static Image BarAt(Transform parent, float x, float y, float w, float h, Color track, Color fill)
        {
            Image trackImg = PanelAt(parent, "Bar", x, y, w, h, track);

            RectTransform rt = NewRect("Fill", trackImg.transform);
            var fillImg = rt.gameObject.AddComponent<Image>();
            fillImg.color = fill;
            fillImg.raycastTarget = false;

            SetBar(fillImg, 0f);
            return fillImg;
        }

        public static void SetBar(Image fill, float t)
        {
            if (fill == null) return;

            RectTransform rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ============================================================
        // SLIDER
        // ============================================================

        /// <summary>Slider ngang 0..1. Toàn bộ vùng slider đều bấm được.</summary>
        public static Slider SliderAt(
            Transform parent, string name, float x, float y, float w, float h,
            Color fillColor, UnityAction<float> onChanged)
        {
            RectTransform root = NewRect(name, parent);
            Place(root, x, y, w, h);

            // Vùng bắt chuột trong suốt phủ cả slider.
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            RectTransform bg = NewRect("Background", root);
            bg.anchorMin = new Vector2(0f, 0.5f);
            bg.anchorMax = new Vector2(1f, 0.5f);
            bg.offsetMin = new Vector2(0f, -3f);
            bg.offsetMax = new Vector2(0f, 3f);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = UITheme.PanelLight;
            bgImg.raycastTarget = false;

            RectTransform fillArea = NewRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(0f, -3f);
            fillArea.offsetMax = new Vector2(0f, 3f);

            RectTransform fill = NewRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;

            RectTransform handleArea = NewRect("Handle Slide Area", root);
            Stretch(handleArea, 8f, 0f, 8f, 0f);

            RectTransform handle = NewRect("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(16f, 0f);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = Color.white;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(0f);

            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        // ============================================================
        // CUỘN
        // ============================================================

        /// <summary>
        /// Vùng cuộn có cắt (RectMask2D). <paramref name="content"/> có gốc ở góc trên trái;
        /// đặt các phần tử con bằng <see cref="Place"/> như bình thường.
        /// </summary>
        public static ScrollRect ScrollAt(
            Transform parent, string name, float x, float y, float w, float h,
            bool horizontal, float contentSize, out RectTransform content)
        {
            RectTransform root = NewRect(name, parent);
            Place(root, x, y, w, h);

            var scroll = root.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = NewRect("Viewport", root);
            Stretch(viewport);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0f); // để kéo được cả khi trỏ vào chỗ trống
            viewport.gameObject.AddComponent<RectMask2D>();

            content = NewRect("Content", viewport);
            if (horizontal)
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 1f);
                content.sizeDelta = new Vector2(contentSize, 0f);
            }
            else
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0f, 1f);
                content.sizeDelta = new Vector2(0f, contentSize);
            }
            content.anchoredPosition = Vector2.zero;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            scroll.inertia = true;
            return scroll;
        }

        // ============================================================
        // ICON
        // ============================================================

        /// <summary>Hình sprite đơn sắc (vd <see cref="UIIcons"/>) tô màu bằng <paramref name="color"/>.</summary>
        public static Image SpriteAt(Transform parent, string name, Sprite sprite, float x, float y, float w, float h, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            Place(rt, x, y, w, h);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Ô icon. Chưa có sprite thì hiện một ô xám với chữ cái đầu (xem <see cref="SetIcon"/>).</summary>
        public static Image IconAt(Transform parent, string name, float x, float y, float w, float h)
        {
            RectTransform rt = NewRect(name, parent);
            Place(rt, x, y, w, h);

            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = UITheme.PanelLight;

            TextMeshProUGUI fallback = Label(rt, "?", 0, 0, w, h, Mathf.Max(12f, h * 0.45f),
                UITheme.TextFaint, TextAlignmentOptions.Center, FontStyles.Bold);
            fallback.gameObject.name = "Fallback";
            Stretch(fallback.rectTransform);
            return img;
        }

        public static void SetIcon(Image icon, Sprite sprite, string fallbackText)
        {
            if (icon == null) return;

            TextMeshProUGUI fallback = icon.transform.childCount > 0
                ? icon.transform.GetChild(0).GetComponent<TextMeshProUGUI>()
                : null;

            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.color = Color.white;
                if (fallback != null) fallback.gameObject.SetActive(false);
            }
            else
            {
                icon.sprite = null;
                icon.color = UITheme.PanelLight;
                if (fallback != null)
                {
                    fallback.gameObject.SetActive(true);
                    fallback.text = string.IsNullOrEmpty(fallbackText) ? "?" : fallbackText.Substring(0, 1).ToUpperInvariant();
                }
            }
        }
    }
}
