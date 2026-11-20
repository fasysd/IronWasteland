using System;

namespace IronWasteland.UI
{
    /// <summary>
    /// Danh mục 36 lựa chọn kỹ năng tùy chọn (6 xe × 2 tab × 3 lựa chọn).
    /// Số liệu năng lượng, hồi chiêu và mô tả lấy từ các lớp kỹ năng thật trong
    /// Assets/Scripts/Tanks; phần hiển thị trong Garage và Picker đều đọc từ đây
    /// nên chỉ cần sửa một nơi khi cân bằng lại kỹ năng.
    /// </summary>
    public static class SkillCatalog
    {
        /// <summary>Số xe tăng đang có trong màn Garage.</summary>
        public const int TankCount = 6;

        /// <summary>Số tab lựa chọn: tab 0 là TÙY CHỌN 1, tab 1 là TÙY CHỌN 2.</summary>
        public const int TabCount = 2;

        /// <summary>Mỗi tab có 3 lựa chọn kỹ năng.</summary>
        public const int OptionCount = 3;

        /// <summary>Một lựa chọn kỹ năng hiển thị trong màn Picker.</summary>
        [Serializable]
        public sealed class Option
        {
            public string Name;
            public string Role;
            public int Energy;
            public float Cooldown;
            public string Description;

            public Option(string name, string role, int energy, float cooldown, string description)
            {
                Name = name;
                Role = role;
                Energy = energy;
                Cooldown = cooldown;
                Description = description;
            }
        }

        private static readonly string[] TankNames =
        {
            "WALL TITAN",
            "RED NOVA",
            "BLACK SHADOW",
            "LEVIATHAN",
            "WATER B",
            "GREEN FOX"
        };

        private static readonly string[] TankKeys =
        {
            "wall_titan",
            "red_nova",
            "black_shadow",
            "leviathan",
            "water_b",
            "green_fox"
        };

