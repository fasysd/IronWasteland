using TMPro;
using UnityEngine;

namespace IronWasteland.Menu
{
    /// <summary>
    /// Bảng màu + font của UI Menu (bám theo mockup: nền tối, nhấn cam / xanh ngọc / tím).
    /// Đổi giao diện ở đây là đổi cho mọi màn hình.
    /// </summary>
    public static class UITheme
    {
        /// <summary>
        /// Font dùng cho toàn bộ UI. BẮT BUỘC dùng font có đủ ký tự tiếng Việt (xem README).
        /// MenuBootstrap gán giá trị này lúc khởi động; để trống sẽ dùng font mặc định của TMP.
        /// </summary>
        public static TMP_FontAsset Font;

        /// <summary>
        /// Font chữ TIÊU ĐỀ (theo mockup Figma: Anton Regular) cho tiêu đề trang, tên xe lớn, số lớn, "ẢI 1-1".
        /// Chữ còn lại dùng <see cref="Font"/> (Barlow Condensed). Để trống sẽ dùng <see cref="Font"/>.
        /// </summary>
        public static TMP_FontAsset DisplayFont;

        public static readonly Color Bg = Hex("#06070C");
        public static readonly Color PanelDark = Hex("#0A0E19");
        public static readonly Color Panel = Hex("#0F1424");
        public static readonly Color PanelLight = Hex("#171E35");
        public static readonly Color Border = Hex("#262F4D");
        public static readonly Color BorderBright = Hex("#3A4672");

        public static readonly Color Orange = Hex("#FF8A1F");
        public static readonly Color OrangeHi = Hex("#FFA54D");
        public static readonly Color OrangeLo = Hex("#CC6C12");
        public static readonly Color Teal = Hex("#28E0CC");
        public static readonly Color Purple = Hex("#8F5CFF");
        public static readonly Color Green = Hex("#3BE08A");
        public static readonly Color Red = Hex("#FF4D5E");
        public static readonly Color Blue = Hex("#5B8CFF");
        public static readonly Color Gold = Hex("#FFC857");

        public static readonly Color Text = Hex("#F2F4FA");
        public static readonly Color TextDim = Hex("#8A93AD");
        public static readonly Color TextFaint = Hex("#566080");
        public static readonly Color TextOnOrange = Hex("#1A0E00");

        public static Color Hex(string hex, float alpha = 1f)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color c)) c = Color.magenta;
            c.a = alpha;
            return c;
        }

        public static Color Alpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }

    /// <summary>Bộ màu của một nút theo từng trạng thái.</summary>
    public sealed class ButtonLook
    {
        public readonly Color Normal;
        public readonly Color Hover;
        public readonly Color Pressed;
        public readonly Color Disabled;
        public readonly Color Border;
        public readonly Color HoverBorder;
        public readonly Color Text;

        public ButtonLook(Color normal, Color hover, Color pressed, Color disabled, Color border, Color text, Color? hoverBorder = null)
        {
            Normal = normal;
            Hover = hover;
            Pressed = pressed;
            Disabled = disabled;
            Border = border;
            HoverBorder = hoverBorder ?? border;
            Text = text;
        }
    }

    /// <summary>Các kiểu nút dựng sẵn.</summary>
    public static class Looks
    {
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        public static readonly ButtonLook Primary = new ButtonLook(
            UITheme.Orange, UITheme.OrangeHi, UITheme.OrangeLo,
            UITheme.Hex("#3A2B1B"), UITheme.Orange, UITheme.TextOnOrange);

        public static readonly ButtonLook Secondary = new ButtonLook(
            UITheme.PanelLight, UITheme.Hex("#1F2845"), UITheme.Hex("#10162B"),
            UITheme.Hex("#0C101C"), UITheme.Border, UITheme.Text);

        /// <summary>Nút menu chính: nền tối, chỉ khi rê chuột vào mới chuyển sang cam.</summary>
        public static readonly ButtonLook MenuItem = new ButtonLook(
            UITheme.Hex("#101627"), UITheme.Orange, UITheme.OrangeLo,
            UITheme.Hex("#0C101C"), UITheme.Border, UITheme.Text, UITheme.Orange);

        public static readonly ButtonLook Danger = new ButtonLook(
            UITheme.Hex("#1B1117"), UITheme.Hex("#2A141C"), UITheme.Hex("#12090D"),
            UITheme.Hex("#0C0A0D"), UITheme.Hex("#5A2430"), UITheme.Red);

        public static readonly ButtonLook Ghost = new ButtonLook(
            Clear, UITheme.Hex("#FFFFFF", 0.08f), UITheme.Hex("#FFFFFF", 0.14f),
            Clear, Clear, UITheme.TextDim);

        public static readonly ButtonLook Card = new ButtonLook(
            UITheme.Panel, UITheme.Hex("#151C33"), UITheme.Hex("#0C1020"),
            UITheme.Hex("#0A0D18"), UITheme.Border, UITheme.Text);

        public static readonly ButtonLook CardSelected = new ButtonLook(
            UITheme.Hex("#17142B"), UITheme.Hex("#1D1936"), UITheme.Hex("#100E22"),
            UITheme.Hex("#0A0D18"), UITheme.Purple, UITheme.Text);

        public static readonly ButtonLook CardActive = new ButtonLook(
            UITheme.Hex("#241A12"), UITheme.Hex("#2E2016"), UITheme.Hex("#1A120C"),
            UITheme.Hex("#0A0D18"), UITheme.Orange, UITheme.Text);

        public static readonly ButtonLook CardLocked = new ButtonLook(
            UITheme.Hex("#0B0E18"), UITheme.Hex("#10141F"), UITheme.Hex("#090B14"),
            UITheme.Hex("#090B14"), UITheme.Hex("#1B2238"), UITheme.TextFaint);

        public static readonly ButtonLook TabIdle = new ButtonLook(
            UITheme.Hex("#0C101C"), UITheme.Hex("#141A2E"), UITheme.Hex("#090C16"),
            UITheme.Hex("#090C16"), UITheme.Border, UITheme.TextDim);

        public static readonly ButtonLook TabActive = new ButtonLook(
            UITheme.Orange, UITheme.OrangeHi, UITheme.OrangeLo,
            UITheme.OrangeLo, UITheme.Orange, UITheme.TextOnOrange);
    }
}
