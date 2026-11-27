using IronWasteland.UiModels;
using NUnit.Framework;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Khoá hành vi lật trục y của hệ thiết kế 1920x1080 sang RectTransform gốc dưới-trái.
    /// Số kỳ vọng lấy trực tiếp từ toạ độ thiết kế, không tính lại bằng công thức của code.
    /// </summary>
    public class UILayoutModelTests
    {
        [Test]
        public void Muc_dieu_huong_dau_tien_lat_dung_truc_y()
        {
            Assert.That(UILayoutModel.AnchoredY(1010f, 44f), Is.EqualTo(26f).Within(0.001f));
        }

        [Test]
        public void Cha_cao_hon_1080_van_lat_dung()
        {
            Assert.That(UILayoutModel.AnchoredY(100f, 50f, 720f), Is.EqualTo(570f).Within(0.001f));
        }

        [Test]
        public void Doi_qua_lai_design_va_anchored_giu_nguyen_rect()
        {
            const float y = 484f;
            const float h = 112f;

            float anchoredY = UILayoutModel.AnchoredY(y, h);

            Assert.That(anchoredY, Is.EqualTo(484f).Within(0.001f));
            Assert.That(UILayoutModel.DesignY(anchoredY, h), Is.EqualTo(y).Within(0.001f));
        }
    }
}
