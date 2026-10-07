namespace IronWasteland.Tanks
{
    /// <summary>
    /// Loai trang bi, quyet dinh trang bi do "lep" vao vi tri nao cua Tank.
    /// Chi co 3 loai hien tai, ung 1-1 voi 3 lop con: FirepowerCore, DefenseCore, MobilityCore.
    /// TankBrain / EquipmentDatabase dung enum nay de xep trang bi vao dung loai.
    /// </summary>
    public enum EquipmentSlot
    {
        /// <summary>Khong xep vao slot nao (dung cho gia tri loi / chua gan).</summary>
        None = 0,

        /// <summary>Phu kien sung (o Nong Phao).</summary>
        Firepower = 1,

        /// <summary>Vo xe (o Than Xe).</summary>
        Defense = 2,

        /// <summary>Bo banh / xich (o Bo Banh).</summary>
        Mobility = 3,
    }
}
