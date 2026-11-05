using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// Mo ta 1 loai Tank: ten, mo ta, 2 moc chi so co ban (Cap 1 va Cap 90), va cac prefab.
    /// Theo GDD: moi xe co 2 moc chi so co ban o cap 1 va cap 90, cac cap con lai noi suy tuyen tinh.
    /// </summary>
    [CreateAssetMenu(menuName = "IronWasteland/Tank/Tank Definition", fileName = "TankDefinition")]
    public class TankDefinition : ScriptableObject
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 90;

        [Header("Info")]
        [SerializeField] private string tankName = "Tank";
        [TextArea(2, 4)]
        [SerializeField] private string description;

        [Header("Stats Milestones (Level 1 & 90)")]
        [Tooltip("Chi so co ban tai Cap 1 (Level 1).")]
        [SerializeField] private TankStats baseStatsLevel1 = new TankStats();

        [Tooltip("Chi so co ban tai Cap 90 (Level 90).")]
        [SerializeField] private TankStats baseStatsLevel90 = new TankStats();

        [Header("Prefabs")]
        [Tooltip("Prefab chua TankBrain (khong co TankView).")]
        [SerializeField] private TankBrain tankPrefab;

        [Tooltip("Danh sach TankView prefab. Index trong mang la TankView ID.")]
        [SerializeField] private TankView[] viewPrefabs;

        [Header("Equipment")]
        [Tooltip("Kho du lieu dung de tra cuu prefab trang bi / phu kien.")]
        [SerializeField] private EquipmentDatabase equipmentDatabase;

        [Header("Context")]
        [Tooltip("Context khoi tao cho TankBrain khi CreateTank.")]
        [SerializeField] private TankBrainContext context;

        public string TankName => tankName;
        public string Description => description;
        public TankStats BaseStatsLevel1 => baseStatsLevel1;
        public TankStats BaseStatsLevel90 => baseStatsLevel90;
        public TankBrain TankPrefab => tankPrefab;
        public TankView[] ViewPrefabs => viewPrefabs;
        public int ViewCount => viewPrefabs != null ? viewPrefabs.Length : 0;
        public EquipmentDatabase EquipmentDatabase => equipmentDatabase;

        /// <summary>Lay TankView prefab theo ID (index trong mang).</summary>
        public TankView GetViewPrefab(int viewId)
        {
            if (viewPrefabs == null || viewId < 0 || viewId >= viewPrefabs.Length) return null;
            return viewPrefabs[viewId];
        }

        /// <summary>
        /// Tao Tank moi voi context chi dinh.
        /// </summary>
        public TankBrain CreateTank(TankBrainContext context)
        {
            if (tankPrefab == null)
            {
                Debug.LogError($"[{nameof(TankDefinition)}] Chua gan TankPrefab.", this);
                return null;
            }

            TankView viewPrefab = GetViewPrefab(context.IdView);
            if (viewPrefab == null)
            {
                Debug.LogError($"[{nameof(TankDefinition)}] TankView ID {context.IdView} khong hop le.", this);
                return null;
            }

            TankBrain brain = Instantiate(tankPrefab, Vector3.zero, Quaternion.identity, null);
            brain.Initialize(context, GetStatsForLevel(context.Level));
            brain.SetViewPrefab(viewPrefab);

            // Trang bi 1 phu kien duy nhat theo context.EquipmentId
            if (context.EquipmentId >= 0 && equipmentDatabase != null)
            {
                var accessoryPrefab = equipmentDatabase.GetPrefab(context.EquipmentId);
                if (accessoryPrefab != null)
                {
                    brain.EquipAccessory(accessoryPrefab);
                }
            }

            return brain;
        }

        /// <summary>
        /// Tinh chi so co ban tai mot level (1..90) bang noi suy tuyen tinh tu Cap 1 va Cap 90:
        /// Stat(L) = Stat(1) + (Stat(90) - Stat(1)) * (L - 1) / (90 - 1).
        /// </summary>
        public TankStats GetStatsForLevel(int level)
        {
            level = Mathf.Clamp(level, MinLevel, MaxLevel);

            if (baseStatsLevel1 == null || baseStatsLevel90 == null)
            {
                Debug.LogWarning($"[{nameof(TankDefinition)}] Thieu du lieu chi so Cap 1 hoac Cap 90.", this);
                return new TankStats();
            }

            float t = (float)(level - MinLevel) / (MaxLevel - MinLevel); // (level - 1) / 89f
            return Interpolate(baseStatsLevel1, baseStatsLevel90, t);
        }

        private static TankStats Interpolate(TankStats a, TankStats b, float t)
        {
            TankStats r = a.Clone();
            r.Attack = Mathf.Lerp(a.Attack, b.Attack, t);
            r.MaxHealth = Mathf.Lerp(a.MaxHealth, b.MaxHealth, t);
            r.MaxEnergy = Mathf.Lerp(a.MaxEnergy, b.MaxEnergy, t);
            r.Defense = Mathf.Lerp(a.Defense, b.Defense, t);
            r.MoveSpeed = Mathf.Lerp(a.MoveSpeed, b.MoveSpeed, t);
            r.CooldownReduction = Mathf.Clamp(Mathf.Lerp(a.CooldownReduction, b.CooldownReduction, t), 0f, TankStats.MaxCooldownReduction);
            r.DamageRate = Mathf.Lerp(a.DamageRate, b.DamageRate, t);
            r.ArmorPenetration = Mathf.Clamp(Mathf.Lerp(a.ArmorPenetration, b.ArmorPenetration, t), 0f, TankStats.MaxArmorPenetration);
            return r;
        }
    }
}
