using UnityEngine;

namespace IronWasteland.Enemies
{
    /// <summary>
    /// Kẻ địch THƯỜNG - lớp cha trừu tượng (framework base class).
    /// KHÔNG chứa logic tìm đường hay di chuyển chung, việc này do từng lớp con đảm nhiệm theo đặc thù riêng.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class NormalEnemyBrain : EnemyBrain
    {
    }
}