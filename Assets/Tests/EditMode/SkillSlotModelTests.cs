using IronWasteland.UiModels;
using NUnit.Framework;

namespace IronWasteland.Tests
{
    public class SkillSlotModelTests
    {
        [Test]
        public void Du_bon_ky_nang_thi_moi_the_mot_phim_tu_1_den_4()
        {
            SkillSlotSource[] skills =
            {
                new SkillSlotSource("PHÁO CHÍNH", "Bắn đạn xuyên giáp.", "icon_a"),
                new SkillSlotSource("KHIÊN TỪ TRƯỜNG", "Chặn sát thương.", "icon_b"),
                new SkillSlotSource("ỐNG PHÓNG", "Bắn loạt tên lửa.", "icon_c"),
                new SkillSlotSource("TÀNG HÌNH", "Ẩn thân 3 giây.", "icon_d"),
            };

            SkillSlot[] slots = SkillSlotModel.Build(skills);

            Assert.That(slots.Length, Is.EqualTo(SkillSlotModel.SlotCount));
            Assert.That(slots[0].KeyText, Is.EqualTo("1"));
            Assert.That(slots[3].KeyText, Is.EqualTo("4"));
            Assert.That(slots[3].NameText, Is.EqualTo("TÀNG HÌNH"));
            Assert.That(slots[3].IsEmpty, Is.False);
        }

        [Test]
        public void Thieu_ky_nang_thi_the_trong_van_giu_phim_va_bao_chua_mo()
        {
            SkillSlotSource[] skills =
            {
                new SkillSlotSource("PHÁO CHÍNH", "Bắn đạn xuyên giáp.", "icon_a"),
                new SkillSlotSource("KHIÊN TỪ TRƯỜNG", "Chặn sát thương.", "icon_b"),
            };

            SkillSlot[] slots = SkillSlotModel.Build(skills);

            Assert.That(slots.Length, Is.EqualTo(SkillSlotModel.SlotCount));
            Assert.That(slots[2].IsEmpty, Is.True);
            Assert.That(slots[2].KeyText, Is.EqualTo("3"));
            Assert.That(slots[2].NameText, Is.EqualTo(SkillSlotModel.EmptyName));
            Assert.That(slots[2].DescText, Is.Empty);
        }
    }
}
