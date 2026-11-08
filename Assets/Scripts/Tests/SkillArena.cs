using IronWasteland.Enemies;
using IronWasteland.Tanks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Sân test kỹ năng (dùng cho giai đoạn dev):
    /// - Tự spawn một vòng quái giả quanh xe để có mục tiêu bắn.
    /// - Đổi nhánh kỹ năng tùy chọn ngay lúc chạy: 1/2 đổi chiêu phụ 1 (slot L), 3/4 đổi chiêu phụ 2 (slot I).
    /// - HUD hiện máu / năng lượng / hồi chiêu 4 slot + tên chiêu theo nhánh đang chọn.
    ///
    /// Cách dùng: tạo GameObject trống trong Scene, gắn component này,
    /// gán Dummy Prefab = Assets/Prefabs/Enemies/Enemy_Normal.prefab, rồi bấm Play.
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillArena : MonoBehaviour
    {
        [Header("Quái giả (bia tập bắn)")]
        [Tooltip("Prefab quái dùng làm bia. Ví dụ: Assets/Prefabs/Enemies/Enemy_Normal.prefab")]
        [SerializeField] private EnemyBrain dummyPrefab;

        [SerializeField, Min(1)] private int dummyCount = 4;
        [SerializeField, Min(1f)] private float ringRadius = 6f;

        [Header("HUD")]
        [SerializeField] private bool showHud = true;
        [Tooltip("Tự sinh bia khi bắt đầu. Tắt để sân sạch; bấm [R] để sinh bia khi cần.")]
        [SerializeField] private bool spawnOnStart = false;

        private TankBrain m_Tank;
        private readonly List<EnemyBrain> m_Dummies = new List<EnemyBrain>();

        private GUIStyle m_Panel;
        private GUIStyle m_Title;
        private GUIStyle m_Line;

        private IEnumerator Start()
        {
            // Chờ GameController spawn xe tàng của người chơi.
            while (m_Tank == null)
            {
                m_Tank = FindAnyObjectByType<TankBrain>();
                yield return null;
            }

            // Chờ thêm 1 nhịp để Brain chạy xong Initialize/Start.
            yield return new WaitForSeconds(0.25f);

            // Mặc định không tự sinh bia để sân test sạch; bấm [R] khi cần.
            if (spawnOnStart)
                SpawnDummies();
        }

        private void OnDestroy()
        {
            ClearDummies();
        }

        private void Update()
        {
            if (m_Tank == null)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            ISkillBranchConfigurable configurable = m_Tank as ISkillBranchConfigurable;
            if (configurable != null)
            {
                // 1/2: đổi chiêu phụ 1 (slot L). 3/4: đổi chiêu phụ 2 (slot I).
                if (keyboard.digit1Key.wasPressedThisFrame)
                    configurable.Option1Branch = (configurable.Option1Branch + 1) % 3;

                if (keyboard.digit2Key.wasPressedThisFrame)
                    configurable.Option1Branch = (configurable.Option1Branch + 2) % 3;

                if (keyboard.digit3Key.wasPressedThisFrame)
                    configurable.Option2Branch = (configurable.Option2Branch + 1) % 3;

                if (keyboard.digit4Key.wasPressedThisFrame)
                    configurable.Option2Branch = (configurable.Option2Branch + 2) % 3;
            }

            // R: hồi lại bia. C: xoá hồi chiêu. H: hồi đầy máu + năng lượng.
            if (keyboard.rKey.wasPressedThisFrame)
            {
                ClearDummies();
                SpawnDummies();
            }

            if (keyboard.cKey.wasPressedThisFrame)
                m_Tank.ResetAllSkillCooldowns();

            if (keyboard.hKey.wasPressedThisFrame)
            {
                m_Tank.Heal(m_Tank.Stats.MaxHealth);
                m_Tank.GainEnergy(m_Tank.Stats.MaxEnergy);
            }
        }

        private void SpawnDummies()
        {
            if (dummyPrefab == null || m_Tank == null)
                return;

            Vector2 center = m_Tank.transform.position;
            int count = Mathf.Max(1, dummyCount);

            for (int i = 0; i < count; i++)
            {
                float angle = (360f / count) * i * Mathf.Deg2Rad;
                Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;

                EnemyBrain enemy = Instantiate(dummyPrefab, position, Quaternion.identity);
                if (enemy == null)
                    continue;

                // Giống StageManager: lấy chỉ số từ prefab rồi nạp lại để mỗi con có state riêng.
                EnemyStats stats = enemy.Stats != null ? enemy.Stats.Clone() : new EnemyStats();
                enemy.Initialize(stats);

                m_Dummies.Add(enemy);
            }
        }

        private void ClearDummies()
        {
            for (int i = 0; i < m_Dummies.Count; i++)
            {
                if (m_Dummies[i] != null)
                    Destroy(m_Dummies[i].gameObject);
            }

            m_Dummies.Clear();
        }

        private void EnsureStyles()
        {
            if (m_Panel != null)
                return;

            m_Panel = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 8, 8)
            };

            m_Title = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };

            m_Line = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        }

        private void OnGUI()
        {
            if (!showHud || m_Tank == null)
                return;

            EnsureStyles();

            GUILayout.BeginArea(new Rect(10f, 10f, 380f, 260f), m_Panel);

            GUILayout.Label($"{m_Tank.name}  —  Skill Arena", m_Title);
            GUILayout.Label(
                $"HP {m_Tank.CurrentHealth:0}/{m_Tank.Stats.MaxHealth:0}   " +
                $"EN {m_Tank.CurrentEnergy:0}/{m_Tank.Stats.MaxEnergy:0}   " +
                $"Khiên {m_Tank.CurrentShield:0}",
                m_Line);

            GUILayout.Space(4f);
            DrawSkillLine(0, "J");
            DrawSkillLine(1, "K");
            DrawSkillLine(2, "L");
            DrawSkillLine(3, "I");

            GUILayout.Space(4f);
            GUILayout.Label("[1]/[2] đổi chiêu phụ 1   [3]/[4] đổi chiêu phụ 2", m_Line);
            GUILayout.Label("[R] hồi bia   [C] xoá hồi chiêu   [H] hồi đầy HP/EN", m_Line);

            GUILayout.EndArea();
        }

        private void DrawSkillLine(int slotIndex, string key)
        {
            string skillName = m_Tank is ISkillBranchConfigurable configurable
                ? configurable.GetSkillName(slotIndex)
                : $"Skill {slotIndex}";

            float cooldown = m_Tank.GetSkillCooldown(slotIndex);
            string cooldownText = cooldown > 0.01f ? $"CD {cooldown:0.0}s" : "sẵn sàng";

            GUILayout.Label($"[{key}] {skillName,-18} {cooldownText}", m_Line);
        }
    }
}
