using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Tien ich tinh toan tai nguyen "Ngoc ma phap" tieu hao de nang cap Tank (Cap 1 -> 90)
    /// theo dung bang phan doan trong Tai lieu thiet ke Game (GDD).
    /// </summary>
    public static class TankUpgradeUtility
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 90;

        /// <summary>
        /// Lay luong Ngoc ma phap can thiet de nang tu (level - 1) len level.
        /// Tra ve 0 neu level <= 1 hoac level > 90.
        /// </summary>
        public static int GetCostForNextLevel(int currentLevel)
        {
            int targetLevel = currentLevel + 1;
            if (targetLevel <= MinLevel || targetLevel > MaxLevel) return 0;

            if (targetLevel <= 20) return 5;
            if (targetLevel <= 40) return 10;
            if (targetLevel <= 60) return 15;
            if (targetLevel <= 80) return 20;
            return 25; // 81..90
        }

        /// <summary>
        /// Tinh tong so Ngoc ma phap can de nang tu startLevel len targetLevel.
        /// </summary>
        public static int GetUpgradeCost(int startLevel, int targetLevel)
        {
            startLevel = Mathf.Clamp(startLevel, MinLevel, MaxLevel);
            targetLevel = Mathf.Clamp(targetLevel, MinLevel, MaxLevel);

            if (targetLevel <= startLevel) return 0;

            int totalCost = 0;
            for (int lvl = startLevel; lvl < targetLevel; lvl++)
            {
                totalCost += GetCostForNextLevel(lvl);
            }

            return totalCost;
        }
    }
}
