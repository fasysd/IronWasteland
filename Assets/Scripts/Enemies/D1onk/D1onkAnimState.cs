namespace IronWasteland.Enemies.D1onk
{
    /// <summary>
    /// Các trạng thái diễn hoạt Procedural cho Trưởng Quản Ngục D1onk.
    /// Toàn bộ diễn hoạt được tính toán bằng cách xoay các bộ phận quanh trục Z (localRotation.eulerAngles.z).
    /// </summary>
    public enum D1onkAnimState
    {
        Idle,
        Move,
        PhaoHamDaNong,
        TrungPhat,
        LazeNguyenTo,
        ToaNhietDienRong,
        VienBinhTinhLinh,
        Charging,
        Stun,
        Die
    }
}
