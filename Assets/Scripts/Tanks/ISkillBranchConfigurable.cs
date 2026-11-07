namespace IronWasteland.Tanks
{
    /// <summary>
    /// Cho phép đổi nhánh kỹ năng tùy chọn ngay lúc chạy (dùng cho sân test kỹ năng).
    /// Slot 2 (L) dùng <see cref="Option1Branch"/>, slot 3 (I) dùng <see cref="Option2Branch"/>.
    /// </summary>
    public interface ISkillBranchConfigurable
    {
        /// <summary>Nhánh kỹ năng tùy chọn 1 (slot L): 0, 1 hoặc 2.</summary>
        int Option1Branch { get; set; }

        /// <summary>Nhánh kỹ năng tùy chọn 2 (slot I): 0, 1 hoặc 2.</summary>
        int Option2Branch { get; set; }

        /// <summary>Tên hiển thị của kỹ năng ở slot (0..3) theo nhánh đang chọn.</summary>
        string GetSkillName(int slotIndex);
    }
}
