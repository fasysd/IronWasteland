#if UNITY_EDITOR
using System.Collections;
using System.Text;
using IronWasteland.Enemies;
using IronWasteland.Tanks;
using IronWasteland.Tanks.RedNova;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Kiểm tra tự động 8 kỹ năng của Red Nova: năng lượng tiêu hao, hồi chiêu,
    /// sát thương lên bia và hiệu ứng hình ảnh có được tạo ra hay không.
    /// Chỉ dùng trong Editor: thêm component này vào 1 GameObject rồi bấm Play.
    /// </summary>
    public class RedNovaSkillCheck : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private StringBuilder m_Report = new StringBuilder();

        private IEnumerator Start()
        {
            if (!runOnStart)
                yield break;

            yield return new WaitForSeconds(1f);

            TankDefinition def = AssetDatabase.LoadAssetAtPath<TankDefinition>(
                "Assets/Prefabs/Tanks/RedNova/RedNova.asset");

            if (def == null)
            {
                Debug.LogError("CHECK: không tìm thấy RedNova.asset");
                yield break;
            }

            TankBrain tank = def.CreateTank(new TankBrainContext { Level = 90 });
            tank.transform.position = new Vector3(0f, 0f, 0f);

            var brain = tank as TankBrain_RedNova;
            var redVfx = tank.GetComponent<RedNovaVfx>();

            // Bia: 1 quái đứng yên ở xa, nhiều máu để không chết.
            EnemyBrain dummy = SpawnDummy(new Vector3(6f, 0f, 0f));

            yield return new WaitForSeconds(0.3f);

            Debug.Log($"CHECK START tank atk={brain.Stats.Attack:0} hp={brain.Stats.MaxHealth:0} en={brain.Stats.MaxEnergy:0} vfx={(redVfx != null)}");

            // ---- Slot 0: Nova Shot (+10 EN, CD 0.5) ----
            yield return Run(brain, dummy, 0, -1, "Nova Shot");

            // ---- Slot 1: Triple Nova (40 EN, CD 5) ----
            yield return Run(brain, dummy, 1, -1, "Triple Nova");

            // ---- Slot 2 pool ----
            yield return Run(brain, dummy, 2, 0, "Overdrive");
            yield return Run(brain, dummy, 2, 1, "Laser Cannon");
            yield return Run(brain, dummy, 2, 2, "Execution Core");

            // ---- Slot 3 pool ----
            yield return Run(brain, dummy, 3, 0, "Nova Barrage");
            yield return Run(brain, dummy, 3, 1, "Nova Pierce");
            yield return Run(brain, dummy, 3, 2, "Nova Rush");

            Debug.Log("CHECK DONE\n" + m_Report);
        }

        private IEnumerator Run(TankBrain_RedNova brain, EnemyBrain dummy, int slot, int branch, string label)
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
            Vector3 camPos = brain.transform.position;

            float hpMax = dummy != null ? dummy.Stats.MaxHealth : 0f;
            brain.SetLookTarget(dummy != null ? (Vector2)dummy.transform.position : new Vector2(6f, 0f));
            brain.UseSkill(slot);

            float energyAfter = brain.CurrentEnergy;
            float cd = brain.GetSkillCooldown(slot);

            // đợi đòn đánh bay tới nơi (barrage/pierce/tick)
            yield return new WaitForSeconds(label == "Nova Barrage" || label == "Nova Pierce" ? 2.6f : 0.5f);

            float hpAfter = dummy != null ? dummy.CurrentHealth : 0f;
            float damage = hpBefore - hpAfter;

            int vfxChildren = CountVfx(brain.transform);
            string status = damage > 0f ? "OK" : (cd > 0f ? "COOLDOWN-ONLY" : "NO-EFFECT");
            m_Report.AppendLine(
                string.Format("{0,-16} EN {1:0}->{2:0} (-{3:0})  CD {4:0.00}  DMG {5:0}/{6:0}  VFX {7}  {8}",
                    label, energyBefore, energyAfter, energyBefore - energyAfter, cd, damage, hpMax, vfxChildren, status));

            // dọn hiệu ứng tạm trên bản đồ
            yield return null;
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
                // Bia máu rất cao để không chết giữa chừng, đo được sát thương của cả 8 chiêu.
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
