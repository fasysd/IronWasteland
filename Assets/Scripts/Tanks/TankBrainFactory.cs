using UnityEngine;

namespace IronWasteland.Tanks
{
    /// <summary>
    /// ScriptableObject tao TankBrain. Inspector la noi designer chinh sua chi so.
    /// Chi "Level" duoc truyen tu ben ngoi (vd Player), cac chi so khac lay tu day.
    /// </summary>
    [CreateAssetMenu(menuName = "IronWasteland/Tank/Tank Brain Factory", fileName = "TankBrainFactory")]
    public class TankBrainFactory : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("Prefab chua TankBrain + TankModel.")]
        [SerializeField] private TankBrain tankPrefab;

        [Header("Base Stats (Level 1)")]
        [SerializeField] private TankStats baseStats = new TankStats();

        [Header("Growth per Level")]
        [SerializeField] private float attackPerLevel = 1f;
        [SerializeField] private float maxHealthPerLevel = 8f;
        [SerializeField] private float maxEnergyPerLevel = 4f;
        [SerializeField] private float defensePerLevel = 0.4f;
        [SerializeField] private float moveSpeedPerLevel = 0.05f;
        [SerializeField] private float cooldownReductionPerLevel = 0.004f;
        [SerializeField] private float damageMultiplierPerLevel = 0.01f;
        [SerializeField] private float armorPenetrationPerLevel = 0.003f;

        public TankBrain TankPrefab => tankPrefab;

        /// <summary>Tao TankBrain moi theo Level va cac chi so thiet ke trong Inspector.</summary>
        public TankBrain CreateTank(int level, Vector3 position, Transform parent = null)
        {
            if (tankPrefab == null)
            {
                Debug.LogError($"[{nameof(TankBrainFactory)}] Chua gan TankPrefab.");
                return null;
            }

            level = Mathf.Clamp(level, TankBrain.MinLevel, TankBrain.MaxLevel);

            TankBrain brain = Instantiate(tankPrefab, position, Quaternion.identity, parent);
            brain.Initialize(level, BuildStats(level));

            return brain;
        }

        /// <summary>Tinh chi so theo Level (gia tri co so + luong tang moi cap).</summary>
        public TankStats BuildStats(int level)
        {
            int steps = Mathf.Clamp(level, TankBrain.MinLevel, TankBrain.MaxLevel) - TankBrain.MinLevel;

            TankStats result = baseStats.Clone();
            result.Attack += attackPerLevel * steps;
            result.MaxHealth += maxHealthPerLevel * steps;
            result.MaxEnergy += maxEnergyPerLevel * steps;
            result.Defense += defensePerLevel * steps;
            result.MoveSpeed += moveSpeedPerLevel * steps;
            result.CooldownReduction += cooldownReductionPerLevel * steps;
            result.DamageMultiplier += damageMultiplierPerLevel * steps;
            result.ArmorPenetration += armorPenetrationPerLevel * steps;

            return result;
        }
    }
}