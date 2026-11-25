using IronWasteland.UiModels;
using NUnit.Framework;

namespace IronWasteland.Tests
{
    public class AccessoryCardModelTests
    {
        [Test]
        public void Chua_trang_bi_hien_thi_chu_goi_y()
        {
            AccessoryCard card = AccessoryCardModel.Build(null);

            Assert.That(card.HasAccessory, Is.False);
            Assert.That(card.NameText, Is.EqualTo(AccessoryCardModel.EmptyName));
            Assert.That(card.BonusText, Is.Empty);
        }

        [Test]
        public void Co_trang_bi_hien_thi_ten_dong_chi_so_va_bi_dong()
        {
            AccessoryCardSource source = new AccessoryCardSource(
                "GIÁP PHẢN LỰC",
                new[] { "+120 GIÁP", "+8% KHÁNG NỔ" },
                "+10% sát thương phản lại");

            AccessoryCard card = AccessoryCardModel.Build(source);

            Assert.That(card.HasAccessory, Is.True);
            Assert.That(card.NameText, Is.EqualTo("GIÁP PHẢN LỰC"));
            Assert.That(card.BonusText, Is.EqualTo("+120 GIÁP\n+8% KHÁNG NỔ"));
            Assert.That(card.PassiveText, Is.EqualTo("+10% sát thương phản lại"));
        }
    }
}
