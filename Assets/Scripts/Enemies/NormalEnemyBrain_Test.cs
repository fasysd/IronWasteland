namespace IronWasteland.Enemies
{
    /// <summary>
    /// Ke dich THUONG cu the (doi tuong thuc te) ke thua <see cref="NormalEnemyBrain"/>.
    /// Logic chua trien khai - de trong / khong lam gi.
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public class NormalEnemyBrain_Test : NormalEnemyBrain
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