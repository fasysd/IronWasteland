namespace IronWasteland.Enemies
{
    /// <summary>
    /// Ke dich TINH ANH cu the (doi tuong thuc te) ke thua <see cref="EliteEnemyBrain"/>.
    /// Logic chua trien khai - de trong / khong lam gi.
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public class EliteEnemyBrain_Test : EliteEnemyBrain
    {
        protected override void OnDeath()
        {
            Destroy(this.gameObject);
        }
    }
}