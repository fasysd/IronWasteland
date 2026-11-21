using System;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies.D1onk
{
    /// <summary>
    /// Không gian buồng lái pháo hạm BÊN TRONG (D1onk Mothership Sub-Arena):
    /// - Tọa độ độc lập trên cùng scene (mặc định 200, 100).
    /// - Giam giữ Tank (Solo: 1 Tank; Co-op: 2 Tank gần nhất).
    /// - Trái Tim Boss ở trung tâm buồng lái: 10% Boss Max HP, khiên Bất Hoại.
    /// - 4 cột năng lượng ở 4 góc: Trắng, Đỏ, Vàng, Xanh (500 HP/cột).
    /// - Sinh ngẫu nhiên 1 quái vật thường mỗi 5s.
    /// - Solo: Phá 3/4 cột -> Xóa khiên Bất Hoại của Trái Tim.
    /// - Co-op: Phá cột kích hoạt thử thách bên ngoài; hoàn thành 3 thử thách -> Xóa khiên Bất Hoại.
    /// - Tiêu diệt Trái Tim -> Hoàn thành Bùng Nổ Năng Lượng (Thắng).
    /// 
    /// RÀNG BUỘC COLLIDER:
    /// Tuyệt đối không gắn ColliderOwnerRef trỏ về Boss Brain trên bất kỳ đối tượng buồng lái nào!
    /// </summary>
    [DisallowMultipleComponent]
    public class D1onkMothershipSubArena : MonoBehaviour
    {
        [Header("Arena Bounds & Offsets")]
        [Tooltip("Tọa độ trung tâm buồng lái lõi pháo hạm.")]
        [SerializeField] private Vector2 arenaCenter = new Vector2(200f, 100f);

        [Tooltip("Bán kính sàn đấu buồng lái (m).")]
        [SerializeField, Min(5f)] private float arenaRadius = 14f;

        [Tooltip("Khoảng cách đặt 4 cột năng lượng từ tâm buồng lái (m).")]
        [SerializeField, Min(2f)] private float pillarOffsetDistance = 7f;

        [Header("Prefabs (Optional)")]
        [Tooltip("Prefab Trái Tim Boss (D1onkHeart).")]
        [SerializeField] private GameObject heartPrefab;

        [Tooltip("Prefab Cột Năng Lượng (D1onkEnergyPillar).")]
        [SerializeField] private GameObject pillarPrefab;

        [Tooltip("Danh sách Prefab quái thường để triệu hồi ngẫu nhiên mỗi 5s.")]
        [SerializeField] private GameObject[] normalEnemyPrefabs;

        private D1onkBrain m_Boss;
        private D1onkCoopChallenges m_CoopChallenges;
        private bool m_IsCoop;

        private readonly List<TankBrain> m_TrappedTanks = new List<TankBrain>();
        private readonly Dictionary<TankBrain, Vector2> m_SavedTankPositions = new Dictionary<TankBrain, Vector2>();

        private D1onkHeart m_Heart;
        private readonly List<D1onkEnergyPillar> m_Pillars = new List<D1onkEnergyPillar>();
        private readonly HashSet<D1onkPillarColor> m_DestroyedPillars = new HashSet<D1onkPillarColor>();
        private readonly List<EnemyBrain> m_SpawnedMinionsInside = new List<EnemyBrain>();

        private float m_MonsterSpawnTimer;
        private bool m_IsArenaActive;

        public Vector2 ArenaCenter => arenaCenter;
        public float ArenaRadius => arenaRadius;
        public IReadOnlyList<TankBrain> TrappedTanks => m_TrappedTanks;
        public D1onkHeart Heart => m_Heart;
        public IReadOnlyList<D1onkEnergyPillar> Pillars => m_Pillars;
        public int DestroyedPillarsCount => m_DestroyedPillars.Count;
        public bool IsArenaActive => m_IsArenaActive;
        public bool IsCoop => m_IsCoop;

        public void SetNormalEnemyPrefabs(GameObject[] prefabs)
        {
            if (prefabs != null && prefabs.Length > 0)
            {
                normalEnemyPrefabs = prefabs;
            }
        }

        public void InitializeArena(D1onkBrain boss, D1onkCoopChallenges challenges, bool isCoop, Vector2? customCenter = null)
        {
            m_Boss = boss;
            m_CoopChallenges = challenges;
            m_IsCoop = isCoop;

            if (customCenter.HasValue)
            {
                arenaCenter = customCenter.Value;
            }
            transform.position = arenaCenter;

            m_TrappedTanks.Clear();
            m_SavedTankPositions.Clear();
            m_DestroyedPillars.Clear();
            m_SpawnedMinionsInside.Clear();
            m_MonsterSpawnTimer = 0f;
            m_IsArenaActive = true;

            if (m_CoopChallenges != null)
            {
                m_CoopChallenges.OnThreeChallengesCompleted += HandleThreeCoopChallengesCompleted;
            }

            BuildVisualFloor();
            SpawnHeart();
            SpawnPillars();
        }

        private void BuildVisualFloor()
        {
            GameObject floorGo = new GameObject("D1onkMothership_Floor");
            floorGo.transform.SetParent(transform);
            floorGo.transform.localPosition = Vector3.zero;

            SpriteRenderer sr = floorGo.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.SoftCircle;
            sr.color = new Color(0.12f, 0.08f, 0.22f, 0.95f); // Tím kim loại pháo hạm
            sr.transform.localScale = Vector3.one * (arenaRadius * 2f);
            sr.sortingOrder = -10;
        }

        private void SpawnHeart()
        {
            float bossHp = m_Boss != null ? m_Boss.MaxHealth : (m_IsCoop ? 54000f : 27000f);
            float heartHp = bossHp * 0.10f; // 10% Max HP của Boss

            GameObject heartObj = null;
            if (heartPrefab != null)
            {
                heartObj = Instantiate(heartPrefab, arenaCenter, Quaternion.identity, transform);
                m_Heart = heartObj.GetComponent<D1onkHeart>();
            }
            else
            {
                heartObj = new GameObject("D1onkHeart");
                heartObj.transform.position = arenaCenter;
                heartObj.transform.SetParent(transform);
                m_Heart = heartObj.AddComponent<D1onkHeart>();
            }

            if (m_Heart != null)
            {
                m_Heart.InitializeHeart(m_Boss, this, heartHp);
            }
        }

        private void SpawnPillars()
        {
            m_Pillars.Clear();

            // 4 Cột ở 4 góc: Trắng (Top-Left), Đỏ (Top-Right), Vàng (Bottom-Left), Xanh (Bottom-Right)
            var configs = new[]
            {
                new { color = D1onkPillarColor.White, offset = new Vector2(-pillarOffsetDistance, pillarOffsetDistance) },
                new { color = D1onkPillarColor.Red, offset = new Vector2(pillarOffsetDistance, pillarOffsetDistance) },
                new { color = D1onkPillarColor.Yellow, offset = new Vector2(-pillarOffsetDistance, -pillarOffsetDistance) },
                new { color = D1onkPillarColor.Blue, offset = new Vector2(pillarOffsetDistance, -pillarOffsetDistance) }
            };

            for (int i = 0; i < configs.Length; i++)
            {
                var cfg = configs[i];
                Vector2 spawnPos = arenaCenter + cfg.offset;

                GameObject pillarObj = null;
                D1onkEnergyPillar pillar = null;

                if (pillarPrefab != null)
                {
                    pillarObj = Instantiate(pillarPrefab, spawnPos, Quaternion.identity, transform);
                    pillar = pillarObj.GetComponent<D1onkEnergyPillar>();
                }
                else
                {
                    pillarObj = new GameObject($"EnergyPillar_{cfg.color}");
                    pillarObj.transform.position = spawnPos;
                    pillarObj.transform.SetParent(transform);
                    pillar = pillarObj.AddComponent<D1onkEnergyPillar>();
                }

                if (pillar != null)
                {
                    pillar.InitializePillar(cfg.color, this, 500f);
                    m_Pillars.Add(pillar);
                }
            }
        }

        public void TrapTank(TankBrain tank)
        {
            if (tank == null || !tank.IsAlive) return;

            if (!m_SavedTankPositions.ContainsKey(tank))
            {
                m_SavedTankPositions[tank] = tank.transform.position;
            }

            if (!m_TrappedTanks.Contains(tank))
            {
                m_TrappedTanks.Add(tank);
            }

            // Đặt vị trí bên trong buồng lái
            int idx = m_TrappedTanks.IndexOf(tank);
            Vector2 offset = idx == 0 ? new Vector2(-2.5f, -3.5f) : new Vector2(2.5f, -3.5f);
            tank.transform.position = arenaCenter + offset;
        }

        public void OnPillarDestroyed(D1onkEnergyPillar pillar)
        {
            if (pillar == null || m_DestroyedPillars.Contains(pillar.PillarColor)) return;

            m_DestroyedPillars.Add(pillar.PillarColor);
            Debug.Log($"[D1onkMothershipSubArena] Cột năng lượng {pillar.PillarColor} bị phá hủy! (Tổng: {m_DestroyedPillars.Count}/4)");

            if (!m_IsCoop)
            {
                // CHẾ ĐỘ SOLO: Phá hủy 3 trong 4 Cột Năng Lượng lập tức gỡ Khiên Bất Hoại của Trái Tim
                if (m_DestroyedPillars.Count >= 3 && m_Heart != null && m_Heart.InvincibleShieldActive)
                {
                    Debug.Log("[D1onkMothershipSubArena] Chế độ Solo: Phá hủy 3/4 cột! Gỡ bỏ Khiên Bất Hoại của Trái Tim!");
                    m_Heart.RemoveInvincibleShield();
                }
            }
            else
            {
                // CHẾ ĐỘ CO-OP: Kích hoạt thử thách tương ứng ở BÊN NGOÀI
                if (m_CoopChallenges != null)
                {
                    m_CoopChallenges.ActivateChallenge(pillar.PillarColor);
                }
            }
        }

        private void HandleThreeCoopChallengesCompleted()
        {
            if (m_Heart != null && m_Heart.InvincibleShieldActive)
            {
                Debug.Log("[D1onkMothershipSubArena] Chế độ Co-op: 3 thử thách bên ngoài hoàn thành! Gỡ bỏ Khiên Bất Hoại của Trái Tim!");
                m_Heart.RemoveInvincibleShield();
            }
        }

        public void OnHeartKilled()
        {
            if (!m_IsArenaActive) return;

            Debug.Log("[D1onkMothershipSubArena] Trái Tim Boss ĐÃ BỊ TIÊU DIỆT! Kích hoạt chiến thắng Bùng Nổ Năng Lượng!");
            m_IsArenaActive = false;

            if (m_Boss != null)
            {
                m_Boss.CompleteBurstSuccess();
            }
        }

        public void ManualTick(float deltaTime)
        {
            if (!m_IsArenaActive) return;

            // Ràng buộc ranh giới buồng lái: giữ các Tank bị giam không thể dash/teleport ra ngoài bán kính arenaRadius
            for (int i = 0; i < m_TrappedTanks.Count; i++)
            {
                TankBrain t = m_TrappedTanks[i];
                if (t != null && t.IsAlive)
                {
                    Vector2 offset = (Vector2)t.transform.position - arenaCenter;
                    float maxR = Mathf.Max(1f, arenaRadius - 0.5f);
                    if (offset.sqrMagnitude > maxR * maxR)
                    {
                        t.transform.position = arenaCenter + offset.normalized * maxR;
                    }
                }
            }

            // Mỗi 5s xuất hiện thêm 1 quái vật thường ngẫu nhiên ở Vùng Đất Nguyên Tố bên trong buồng lái
            m_MonsterSpawnTimer += deltaTime;
            if (m_MonsterSpawnTimer >= 5.0f)
            {
                m_MonsterSpawnTimer = 0f;
                SpawnInsideMonster();
            }

            if (m_Heart != null && m_Heart.IsAlive)
            {
                m_Heart.ManualTick(deltaTime);
            }
        }

        public void SpawnInsideMonster()
        {
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * (arenaRadius * 0.6f);
            Vector2 spawnPos = arenaCenter + randomOffset;

            EnemyBrain monster = null;
            if (normalEnemyPrefabs != null && normalEnemyPrefabs.Length > 0)
            {
                int rndIdx = UnityEngine.Random.Range(0, normalEnemyPrefabs.Length);
                GameObject prefab = normalEnemyPrefabs[rndIdx];
                if (prefab != null)
                {
                    GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity, transform);
                    monster = go.GetComponent<EnemyBrain>();
                }
            }

            if (monster == null)
            {
                GameObject go = new GameObject($"CockpitMinion_{m_SpawnedMinionsInside.Count + 1}");
                go.transform.position = spawnPos;
                go.transform.SetParent(transform);
                monster = go.AddComponent<LightweightTestMinion>();
                monster.Initialize(new EnemyStats { MaxHealth = 150f, Attack = 25f });
            }

            if (monster != null)
            {
                m_SpawnedMinionsInside.Add(monster);
            }
        }

        /// <summary>
        /// Đưa toàn bộ Tank ở BÊN TRONG trở ra ngoài sân chính theo vị trí đã lưu.
        /// </summary>
        public void ReleaseTanks()
        {
            for (int i = 0; i < m_TrappedTanks.Count; i++)
            {
                TankBrain tank = m_TrappedTanks[i];
                if (tank != null && m_SavedTankPositions.TryGetValue(tank, out var pos))
                {
                    tank.transform.position = pos;
                }
            }
            m_TrappedTanks.Clear();
            m_SavedTankPositions.Clear();
        }

        public void CleanupArena()
        {
            m_IsArenaActive = false;

            if (m_CoopChallenges != null)
            {
                m_CoopChallenges.OnThreeChallengesCompleted -= HandleThreeCoopChallengesCompleted;
            }

            ReleaseTanks();

            for (int i = 0; i < m_Pillars.Count; i++)
            {
                if (m_Pillars[i] != null)
                {
                    if (Application.isPlaying) Destroy(m_Pillars[i].gameObject);
                    else DestroyImmediate(m_Pillars[i].gameObject);
                }
            }
            m_Pillars.Clear();

            for (int i = 0; i < m_SpawnedMinionsInside.Count; i++)
            {
                if (m_SpawnedMinionsInside[i] != null)
                {
                    if (Application.isPlaying) Destroy(m_SpawnedMinionsInside[i].gameObject);
                    else DestroyImmediate(m_SpawnedMinionsInside[i].gameObject);
                }
            }
            m_SpawnedMinionsInside.Clear();

            if (m_Heart != null)
            {
                if (Application.isPlaying) Destroy(m_Heart.gameObject);
                else DestroyImmediate(m_Heart.gameObject);
                m_Heart = null;
            }

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
