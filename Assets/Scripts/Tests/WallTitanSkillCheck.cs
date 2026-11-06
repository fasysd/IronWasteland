#if UNITY_EDITOR
using System.Collections;
using System.Text;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using IronWasteland.Tanks.WallTitan;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Kiểm tra tự động 8 kỹ năng của Wall Titan: năng lượng, hồi chiêu, sát thương,
    /// khiên và hiệu ứng hình ảnh. Chỉ chạy trong Editor.
    /// </summary>
    public class WallTitanSkillCheck : MonoBehaviour
    {
        private readonly StringBuilder m_Report = new StringBuilder();

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(1f);

            TankDefinition def = AssetDatabase.LoadAssetAtPath<TankDefinition>(
                "Assets/Prefabs/Tanks/WallTitan/WallTitan.asset");
            TankDefinition allyDef = AssetDatabase.LoadAssetAtPath<TankDefinition>(
                "Assets/Prefabs/Tanks/RedNova/RedNova.asset");

            if (def == null)
            {
                Debug.LogError("CHECK: không tìm thấy WallTitan.asset");
                yield break;
            }

            TankBrain tank = def.CreateTank(new TankBrainContext { Level = 90 });
            tank.transform.position = Vector3.zero;
            var brain = tank as TankBrain_WallTitan;

            TankBrain ally = allyDef != null ? allyDef.CreateTank(new TankBrainContext { Level = 90 }) : null;
            if (ally != null) ally.transform.position = new Vector3(2f, 0f, 0f);

            EnemyBrain dummy = SpawnDummy(new Vector3(6f, 0f, 0f));
            yield return new WaitForSeconds(0.3f);

            Debug.Log($"CHECK START WallTitan hp={brain.Stats.MaxHealth:0} atk={brain.Stats.Attack:0} def={brain.Stats.Defense:0} en={brain.Stats.MaxEnergy:0}");

            yield return Run(brain, dummy, ally, 0, -1, "Earth Break", false);
            yield return Run(brain, dummy, ally, 1, -1, "Earth Wall", false);
            yield return Run(brain, dummy, ally, 2, 0, "Stone Fortress", false);
            yield return Run(brain, dummy, ally, 2, 1, "Guardian Wall", true);
            yield return Run(brain, dummy, ally, 2, 2, "Earth Prison", false);
            yield return Run(brain, dummy, ally, 3, 0, "Immovable Titan", false);
            yield return Run(brain, dummy, ally, 3, 1, "Earth Guardian", true);
            yield return Run(brain, dummy, ally, 3, 2, "Earthquake", false);

            Debug.Log("CHECK DONE\n" + m_Report);
        }

        private IEnumerator Run(TankBrain_WallTitan brain, EnemyBrain dummy, TankBrain ally, int slot, int branch, string label, bool allySkill)
        {
            if (branch >= 0)
            {
                if (slot == 2) brain.Option1Branch = branch;
                else brain.Option2Branch = branch;
                yield return null;
            }

            brain.ResetAllSkillCooldowns();
            brain.GainEnergy(9999f);
            yield return null;

            float energyBefore = brain.CurrentEnergy;
            float hpBefore = dummy != null ? dummy.CurrentHealth : 0f;
            float shieldBefore = brain.CurrentShield;

            Vector2 target = allySkill && ally != null ? (Vector2)ally.transform.position : new Vector2(6f, 0f);
            brain.SetLookTarget(target);
            brain.UseSkill(slot);

            float energyAfter = brain.CurrentEnergy;
            float cd = brain.GetSkillCooldown(slot);

            yield return new WaitForSeconds(label == "Earthquake" || label == "Earth Prison" ? 2.6f : 0.6f);

            float hpAfter = dummy != null ? dummy.CurrentHealth : 0f;
            float damage = hpBefore - hpAfter;
            float shieldGain = brain.CurrentShield - shieldBefore;
            int vfxChildren = CountVfx(brain.transform);

            m_Report.AppendLine(string.Format(
                "{0,-16} EN {1:0}->{2:0} (-{3:0})  CD {4:0.00}  DMG {5:0}  SHIELD {6:0}  VFX {7}",
                label, energyBefore, energyAfter, energyBefore - energyAfter, cd, damage, shieldGain, vfxChildren));
        }

        private int CountVfx(Transform root)
        {
            int c = 0;
            foreach (Transform t in root)
                if (t.name.StartsWith("VFX_") && t.gameObject.activeSelf) c++;
            return c;
        }

        private EnemyBrain SpawnDummy(Vector3 pos)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy_Normal.prefab");
            if (prefab == null) return null;

            GameObject go = Instantiate(prefab, pos, Quaternion.identity);
            var brain = go.GetComponent<EnemyBrain>();
            if (brain != null)
            {
                EnemyStats stats = brain.Stats.Clone();
                stats.MaxHealth = 500000f;
                stats.Defense = 0f;
                brain.Initialize(stats);
            }
            return brain;
        }
    }
}
#endif
