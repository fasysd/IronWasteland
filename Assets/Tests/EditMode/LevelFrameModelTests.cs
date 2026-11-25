using NUnit.Framework;
using IronWasteland.UiModels;

namespace IronWasteland.Tests
{
    public sealed class LevelFrameModelTests
    {
        [Test]
        public void Khung_cap_in_so_hai_chu_so_va_mau_so_dong()
        {
            LevelFrame frame = LevelFrameModel.Build(5, 90);

            Assert.That(frame.LevelText, Is.EqualTo("05"));
            Assert.That(frame.MaxText, Is.EqualTo("/ 90"));
            Assert.That(frame.Level, Is.EqualTo(5));
        }

        [Test]
        public void Cap_bi_kep_trong_khoang_1_den_cap_toi_da()
        {
            Assert.That(LevelFrameModel.Build(0, 90).LevelText, Is.EqualTo("01"));
            Assert.That(LevelFrameModel.Build(120, 90).LevelText, Is.EqualTo("90"));
        }

        [Test]
        public void Cap_toi_da_khong_hop_le_thi_dung_mac_dinh()
        {
            LevelFrame frame = LevelFrameModel.Build(5, 0);

            Assert.That(frame.MaxLevel, Is.EqualTo(LevelFrameModel.DefaultMaxLevel));
            Assert.That(frame.MaxText, Is.EqualTo("/ 90"));
        }
    }
}
