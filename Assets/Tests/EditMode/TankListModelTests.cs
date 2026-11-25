using NUnit.Framework;
using IronWasteland.UiModels;

namespace IronWasteland.Tests
{
    public sealed class TankListModelTests
    {
        [Test]
        public void Xe_da_mo_hien_thi_ten_vai_tro_va_cap()
        {
            // Xe mở sẵn (không có mã ải), cấp 1, vai trò chính + phụ.
            TankRowSource[] roster =
            {
                new TankRowSource("wall-titan", "WALL TITAN", "TANKER", "HỖ TRỢ", ""),
            };

            TankRow[] rows = TankListModel.Build(roster, _ => 1, _ => false);

            Assert.That(rows.Length, Is.EqualTo(1));
            Assert.That(rows[0].DisplayName, Is.EqualTo("WALL TITAN"));
            Assert.That(rows[0].RoleText, Is.EqualTo("TANKER / HỖ TRỢ"));
            Assert.That(rows[0].LevelText, Is.EqualTo("CẤP 01 / 90"));
            Assert.That(rows[0].IsLocked, Is.False);
            Assert.That(rows[0].UnlockText, Is.Empty);
        }

        [Test]
        public void Xe_chua_mo_bi_khoa_va_hien_thi_ai_can_mo()
        {
            // Green Fox chỉ mở khi ải 2-5 mở; người chơi chưa mở ải đó.
            TankRowSource[] roster =
            {
                new TankRowSource("green-fox", "GREEN FOX", "DPS", "DPS", "2-5"),
            };

            TankRow[] rows = TankListModel.Build(roster, _ => 1, _ => false);

            Assert.That(rows[0].IsLocked, Is.True);
            Assert.That(rows[0].UnlockText, Is.EqualTo("ẢI 2-5"));
        }

        [Test]
        public void Thanh_tien_trinh_bang_ti_le_cap_tren_cap_toi_da()
        {
            // Cấp 45 trên thang 90 → thanh đầy một nửa.
            TankRowSource[] roster =
            {
                new TankRowSource("iron-viper", "IRON VIPER", "TANKER", "", ""),
            };

            TankRow[] rows = TankListModel.Build(roster, _ => 45, _ => true);

            Assert.That(rows[0].BarFraction, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
