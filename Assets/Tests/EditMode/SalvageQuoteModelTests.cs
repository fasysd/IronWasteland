using IronWasteland.UiModels;
using NUnit.Framework;

namespace IronWasteland.Tests
{
    public class SalvageQuoteModelTests
    {
        [Test]
        public void So_luong_muon_phan_ra_bi_kep_ve_ton_kho_va_tinh_tong_ngoc()
        {
            SalvageQuote quote = SalvageQuoteModel.Build(owned: 3, salvageValue: 5, requested: 7, isEquipped: false);

            Assert.That(quote.Quantity, Is.EqualTo(3));
            Assert.That(quote.TotalGems, Is.EqualTo(15));
            Assert.That(quote.CanSalvage, Is.True);
        }

        [Test]
        public void Phu_kien_dang_trang_bi_thi_khong_phan_ra_duoc()
        {
            SalvageQuote quote = SalvageQuoteModel.Build(owned: 2, salvageValue: 5, requested: 1, isEquipped: true);

            Assert.That(quote.CanSalvage, Is.False);
            Assert.That(quote.TotalGems, Is.Zero);
            Assert.That(quote.Hint, Is.EqualTo(SalvageQuoteModel.EquippedHint));
        }

        [Test]
        public void Phu_kien_chua_so_huu_thi_khong_phan_ra_duoc()
        {
            SalvageQuote quote = SalvageQuoteModel.Build(owned: 0, salvageValue: 5, requested: 1, isEquipped: false);

            Assert.That(quote.CanSalvage, Is.False);
            Assert.That(quote.Hint, Is.EqualTo(SalvageQuoteModel.NotOwnedHint));
        }

        [Test]
        public void Phu_kien_chua_so_huu_du_loadout_con_tro_toi_van_bao_chua_so_huu()
        {
            SalvageQuote quote = SalvageQuoteModel.Build(owned: 0, salvageValue: 5, requested: 1, isEquipped: true);

            Assert.That(quote.CanSalvage, Is.False);
            Assert.That(quote.Hint, Is.EqualTo(SalvageQuoteModel.NotOwnedHint));
        }

        [Test]
        public void So_luong_bi_kep_toi_da_chin_muoi_chin()
        {
            SalvageQuote quote = SalvageQuoteModel.Build(owned: 500, salvageValue: 2, requested: 999, isEquipped: false);

            Assert.That(quote.Quantity, Is.EqualTo(SalvageQuoteModel.MaxQuantity));
            Assert.That(quote.TotalGems, Is.EqualTo(198));
        }
    }
}
