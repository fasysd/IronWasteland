using NUnit.Framework;
using IronWasteland.UiModels;

namespace IronWasteland.Tests
{
    public sealed class RoleTagModelTests
    {
        [Test]
        public void Hai_vai_tro_thi_hien_hai_pill_dung_mau_tu_khoa()
        {
            // Black Shadow: DPS tím, SÁT THỦ đỏ; accent của xe không được dùng vì đã khớp từ khoá.
            RoleTag[] tags = RoleTagModel.Build("DPS", "SÁT THỦ", 0x112233);

            Assert.That(tags.Length, Is.EqualTo(2));
            Assert.That(tags[0].Text, Is.EqualTo("DPS"));
            Assert.That(tags[0].Argb, Is.EqualTo(0x8A4BF0u));
            Assert.That(tags[1].Text, Is.EqualTo("SÁT THỦ"));
            Assert.That(tags[1].Argb, Is.EqualTo(0xFF3B4Eu));
        }

        [Test]
        public void Vai_tro_trung_lap_chi_hien_mot_pill()
        {
            // Green Fox khai báo DPS/DPS thì không được vẽ hai pill giống nhau.
            RoleTag[] tags = RoleTagModel.Build("DPS", "DPS", 0x112233);

            Assert.That(tags.Length, Is.EqualTo(1));
            Assert.That(tags[0].Text, Is.EqualTo("DPS"));
        }

        [Test]
        public void Vai_tro_la_dung_mau_accent_cua_xe()
        {
            // Vai trò không nằm trong bảng từ khoá thì lấy màu accent của xe.
            RoleTag[] tags = RoleTagModel.Build("XẠ THỦ HỎA LỰC", "", 0x112233);

            Assert.That(tags.Length, Is.EqualTo(1));
            Assert.That(tags[0].Argb, Is.EqualTo(0x112233u));
        }

        [Test]
        public void Vai_tro_rong_thi_khong_co_pill_nao()
        {
            RoleTag[] tags = RoleTagModel.Build("", null, 0x112233);

            Assert.That(tags.Length, Is.EqualTo(0));
        }
    }
}