        /// <summary>36 lựa chọn, xếp theo thứ tự: xe × 6 + tab × 3 + lựa chọn.</summary>
        private static readonly Option[][] Options =
        {
            // WALL TITAN - TÙY CHỌN 1
            new[]
            {
                new Option("Stone Fortress", "PHÒNG THỦ  /  CHỐNG CHỊU", 50, 12f, "Hóa đá, nhận khiên 30% Máu tối đa trong 8s. Tăng 50% Giáp nhưng giảm 30% Tốc độ."),
                new Option("Guardian Wall", "HỖ TRỢ  /  PHÒNG THỦ", 50, 10f, "Dựng tường đá cho đồng minh: khiên 30% Máu trong 5s, chuyển 35% sát thương nhận vào sang bản thân."),
                new Option("Earth Prison", "KHỐNG CHẾ  /  SÁT THƯƠNG", 60, 14f, "Giam địch trong 4s. Khi địch bị giam, bản thân được tăng 100% Tấn công và 150% Giáp.")
            },
            // WALL TITAN - TÙY CHỌN 2
            new[]
            {
                new Option("Immovable Titan", "PHÒNG THỦ  /  CHỐNG CHỊU", 0, 10f, "Hồi 40 Năng lượng, tăng 50 Giáp và miễn mọi hiệu ứng khống chế trong 5s."),
                new Option("Earth Guardian", "HỖ TRỢ  /  PHÒNG THỦ", 50, 10f, "Triệu hồi hộ vệ đá: tăng 35 Giáp, chuyển 35% sát thương đồng minh nhận vào sang bản thân trong 5s."),
                new Option("Earthquake", "SÁT THƯƠNG  /  KHỐNG CHẾ", 65, 14f, "Chấn địa 6s, mỗi 0.5s gây 60% Tấn công và 100% Giáp; địch bị giảm 35% Tốc độ.")
            },

            // RED NOVA - TÙY CHỌN 1
            new[]
            {
                new Option("Overdrive", "CƠ ĐỘNG  /  CƯỜNG HÓA", 20, 1f, "Tăng 35% Tốc độ trong 1s và 20% tốc độ bắn cho đòn kế tiếp."),
                new Option("Laser Cannon", "SÁT THƯƠNG  /  CHÍNH XÁC", 5, 1f, "Đòn kế tiếp bắn tia laser 150% Tấn công, xuyên qua toàn bộ mục tiêu trên đường."),
                new Option("Execution Core", "SÁT THƯƠNG  /  XUYÊN GIÁP", 45, 10f, "Lõi hành quyết: tăng 55% tốc độ bắn và 30% xuyên giáp trong 10s.")
            },
            // RED NOVA - TÙY CHỌN 2
            new[]
            {
                new Option("Nova Barrage", "SÁT THƯƠNG  /  DIỆN RỘNG", 65, 12f, "Bắn phủ đầu 3s, mỗi 0.5s gây 75% Tấn công lên mọi địch trong vùng."),
                new Option("Nova Pierce", "SÁT THƯƠNG  /  XUYÊN GIÁP", 55, 12f, "Trong 5s: tăng 50% Tấn công, 25% tốc độ bắn và 75% xuyên giáp."),
                new Option("Nova Rush", "CƠ ĐỘNG  /  SÁT THƯƠNG", 35, 7f, "Lướt 4 ô lao tới mục tiêu, đòn kế tiếp gây thêm 200% Tấn công.")
            },

            // BLACK SHADOW - TÙY CHỌN 1
            new[]
            {
                new Option("Shadow Predator", "CƯỜNG HÓA  /  SÁT THƯƠNG", 45, 10f, "Tăng 20% Tấn công, đạn Shadow Claw to hơn 25%. Hạ gục giảm ngay hồi chiêu của Shadow Step."),
                new Option("Shadow Link", "HỖ TRỢ  /  CƠ ĐỘNG", 50, 12f, "Nối bóng tối với đồng minh: tăng 25% Tốc độ cho cả hai, giảm 20% hồi chiêu của đồng minh trong 6s."),
                new Option("Shadow Guard", "PHÒNG THỦ  /  HỖ TRỢ", 40, 8f, "Chắn hoàn toàn 1 đòn đánh vào đồng minh. Đỡ thành công hồi 20 Năng lượng cho bản thân.")
            },
            // BLACK SHADOW - TÙY CHỌN 2
            new[]
            {
                new Option("Shadow Ambush", "SÁT THƯƠNG  /  CƠ ĐỘNG", 50, 9f, "Dịch chuyển sau lưng mục tiêu, đòn kế tiếp gây 200% Tấn công; sau đó tăng 150% sát thương trong 6s."),
                new Option("Shadow Possession", "HỖ TRỢ  /  PHÒNG THỦ", 60, 15f, "Nhập vào bóng đồng minh 10s: tăng 35% Tốc độ và không thể bị chọn làm mục tiêu."),
                new Option("Shadow Exchange", "CƠ ĐỘNG  /  HỖ TRỢ", 65, 14f, "Tạo phân thân 50% Máu hấp thụ 3 đòn trong 10s. Kích hoạt lại để hoán đổi vị trí và tăng 20% Tốc độ 2s.")
            },

            // LEVIATHAN - TÙY CHỌN 1
            new[]
            {
                new Option("Water Armor", "PHÒNG THỦ  /  SÁT THƯƠNG", 55, 12f, "Bọc giáp nước 6s: tăng 50% Giáp và 15% sát thương Water Pulse; vỡ gây 60% Tấn công và 1% Máu."),
                new Option("Guardian Current", "HỖ TRỢ  /  PHÒNG THỦ", 60, 13f, "Dòng hải lưu hộ vệ 6s: hồi 3.5% Máu mỗi 1s, gánh 30% sát thương đồng minh nhận vào."),
                new Option("Regenerative Shell", "HỒI MÁU  /  PHÒNG THỦ", 50, 15f, "Vỏ tái sinh 5s: hồi 4% Máu mỗi 0.5s, kết thúc tăng vĩnh viễn 20% Máu tối đa.")
            },
            // LEVIATHAN - TÙY CHỌN 2
            new[]
            {
                new Option("Tidal Return", "KHỐNG CHẾ  /  SÁT THƯƠNG", 60, 14f, "Sóng thủy triều kéo địch lại gần, gây 150% Tấn công và 3% Máu tối đa."),
                new Option("Leviathan Form", "CƯỜNG HÓA  /  PHÒNG THỦ", 80, 18f, "Hóa Leviathan 10s: tăng 50 Giáp và 50% Máu tối đa, hồi 1% Máu mỗi giây nhưng giảm 50% Tấn công."),
                new Option("Tide Commander", "HỖ TRỢ  /  CHỈ HUY", 70, 16f, "Vùng chỉ huy 7s: đồng minh tăng 35% Tốc độ, khiên 10% Máu tăng dần lên 15%.")
            },

            // WATER B - TÙY CHỌN 1
            new[]
            {
                new Option("Shared Healing", "HỖ TRỢ  /  HỒI MÁU", 50, 12f, "Chia sẻ hồi máu 10s: 50% lượng hồi máu lên bản thân cũng hồi cho đồng minh gần nhất."),
                new Option("Life Drain", "HỖ TRỢ  /  HÚT MÁU", 45, 11f, "Hút sinh lực 5s: tăng 25% Tấn công, 35% sát thương gây ra chuyển thành Máu."),
                new Option("Bubble Protector", "PHÒNG THỦ  /  HỖ TRỢ", 45, 11f, "Bong bóng bảo vệ 6s: tăng 25 Giáp và 25% Tấn công, hồi 100% Tấn công và 6% Máu.")
            },
            // WATER B - TÙY CHỌN 2
            new[]
            {
                new Option("Mobile Altar", "HỖ TRỢ  /  HỒI MÁU", 75, 16f, "Bàn thờ di động 10s: mỗi 2s hồi 50% Tấn công và 2% Máu, đồng thời tạo khiên 80% Tấn công và 5% Máu."),
                new Option("Water Turrets", "SÁT THƯƠNG  /  TRIỆU HỒI", 75, 14f, "Đặt 3 tháp nước 8s, mỗi tháp bắn 50% Tấn công mỗi 1s; mỗi đòn trúng hồi 3 Năng lượng."),
                new Option("Aqua Assault", "CƯỜNG HÓA  /  SÁT THƯƠNG", 65, 14f, "Cuồng phong thủy kích 7s: tăng 25% Tấn công, 20% hồi chiêu, 15% sát thương và 20% xuyên giáp, đạn to và nhanh hơn.")
            },

            // GREEN FOX - TÙY CHỌN 1
            new[]
            {
                new Option("Mark Hunter", "SÁT THƯƠNG  /  ĐÁNH DẤU", 45, 10f, "Săn dấu 5s, cộng dồn 2 lớp Fox Mark; nổ 300% Tấn công, hạ gục giảm 3s hồi chiêu Fox Mark."),
                new Option("Burning Fox", "SÁT THƯƠNG  /  THIÊU ĐỐT", 40, 11f, "Thiêu đốt 6s, mỗi 0.5s gây 25% Tấn công; trúng mục tiêu đang cháy hồi 3 Năng lượng."),
                new Option("Fox Trap", "KHỐNG CHẾ  /  ĐẶT BẪY", 50, 10f, "Đặt bẫy 15s, trói 2s và gây 150% Tấn công; sát thương lên quái tinh anh tăng lên 230%.")
            },
            // GREEN FOX - TÙY CHỌN 2
            new[]
            {
                new Option("Mark Explosion", "SÁT THƯƠNG  /  DIỆN RỘNG", 55, 12f, "Kích nổ mọi lớp Fox Mark: 20% Tấn công mỗi lớp, sau đó tăng 30% Tấn công trong 6s."),
                new Option("Fox Territory", "HỖ TRỢ  /  KHỐNG CHẾ", 60, 15f, "Lãnh địa cáo 6s: địch giảm 30% Tốc độ, đồng minh tăng 30% Tốc độ; mỗi đòn trúng nhận 3 Năng lượng (tối đa 30)."),
                new Option("Nine-Tailed Assault", "SÁT THƯƠNG  /  TRIỆU HỒI", 75, 16f, "Triệu hồi 9 đuôi 6s, mỗi đuôi gây 15% Tấn công mỗi 1s lên mục tiêu gần nhất.")
            }
        };

