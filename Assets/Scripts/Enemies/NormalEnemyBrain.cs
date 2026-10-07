namespace IronWasteland.Enemies
{
    /// <summary>
    /// Ke dich THUONG - ke dich binh thuong (lop ABSTRACT, dung lam base/framework).
    ///
    /// Khong trien khai truc tiep: cac hook core ke thua tu <see cref="EnemyBrain"/>
    /// (<see cref="EnemyBrain.OnBrainTick(float)"/>, <see cref="EnemyBrain.OnDeath"/> )
    /// de trong cho lop con cu the (vi du NormalEnemyBrain_Test) trien khai.
    ///
    /// TODO (giai doan sau, chua lam):
    /// - Di chuyen / truy duoi muc tieu (dung Stats.MoveSpeed).
    /// - Tan cong Tank khi den gan.
    /// - Ky nang: tu tao field CD / timer va method thi trien rieng trong lop con.
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public abstract class NormalEnemyBrain : EnemyBrain
    {
    }
}