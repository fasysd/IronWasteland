using IronWasteland.Enemies;
using IronWasteland.Tanks;
using UnityEngine;

namespace IronWasteland.Core.Effects
{
    /// <summary>
    /// Hieu ung Thieu dot (Burn - DoT): gay sat thuong theo thoi gian theo % Attack moi giay.
    /// Ho tro toi da maxStacks tang. Theo GDD, chinh sach mac dinh la CapOnly (them tang khong dat lai gio).
    /// </summary>
    public class BurnEffect : StatusEffect
    {
        public UnityEngine.Object Source { get; private set; }
        public float DamagePerSecondPerStack { get; private set; }

        public BurnEffect(
            UnityEngine.Object source,
            float duration = 3f,
            float damagePerSecondPerStack = 10f,
            int maxStacks = 3,
            float tickInterval = 0.5f,
            EffectStackPolicy stackPolicy = EffectStackPolicy.CapOnly)
            : base("Burn", "Thiêu Đốt", duration, maxStacks, stackPolicy, tickInterval)
        {
            Source = source;
            DamagePerSecondPerStack = damagePerSecondPerStack;
        }

        public override void OnTick(UnityEngine.Object target)
        {
            if (target == null) return;

            float damage = DamagePerSecondPerStack * currentStacks * tickInterval;

            if (target is EnemyBrain enemy)
            {
                var hitPos = enemy.transform.position;
                DamageInfo info = new DamageInfo(Source, enemy, damage, 0f, hitPos, isDirectToHealth: true, isDamageOverTime: true);
                enemy.TakeDamage(info);
                TextDamageManager.Instance?.Show(damage, hitPos);
            }
            else if (target is TankBrain tank)
            {
                var hitPos = tank.transform.position;
                DamageInfo info = new DamageInfo(Source, tank, damage, 0f, hitPos, isDirectToHealth: true, isDamageOverTime: true);
                tank.TakeDamage(info);
                TextDamageManager.Instance?.Show(damage, hitPos);
            }
        }
    }
}