        /// <summary>Lấy tên hiển thị của xe theo chỉ số.</summary>
        public static string GetTankName(int tankIndex)
        {
            return tankIndex >= 0 && tankIndex < TankNames.Length ? TankNames[tankIndex] : string.Empty;
        }

        /// <summary>Khoá sprite icon của xe, dùng để ghép tên file icon_&lt;tank&gt;_&lt;tab&gt;_&lt;option&gt;.</summary>
        public static string GetTankKey(int tankIndex)
        {
            return tankIndex >= 0 && tankIndex < TankKeys.Length ? TankKeys[tankIndex] : string.Empty;
        }

        /// <summary>Lấy một lựa chọn theo xe, tab và thứ tự trong tab.</summary>
        public static Option Get(int tankIndex, int tabIndex, int optionIndex)
        {
            int tank = Math.Clamp(tankIndex, 0, TankCount - 1);
            int tab = Math.Clamp(tabIndex, 0, TabCount - 1);
            int option = Math.Clamp(optionIndex, 0, OptionCount - 1);
            return Options[tank * TabCount + tab][option];
        }

        /// <summary>Chỉ số sprite icon phẳng của một lựa chọn trong mảng 36 sprite.</summary>
        public static int GetIconIndex(int tankIndex, int tabIndex, int optionIndex)
        {
            int tank = Math.Clamp(tankIndex, 0, TankCount - 1);
            int tab = Math.Clamp(tabIndex, 0, TabCount - 1);
            int option = Math.Clamp(optionIndex, 0, OptionCount - 1);
            return tank * (TabCount * OptionCount) + tab * OptionCount + option;
        }

        /// <summary>Khoá icon của một lựa chọn: &lt;tank&gt;_&lt;tab&gt;_&lt;option&gt;.</summary>
        public static string GetIconKey(int tankIndex, int tabIndex, int optionIndex)
        {
            return GetTankKey(tankIndex) + "_" + tabIndex + "_" + optionIndex;
        }
    }
}
