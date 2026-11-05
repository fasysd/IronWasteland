using UnityEngine;

namespace IronWasteland.Core
{
    /// <summary>
    /// Cau truc du lieu sat thuong truyen qua toan bo he thong combat va su kien.
    /// </summary>
    public struct DamageInfo
    {
        /// <summary>Doi tuong gay sat thuong (TankBrain, EnemyBrain hoac null).</summary>
        public Object Attacker;

        /// <summary>Doi tuong nhan sat thuong (TankBrain, EnemyBrain).</summary>
        public Object Target;

        /// <summary>Sat thuong goc truoc khi tinh giam tru boi phong thu.</summary>
        public float RawDamage;

        /// <summary>Sat thuong thuc te gay ra sau khi ap dung phong thu va xuyen giap.</summary>
        public float FinalDamage;

        /// <summary>Ty le xuyen giap cua ke tan cong (0..1, toi da 100%).</summary>
        public float ArmorPenetration;

        /// <summary>Vi tri gay sat thuong trong world space (de hien text damage, hieu ung).</summary>
        public Vector2 HitPosition;

        /// <summary>
        /// True: Sat thuong tru truc tiep vao mau, bo qua khien chan
        /// (dung cho co che chuyen sat thuong dong minh nhu Guardian Wall, Earth Guardian).
        /// </summary>
        public bool IsDirectToHealth;

        public DamageInfo(Object attacker, Object target, float rawDamage, float armorPenetration = 0f, Vector2 hitPosition = default, bool isDirectToHealth = false)
        {
            Attacker = attacker;
            Target = target;
            RawDamage = rawDamage;
            FinalDamage = rawDamage;
            ArmorPenetration = Mathf.Clamp01(armorPenetration);
            HitPosition = hitPosition;
            IsDirectToHealth = isDirectToHealth;
        }
    }
}
