namespace IronWasteland.Enemies
{
    /// <summary>
    /// Ke dich THUONG - ke dich binh thuong, chi co khung logic.
    ///
    /// TODO (giai doan sau, chua lam):
    /// - Di chuyen / truy duoi muc tieu (dung Stats.MoveSpeed).
    /// - Tan cong Tank khi den gan.
    /// - Ky nang: tu tao field CD / timer va method thi trien rieng trong lop nay.
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public class NormalEnemyBrain : EnemyBrain
    {
        protected override void OnBrainTick(float deltaTime)
        {
            // TODO: di chuyen + hanh vi AI cua ke dich thuong.
        }

        protected override void OnDeath()
        {
            // TODO: hieu ung chet, drop pham, tru di muc tren man choi.
        }
    }
}