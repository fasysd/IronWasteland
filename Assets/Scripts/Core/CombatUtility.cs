using UnityEngine;

namespace IronWasteland.Core
{
    /// <summary>
    /// Tien ich tinh toan sat thuong va phong thu chuan theo Tai lieu thiet ke Game (GDD).
    /// </summary>
    public static class CombatUtility
    {
        private static readonly float Ln41 = Mathf.Log(41f);
        private static readonly float Ln31 = Mathf.Log(31f);

        /// <summary>
        /// Sat thuong goc cua Tank:
        /// Sat thuong goc = Tan cong * Scale Ky nang * (1 + Ti le sat thuong).
        /// </summary>
        public static float CalculateTankRawDamage(float attack, float skillScale, float damageRate)
        {
            return Mathf.Max(0f, attack * skillScale * (1f + Mathf.Max(0f, damageRate)));
        }

        /// <summary>
        /// Sat thuong goc cua Ke dich:
        /// Sat thuong goc = Tan cong * Scale Ky nang.
        /// </summary>
        public static float CalculateEnemyRawDamage(float attack, float skillScale)
        {
            return Mathf.Max(0f, attack * skillScale);
        }

        /// <summary>
        /// Tinh chi so Def thuc te sau khi tinh ty le xuyen giap cua ke tan cong:
        /// Def = Phong thu * (1 - Ty le xuyen giap).
        /// </summary>
        public static float CalculateEffectiveDefense(float baseDefense, float attackerArmorPenetration)
        {
            float pen = Mathf.Clamp01(attackerArmorPenetration);
            return Mathf.Max(0f, baseDefense * (1f - pen));
        }

        /// <summary>
        /// Tinh ty le giam sat thuong nhan vao (%) theo 4 phan doan Logarit lien tuc:
        /// - Def tu 0 -> 40 (0% -> 50%): 50 * ln(1 + Def) / ln(41)
        /// - Def tu 40 -> 70 (50% -> 75%): 50 + 25 * ln(1 + Def - 40) / ln(31)
        /// - Def tu 70 -> 100 (75% -> 85%): 75 + 10 * ln(1 + Def - 70) / ln(31)
        /// - Def tu 100 -> vo cung (85% -> 90%): 85 + 5 * (1 - 1 / (1 + ln(Def - 99)))
        /// </summary>
        public static float CalculateDamageReductionPercent(float def)
        {
            if (def <= 0f) return 0f;

            if (def <= 40f)
            {
                return 50f * (Mathf.Log(1f + def) / Ln41);
            }

            if (def <= 70f)
            {
                return 50f + 25f * (Mathf.Log(1f + def - 40f) / Ln31);
            }

            if (def <= 100f)
            {
                return 75f + 10f * (Mathf.Log(1f + def - 70f) / Ln31);
            }

            // Def > 100 -> tiem can 90%
            float logVal = Mathf.Log(Mathf.Max(1f, def - 99f));
            return 85f + 5f * (1f - (1f / (1f + logVal)));
        }

        /// <summary>
        /// Sat thuong gay ra thuc te:
        /// Sat thuong gay ra = Sat thuong goc * (1 - Ti le giam sat thuong / 100).
        /// </summary>
        public static float CalculateFinalDamage(float rawDamage, float damageReductionPercent)
        {
            if (rawDamage <= 0f) return 0f;
            float reductionFraction = Mathf.Clamp(damageReductionPercent, 0f, 100f) / 100f;
            return Mathf.Max(0f, rawDamage * (1f - reductionFraction));
        }

        /// <summary>
        /// Tinh toan day du sat thuong thuc te tu Sat thuong goc, Phong thu va Xuyen giap.
        /// </summary>
        public static float ResolveDamage(float rawDamage, float defenderDefense, float attackerArmorPenetration)
        {
            float effectiveDef = CalculateEffectiveDefense(defenderDefense, attackerArmorPenetration);
            float reductionPercent = CalculateDamageReductionPercent(effectiveDef);
            return CalculateFinalDamage(rawDamage, reductionPercent);
        }
    }
}
