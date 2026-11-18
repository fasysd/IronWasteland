namespace IronWasteland.Audio
{
    /// <summary>
    /// Cac nhom am thanh dung chung trong game.
    /// Moi nhom co 2-3 bien the (clip) de phat ngau nhien, tranh bi lap am.
    /// </summary>
    public enum SfxCategory
    {
        SkillCast = 0,      // tank/ enemy bat dau thi trien ky nang
        SkillImpact = 1,    // don dan/ no/ chem trung muc tieu
        TankHit = 2,        // tank nguoi choi an don
        EnemyHit = 3,       // quai an don
        EnemyDeath = 4,     // quai chet
        TankDeath = 5,      // tank chet
        Shield = 6,         // tao/vo khien
        Heal = 7,           // hoi mau
        EnemySpawn = 8,     // quai xuat hien
        CrowdControl = 9,   // lam cham/ choang
        StageClear = 10,    // thang man
        StageFailed = 11,   // thua man
        UIClick = 12,       // bam nut
    }
}
