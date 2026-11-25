using System;

namespace IronWasteland.UiModels
{
    /// <summary>Dữ liệu thô của một kỹ năng, do controller map từ TankDefinition sang.</summary>
    public readonly struct SkillSlotSource
    {
        public readonly string Name;
        public readonly string Description;
        public readonly string IconKey;

        public SkillSlotSource(string name, string description, string iconKey)
        {
            Name = name;
            Description = description;
            IconKey = iconKey;
        }
    }

    /// <summary>Một thẻ kỹ năng đã sẵn sàng để đổ vào panel kỹ năng.</summary>
    public readonly struct SkillSlot
    {
        public readonly int Index;
        /// <summary>Phím bấm hiển thị trên thẻ: "1".."4".</summary>
        public readonly string KeyText;
        public readonly string NameText;
        public readonly string DescText;
        public readonly string IconKey;
        public readonly bool IsEmpty;

        public SkillSlot(int index, string keyText, string nameText, string descText, string iconKey, bool isEmpty)
        {
            Index = index;
            KeyText = keyText;
            NameText = nameText;
            DescText = descText;
            IconKey = iconKey;
            IsEmpty = isEmpty;
        }
    }

    /// <summary>Dựng 4 thẻ kỹ năng của xe đang chọn.</summary>
    public static class SkillSlotModel
    {
        /// <summary>Số thẻ kỹ năng trên panel.</summary>
        public const int SlotCount = 4;

        /// <summary>Chữ hiển thị cho thẻ chưa có kỹ năng.</summary>
        public const string EmptyName = "CHƯA MỞ";

        public static SkillSlot[] Build(SkillSlotSource[] skills)
        {
            SkillSlot[] slots = new SkillSlot[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                string keyText = (i + 1).ToString();
                bool hasSkill = skills != null && i < skills.Length;

                slots[i] = hasSkill
                    ? new SkillSlot(i, keyText, skills[i].Name, skills[i].Description ?? string.Empty, skills[i].IconKey ?? string.Empty, false)
                    : new SkillSlot(i, keyText, EmptyName, string.Empty, string.Empty, true);
            }

            return slots;
        }
    }
}
