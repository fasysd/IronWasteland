using IronWasteland.UiModels;
using NUnit.Framework;

namespace IronWasteland.Tests
{
    public class StatBarModelTests
    {
        [Test]
        public void Tam_thanh_chuan_hoa_theo_gia_tri_lon_nhat_trong_roster()
        {
            float[] values = { 50f, 25f, 0f, 0f, 100f, 200f, 5f, 0.3f };
            float[] maxima = { 100f, 100f, 100f, 1f, 400f, 400f, 10f, 0.6f };

            float[] bars = StatBarModel.Build(values, maxima);

            Assert.That(bars.Length, Is.EqualTo(StatBarModel.Count));
            Assert.That(bars[0], Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(bars[4], Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(bars[7], Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Truc_bang_khong_thi_thanh_rong_khong_chia_cho_khong()
        {
            float[] values = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f };
            float[] maxima = new float[StatBarModel.Count];

            float[] bars = StatBarModel.Build(values, maxima);

            Assert.That(bars, Is.All.EqualTo(0f));
        }

        [Test]
        public void Gia_tri_vuot_tran_bi_kep_trong_khoang_0_1()
        {
            float[] values = { 500f, -10f };
            float[] maxima = { 100f, 100f };

            float[] bars = StatBarModel.Build(values, maxima);

            Assert.That(bars[0], Is.EqualTo(1f));
            Assert.That(bars[1], Is.EqualTo(0f));
        }
    }
}
