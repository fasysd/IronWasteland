using System;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Registry định nghĩa danh sách và nạp 6 TankDefinition của trò chơi.
    /// Đảm bảo tính nhất quán về thứ tự xe (index 0..5):
    /// 0: Wall Titan
    /// 1: Red Nova
    /// 2: Black Shadow
    /// 3: Leviathan
    /// 4: Water B
    /// 5: Green Fox
    /// </summary>
    public static class TankRegistry
    {
        public const int TankCount = 6;

        public static readonly string[] TankIds =
        {
            "WallTitan",
            "RedNova",
            "BlackShadow",
            "Leviathan",
            "WaterB",
            "GreenFox"
        };

        public static readonly string[] TankAssetPaths =
        {
            "Assets/Prefabs/Tanks/WallTitan/WallTitan.asset",
            "Assets/Prefabs/Tanks/RedNova/RedNova.asset",
            "Assets/Prefabs/Tanks/BlackShadow/BlackShadow.asset",
            "Assets/Prefabs/Tanks/Leviathan/Leviathan.asset",
            "Assets/Prefabs/Tanks/WaterB/WaterB.asset",
            "Assets/Prefabs/Tanks/GreenFox/GreenFox.asset"
        };

        private static TankDefinition[] s_CachedDefinitions;

        public static TankDefinition GetDefinition(int index)
        {
            if (index < 0 || index >= TankCount) return null;

            if (s_CachedDefinitions == null || s_CachedDefinitions.Length != TankCount)
            {
                s_CachedDefinitions = new TankDefinition[TankCount];
            }

            if (s_CachedDefinitions[index] == null)
            {
#if UNITY_EDITOR
                s_CachedDefinitions[index] = UnityEditor.AssetDatabase.LoadAssetAtPath<TankDefinition>(TankAssetPaths[index]);
#endif
            }

            return s_CachedDefinitions[index];
        }

        public static void RegisterCache(int index, TankDefinition def)
        {
            if (index < 0 || index >= TankCount) return;
            if (s_CachedDefinitions == null || s_CachedDefinitions.Length != TankCount)
            {
                s_CachedDefinitions = new TankDefinition[TankCount];
            }
            s_CachedDefinitions[index] = def;
        }

        public static void RegisterAll(TankDefinition[] defs)
        {
            if (defs == null) return;
            if (s_CachedDefinitions == null || s_CachedDefinitions.Length != TankCount)
            {
                s_CachedDefinitions = new TankDefinition[TankCount];
            }
            for (int i = 0; i < defs.Length && i < TankCount; i++)
            {
                if (defs[i] != null) s_CachedDefinitions[i] = defs[i];
            }
        }
    }
}
