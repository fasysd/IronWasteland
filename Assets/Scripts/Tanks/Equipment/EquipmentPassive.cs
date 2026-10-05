namespace IronWasteland.Tanks
{
    /// <summary>
    /// Hieu ung bi dong dac biet cua trang bi. Tach rieng khoi <c>statBonus</c>:
    /// statBonus la chi so cong don (them/bot truc tiep), con passive la HANH DONG
    /// dac biet can code rieng de kich hoat.
    ///
    /// 4 cach kich hoat:
    /// - Tick moi khung hinh (SelfRepair, EnergyRegen): theo % Max hien tai moi giay.
    /// - Cong thang chi so luc Equip (CooldownSurge): khong tick, chi cong 1 lan.
    /// - Mot lan ngay khi Equip (ShieldOnEquip): hoi mau/energy theo % toi da.
    /// - Khi xay ra su kien (DamageLifesteal): TankBrain thong bao khi danh trung.
    /// </summary>
    public enum EquipmentPassive
    {
        /// <summary>Khong co hieu ung dac biet (chi co statBonus).</summary>
        None = 0,

        /// <summary>
        /// Tu hoi mau: moi giay hoi <c>passiveMagnitude</c>% MaxHealth.
        /// passiveMagnitude = 2 -> hoi 2%/giay.
        /// </summary>
        SelfRepair = 1,

        /// <summary>
        /// Tu hoi nang luong: moi giay hoi <c>passiveMagnitude</c>% MaxEnergy.
        /// passiveMagnitude = 5 -> hoi 5%/giay.
        /// </summary>
        EnergyRegen = 2,

        /// <summary>
        /// Giam hoi chieu: cong them <c>passiveMagnitude</c> vao CooldownReduction
        /// khi trang bi duoc Equip (KHONG tick theo khung hinh).
        /// passiveMagnitude = 0.1 -> giam 10% hoi chieu.
        /// </summary>
        CooldownSurge = 3,

        /// <summary>
        /// Hoi mau khi danh trung: moi lan danh gay <c>damage</c> sat thuong thi hoi
        /// <c>passiveMagnitude</c>% cua sat thuong do. Can TankBrain.NotifyDamageDealt().
        /// passiveMagnitude = 10 -> hoi 10% damage.
        /// </summary>
        DamageLifesteal = 4,

        /// <summary>
        /// Luanch: ngay khi Equip hoi <c>passiveMagnitude</c>% MaxHealth (chi mot lan).
        /// passiveMagnitude = 15 -> hoi 15% MaxHealth.
        /// </summary>
        ShieldOnEquip = 5,
    }
}
