namespace IronWasteland.Enemies
{
    /// <summary>
    /// Kẻ địch TINH ANH cụ thể (đối tượng thực tế) kế thừa <see cref="EliteEnemyBrain"/>.
    /// Dùng làm placeholder cho các prefab quái Tinh anh chưa có logic riêng.
    /// </summary>
    [UnityEngine.DisallowMultipleComponent]
    public class EliteEnemyBrain_Test : EliteEnemyBrain
    {
        protected override void OnDeath()
        {
            base.OnDeath();
            IronWasteland.Net.NetEntity.Despawn(this.gameObject);
        }

        protected override void OnBurstStart() { }
        protected override void OnBurstSuccess() { }
        protected override void OnBurstFail() { }
    }
}
