using System;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Tanks;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Enemies.ElementalReaper
{
    [Serializable]
    public class ElementalAltarData
    {
        public ElementalType element;
        public Vector2 center;
        public Color floorColor;
        [NonSerialized] public bool isActive;
        [NonSerialized] public TankBrain trappedTank;
        [NonSerialized] public Vector2 savedTankPosition;
        [NonSerialized] public ElementalReaperClone clone;
        [NonSerialized] public bool isCloneDefeated;
        [NonSerialized] public GameObject floorObject;
    }

    /// <summary>
    /// Quản lý cụm 3 Tế Đàn Sub-Arena Tam Nguyên Tố (Hỏa / Độc / Thủy):
    /// - Hỏa: (150, 100) — Tế đàn dung nham đỏ
    /// - Độc: (150, 150) — Tế đàn đầm lầy tím lục
    /// - Thủy: (150, 200) — Tế đàn xoáy nước lam
    /// - Chế độ Solo: Chỉ kích hoạt duy nhất Tế Đàn Hỏa, 2 tế đàn kia không kích hoạt.
    /// - Chế độ Co-op:
    ///   + Tank gần nhất -> Hỏa (150, 100)
    ///   + Tank xa nhất -> Độc (150, 150)
    ///   + Tank thứ 3 -> Thủy (150, 200)
    ///   + Tank thứ 4 (nếu có) -> ở lại sân chính
    /// - Sinh Tinh thể nguyên tố định kỳ mỗi 4s trong các tế đàn đang hoạt động.
    /// - Khi Bản sao của một tế đàn chết: lập tức giải phóng Tank về vị trí cũ trên sân chính.
    /// </summary>
    [DisallowMultipleComponent]
    public class ElementalAltarsSubArena : MonoBehaviour
    {
        [Header("Altar Configurations")]
        [Tooltip("Bán kính mỗi tế đàn (m).")]
        [SerializeField, Min(4f)] private float altarRadius = 8.0f;

        [Tooltip("Độ lệch vị trí đặt Tank trong tế đàn.")]
        [SerializeField] private Vector2 tankSpawnOffset = new Vector2(0f, -3f);

        [Tooltip("Độ lệch vị trí sinh Bản sao trong tế đàn.")]
        [SerializeField] private Vector2 cloneSpawnOffset = new Vector2(0f, 2f);

        [Header("Crystals System")]
        [Tooltip("Chu kỳ sinh tinh thể nguyên tố trong tế đàn (giây). GDD: 4s.")]
        [SerializeField, Min(1f)] private float crystalSpawnInterval = 4.0f;

        [Tooltip("Prefab Tinh thể nguyên tố (ElementalCrystal).")]
        [SerializeField] private GameObject crystalPrefab;

        [Header("Clone Prefab")]
        [Tooltip("Prefab Bản sao Tử Thần Nguyên Tố (ElementalReaperClone).")]
        [SerializeField] private GameObject clonePrefab;

        [Header("Altars Data")]
        [SerializeField] private ElementalAltarData fireAltar = new ElementalAltarData
        {
            element = ElementalType.Fire,
            center = new Vector2(150f, 100f),
            floorColor = new Color(0.8f, 0.2f, 0.05f, 0.9f)
        };

        [SerializeField] private ElementalAltarData poisonAltar = new ElementalAltarData
        {
            element = ElementalType.Poison,
            center = new Vector2(150f, 150f),
            floorColor = new Color(0.15f, 0.5f, 0.15f, 0.9f)
        };

        [SerializeField] private ElementalAltarData waterAltar = new ElementalAltarData
        {
            element = ElementalType.Water,
            center = new Vector2(150f, 200f),
            floorColor = new Color(0.05f, 0.35f, 0.75f, 0.9f)
        };

        private readonly List<ElementalCrystal> m_ActiveCrystals = new List<ElementalCrystal>();
        private readonly List<ElementalAltarData> m_AllAltars = new List<ElementalAltarData>();
        private ElementalReaperBrain m_Boss;
        private float m_CrystalTimer;
        private bool m_IsActive;

        public float AltarRadius => altarRadius;
        public ElementalAltarData FireAltar => fireAltar;
        public ElementalAltarData PoisonAltar => poisonAltar;
        public ElementalAltarData WaterAltar => waterAltar;
        public IReadOnlyList<ElementalAltarData> AllAltars => m_AllAltars;
        public GameObject ClonePrefab { get => clonePrefab; set => clonePrefab = value; }
        public GameObject CrystalPrefab { get => crystalPrefab; set => crystalPrefab = value; }

        private void Awake()
        {
            m_AllAltars.Clear();
            m_AllAltars.Add(fireAltar);
            m_AllAltars.Add(poisonAltar);
            m_AllAltars.Add(waterAltar);
        }

        public void InitializeArena(ElementalReaperBrain boss)
        {
            m_Boss = boss;
            m_CrystalTimer = 0f;
            m_IsActive = true;
        }

        /// <summary>
        /// Bắt đầu phân bổ Tank vào tế đàn và sinh Bản sao theo chế độ Solo hoặc Co-op.
        /// </summary>
        public void StartBurstPhase(List<TankBrain> candidateTanks, EnemyStats cloneStats)
        {
            if (m_Boss == null) return;
            m_IsActive = true;

            // Lọc các Tank còn sống
            List<TankBrain> livingTanks = new List<TankBrain>();
            if (candidateTanks != null)
            {
                for (int i = 0; i < candidateTanks.Count; i++)
                {
                    if (candidateTanks[i] != null && candidateTanks[i].IsAlive)
                    {
                        livingTanks.Add(candidateTanks[i]);
                    }
                }
            }

            bool isSolo = livingTanks.Count <= 1;

            if (isSolo)
            {
                // Solo: Chỉ kích hoạt duy nhất Tế Đàn Hỏa
                TankBrain soloTank = livingTanks.Count > 0 ? livingTanks[0] : null;
                SetupAltar(fireAltar, soloTank, cloneStats);
                fireAltar.isActive = true;
                poisonAltar.isActive = false;
                waterAltar.isActive = false;
            }
            else
            {
                // Co-op: Phân bổ
                // 1. Tank gần nhất vào Hỏa
                Vector2 bossPos = m_Boss.transform.position;
                livingTanks.Sort((a, b) =>
                    Vector2.Distance(a.transform.position, bossPos).CompareTo(Vector2.Distance(b.transform.position, bossPos)));

                TankBrain nearestTank = livingTanks[0];
                SetupAltar(fireAltar, nearestTank, cloneStats);
                fireAltar.isActive = true;

                // 2. Tank xa nhất vào Độc
                TankBrain furthestTank = livingTanks[livingTanks.Count - 1];
                SetupAltar(poisonAltar, furthestTank, cloneStats);
                poisonAltar.isActive = true;

                // 3. Tank thứ 3 vào Thủy (nếu có)
                if (livingTanks.Count >= 3)
                {
                    TankBrain thirdTank = livingTanks[1]; // vị trí ở giữa
                    SetupAltar(waterAltar, thirdTank, cloneStats);
                    waterAltar.isActive = true;
                }
                else
                {
                    waterAltar.isActive = false;
                }
            }
        }

        private void SetupAltar(ElementalAltarData altar, TankBrain tank, EnemyStats cloneStats)
        {
            if (altar == null) return;

            // 1. Tạo sàn visual nếu chưa có
            BuildAltarFloor(altar);

            altar.isCloneDefeated = false;

            // 2. Kéo Tank vào nếu có
            if (tank != null && tank.IsAlive)
            {
                altar.trappedTank = tank;
                altar.savedTankPosition = tank.transform.position;
                tank.transform.position = altar.center + tankSpawnOffset;
            }

            // 3. Sinh Bản sao
            Vector2 clonePos = altar.center + cloneSpawnOffset;
            if (clonePrefab != null)
            {
                GameObject cloneGo = Instantiate(clonePrefab, clonePos, Quaternion.identity);
                altar.clone = cloneGo.GetComponent<ElementalReaperClone>();
                if (altar.clone != null)
                {
                    altar.clone.InitializeClone(this, tank, altar.element, cloneStats);
                    altar.clone.OnCloneDefeated += (c) => HandleCloneDefeated(altar);
                }
            }
            else
            {
                Debug.LogWarning($"[ElementalAltarsSubArena] clonePrefab is null on '{gameObject.name}'!");
            }
        }

        private void BuildAltarFloor(ElementalAltarData altar)
        {
            if (altar.floorObject != null) return;

            GameObject floorGo = new GameObject($"AltarFloor_{altar.element}");
            floorGo.transform.SetParent(transform);
            floorGo.transform.position = altar.center;

            SpriteRenderer sr = floorGo.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.SoftCircle;
            sr.color = altar.floorColor;
            sr.transform.localScale = Vector3.one * (altarRadius * 2f);
            sr.sortingOrder = -10;

            altar.floorObject = floorGo;
        }

        private void HandleCloneDefeated(ElementalAltarData altar)
        {
            if (altar == null) return;
            altar.isCloneDefeated = true;

            // Giải phóng Tank của tế đàn này về sân chính
            if (altar.trappedTank != null && altar.trappedTank.IsAlive)
            {
                altar.trappedTank.transform.position = altar.savedTankPosition;
                altar.trappedTank = null;
            }
        }

        private void Update()
        {
            if (!m_IsActive) return;

            m_CrystalTimer += Time.deltaTime;
            if (m_CrystalTimer >= crystalSpawnInterval)
            {
                m_CrystalTimer = 0f;
                SpawnCrystalsBatch();
            }
        }

        private void SpawnCrystalsBatch()
        {
            if (crystalPrefab == null) return;

            for (int i = 0; i < m_AllAltars.Count; i++)
            {
                ElementalAltarData altar = m_AllAltars[i];
                if (!altar.isActive || altar.isCloneDefeated || altar.clone == null || !altar.clone.IsAlive)
                {
                    continue;
                }

                // Chọn ngẫu nhiên hình dạng (Tròn / Vuông / Tam Giác)
                CrystalShape shape = (CrystalShape)UnityEngine.Random.Range(0, 3);
                Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * (altarRadius * 0.5f);
                Vector2 spawnPos = altar.center + randomOffset;

                GameObject crystalGo = Instantiate(crystalPrefab, spawnPos, Quaternion.identity);
                ElementalCrystal crystal = crystalGo.GetComponent<ElementalCrystal>();
                if (crystal != null)
                {
                    crystal.SetupCrystal(shape, altar.element, altar.clone);
                    m_ActiveCrystals.Add(crystal);
                }
            }
        }

        /// <summary>
        /// Kiểm tra toàn bộ Bản sao tại các tế đàn đang kích hoạt đã bị tiêu diệt hay chưa.
        /// </summary>
        public bool AreAllClonesDefeated()
        {
            for (int i = 0; i < m_AllAltars.Count; i++)
            {
                ElementalAltarData altar = m_AllAltars[i];
                if (altar.isActive && !altar.isCloneDefeated)
                {
                    if (altar.clone != null && altar.clone.IsAlive)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Đếm số Bản sao còn sống tại các tế đàn đang kích hoạt (phục vụ tính sát thương phạt).
        /// </summary>
        public int CountLivingClones()
        {
            int count = 0;
            for (int i = 0; i < m_AllAltars.Count; i++)
            {
                ElementalAltarData altar = m_AllAltars[i];
                if (altar.isActive && !altar.isCloneDefeated)
                {
                    if (altar.clone != null && altar.clone.IsAlive)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Dọn dẹp Sub-Arena: hoàn trả toàn bộ Tank còn lại về sân chính và xóa các đối tượng sinh ra.
        /// </summary>
        public void CleanupArena()
        {
            m_IsActive = false;

            // 1. Hoàn trả toàn bộ Tank về vị trí cũ
            for (int i = 0; i < m_AllAltars.Count; i++)
            {
                ElementalAltarData altar = m_AllAltars[i];
                if (altar.trappedTank != null)
                {
                    altar.trappedTank.transform.position = altar.savedTankPosition;
                    altar.trappedTank = null;
                }

                if (altar.clone != null)
                {
                    if (Application.isPlaying) Destroy(altar.clone.gameObject);
                    else DestroyImmediate(altar.clone.gameObject);
                    altar.clone = null;
                }

                if (altar.floorObject != null)
                {
                    if (Application.isPlaying) Destroy(altar.floorObject);
                    else DestroyImmediate(altar.floorObject);
                    altar.floorObject = null;
                }

                altar.isActive = false;
                altar.isCloneDefeated = false;
            }

            // 2. Dọn dẹp tinh thể
            for (int i = 0; i < m_ActiveCrystals.Count; i++)
            {
                if (m_ActiveCrystals[i] != null)
                {
                    if (Application.isPlaying) Destroy(m_ActiveCrystals[i].gameObject);
                    else DestroyImmediate(m_ActiveCrystals[i].gameObject);
                }
            }
            m_ActiveCrystals.Clear();
        }

        private void OnDestroy()
        {
            CleanupArena();
        }
    }
}
