namespace IronWasteland.Core
{
    /// <summary>
    /// Nguồn gốc / Loại của sát thương trong toàn bộ hệ thống Combat.
    /// Tách bạch giữa 'Người gây sát thương' (Attacker: TankBrain, EnemyBrain...) 
    /// và 'Bản chất/Loại của đòn đánh' (SourceType: NormalSkill, SpecialSkill, Accessory...).
    /// </summary>
    public enum DamageSourceType
    {
        Default = 0,

        // Kỹ năng của Tank
        NormalSkill = 1,       // Kỹ năng thường của Tank (Slot 0)
        SpecialSkill = 2,      // Kỹ năng chuyên biệt của Tank (Slot 1)
        OptionSkill1 = 3,      // Kỹ năng tùy chọn 1 của Tank (Slot 2)
        OptionSkill2 = 4,      // Kỹ năng tùy chọn 2 của Tank (Slot 3)

        // Phụ kiện / Trang bị
        Accessory = 10,        // Sát thương từ hiệu ứng Phụ kiện (Thiên thạch nổ, Sấm sét lan...)

        // Kẻ địch
        EnemyAttack = 20,      // Sát thương từ đòn đánh / kỹ năng của kẻ địch (Normal / Elite)

        // Sát thương định kỳ & Môi trường
        DamageOverTime = 30,   // Sát thương định kỳ (DoT) - Thiêu đốt (Burn), trúng độc
        Environment = 40       // Bẫy sàn, địa hình, môi trường
    }
}
