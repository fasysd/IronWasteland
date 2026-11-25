#if UNITY_EDITOR
using IronWasteland.Enemies;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Quái giả cho self-check: chỉ cần vòng đời tối thiểu của <see cref="EnemyBrain"/>
    /// (sống/chết) để kiểm tra HUD, không chạy AI.
    /// </summary>
    public class FakeEnemyBrain : EnemyBrain
    {
        protected override void OnBrainTick(float deltaTime)
        {
        }

        protected override void OnDeath()
        {
        }
    }
}
#endif
