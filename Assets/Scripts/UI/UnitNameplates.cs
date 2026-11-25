using System.Collections.Generic;
using IronWasteland.Enemies;
using IronWasteland.Stages;
using IronWasteland.Tanks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IronWasteland.UI
{
    /// <summary>
    /// Nhãn tên + thanh máu nhỏ nổi trên đầu tank người chơi, quái và boss (giống mockup HUD).
    /// Dùng một canvas Screen Space Overlay chung: mỗi frame đổi toạ độ thế giới sang toạ độ màn hình,
    /// không tạo canvas World Space cho từng đơn vị.
    /// </summary>
    [DisallowMultipleComponent]
    public class UnitNameplates : MonoBehaviour
    {
        private class Entry
        {
            public RectTransform Root;
            public TMP_Text Name;
            public TMP_Text Tag;
            public Image HpBg;
            public Image HpFill;
            public Component Target;
        }

        [Header("Nguồn")]
        [Tooltip("Camera dùng để chiếu toạ độ. Để trống sẽ lấy Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("Nơi chứa các nhãn (thường là object con full màn hình của HUD).")]
        [SerializeField] private RectTransform container;

        [Header("Kích thước")]
        [SerializeField] private float nameFontSize = 15f;
        [SerializeField] private float bossFontSize = 17f;
        [SerializeField] private float barWidth = 96f;
        [SerializeField] private float barHeight = 5f;
        [SerializeField] private float heightOffset = 0.95f;

        [Tooltip("Chỉ hiện nhãn cho quái trong bán kính này quanh tank (đơn vị thế giới).")]
        [SerializeField] private float maxDistance = 16f;

        [Tooltip("Số nhãn quái tối đa hiện cùng lúc để không rối màn hình.")]
        [SerializeField] private int maxLabels = 6;

        [Header("Màu")]
        [SerializeField] private Color allyNameColor = new Color32(0xEA, 0xF2, 0xFF, 0xFF);
        [SerializeField] private Color allyBarColor = new Color32(0x3B, 0xD6, 0xFF, 0xFF);
        [SerializeField] private Color enemyNameColor = new Color32(0xE6, 0xE9, 0xEF, 0xFF);
        [SerializeField] private Color enemyBarColor = new Color32(0xFF, 0x52, 0x52, 0xFF);
        [SerializeField] private Color bossNameColor = new Color32(0xFF, 0x8A, 0x80, 0xFF);
        [SerializeField] private Color bossBarColor = new Color32(0xFF, 0x3B, 0x3B, 0xFF);
        [SerializeField] private Color tagColor = new Color32(0xFF, 0xD5, 0x4F, 0xFF);

        /// <summary>Tên tiếng Việt cho từng prefab quái (khoá = tên prefab, đã bỏ hậu tố "_spawned").</summary>
        private static readonly Dictionary<string, string> EnemyNames = new Dictionary<string, string>
        {
            ["KIenLuaKhongLo"] = "Kiến Lửa Khổng Lồ",
            ["KySinhThienThach"] = "Ký Sinh Thiên Thạch",
            ["MatTrangDen"] = "Mặt Trăng Đen",
            ["MatTroiDo"] = "Mặt Trời Đỏ",
            ["NamDocGaiThach"] = "Nấm Độc Gai Thạch",
            ["QuaiVatDamLay"] = "Quái Vật Đầm Lầy",
            ["QuaiVatTinhThach"] = "Quái Vật Tinh Thạch",
            ["SaoDen"] = "Sao Đen",
            ["TinhLinhNghiaDia"] = "Tinh Linh Nghĩa Địa",
            ["TinhLinhSamSetNho"] = "Tinh Linh Sấm Sét",
            ["XaThuTinhThe"] = "Xạ Thủ Tinh Thể",
            ["XenNuoc"] = "Xén Nước"
        };

        private readonly List<Entry> m_Entries = new List<Entry>();
        private readonly List<EnemyBrain> m_EnemyBuffer = new List<EnemyBrain>();
        private readonly List<Vector2> m_UsedLocal = new List<Vector2>();
        private readonly Dictionary<Component, Entry> m_ByTarget = new Dictionary<Component, Entry>();
        private float m_ScanTimer;

        private void Awake()
        {
            if (container == null) container = transform as RectTransform;
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null || container == null) return;

            m_ScanTimer -= Time.deltaTime;
            if (m_ScanTimer <= 0f)
            {
                m_ScanTimer = 0.25f;
                Rescan();
            }

            m_UsedLocal.Clear();
            PlacePlayer();
            PlaceEnemies();
            CleanupStaleEntries();
        }

        // ------------------------------------------------------------- đơn vị

        private void PlacePlayer()
        {
            GameController gc = FindAnyObjectByType<GameController>();
            TankBrain tank = gc != null ? gc.PlayerTank : null;

            if (IsStaleTarget(tank))
            {
                Hide(GetEntry(tank, true));
                return;
            }

            Entry entry = GetEntry(tank, true);
            if (entry == null) return;

            string tankName = gc.TankDefinition != null ? gc.TankDefinition.TankName : "TANK";
            entry.Name.text = tankName.ToUpperInvariant();
            entry.Name.color = allyNameColor;
            entry.Name.fontSize = nameFontSize;
            entry.Tag.text = "BẠN";
            entry.Tag.color = tagColor;

            float maxHealth = Mathf.Max(1f, tank.Stats.MaxHealth);
            SetBar(entry, tank.CurrentHealth / maxHealth, allyBarColor);
            Place(entry, tank.transform.position, 1.05f);
        }

        private void PlaceEnemies()
        {
            GameController gc = FindAnyObjectByType<GameController>();
            Vector2 tankPos = gc != null && gc.PlayerTank != null
                ? (Vector2)gc.PlayerTank.transform.position
                : Vector2.zero;

            EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);

            // Thứ tự FindObjectsByType không xác định, nên nếu cắt theo giới hạn maxLabels ngay
            // thì tập quái được hiện sẽ đổi tuỳ ý và ta có thể bỏ sót quái đứng ngay cạnh người chơi.
            // Sắp xếp gần trước để giới hạn chỉ rơi vào những con ở xa.
            m_EnemyBuffer.Clear();
            m_EnemyBuffer.AddRange(enemies);
            m_EnemyBuffer.Sort((a, b) =>
            {
                if (a == null) return b == null ? 0 : 1;
                if (b == null) return -1;

                // Boss luôn được ưu tiên giữ suất hiển thị.
                bool bossA = a.GetComponent<BossTag>() != null;
                bool bossB = b.GetComponent<BossTag>() != null;
                if (bossA != bossB) return bossA ? -1 : 1;

                return Vector2.SqrMagnitude((Vector2)a.transform.position - tankPos)
                    .CompareTo(Vector2.SqrMagnitude((Vector2)b.transform.position - tankPos));
            });

            int shown = 0;

            foreach (EnemyBrain enemy in m_EnemyBuffer)
            {
                if (enemy == null) continue;

                Entry entry = GetEntry(enemy, false);
                if (entry == null) continue;

                bool isBoss = enemy.GetComponent<BossTag>() != null;
                float distance = Vector2.Distance(tankPos, enemy.transform.position);

                bool tooFar = !isBoss && distance > maxDistance;
                bool overLimit = !isBoss && shown >= Mathf.Max(0, maxLabels);

                if (!enemy.ShowsNameplate || tooFar || overLimit)
                {
                    Hide(entry);
                    continue;
                }

                if (!isBoss) shown++;

                string display = enemy.DisplayName;
                display = string.IsNullOrEmpty(display) ? ResolveName(enemy.name) : ResolveName(display);

                entry.Name.text = display;
                entry.Name.fontSize = isBoss ? bossFontSize : nameFontSize;
                entry.Name.color = isBoss ? bossNameColor : enemyNameColor;
                entry.Tag.text = isBoss ? "BOSS" : string.Empty;

                float maxHealth = enemy.Stats != null ? Mathf.Max(1f, enemy.Stats.MaxHealth) : 1f;
                SetBar(entry, enemy.CurrentHealth / maxHealth, isBoss ? bossBarColor : enemyBarColor);
                Place(entry, enemy.transform.position, isBoss ? 1.6f : heightOffset);
            }
        }

        private static string ResolveName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Quái";

            string key = raw.Replace("_spawned", string.Empty).Replace("(Clone)", string.Empty).Trim();
            if (EnemyNames.TryGetValue(key, out string name)) return name;

            // Tên prefab kiểu "NormalEnemy_QuaiVatDamLay": bỏ tiền tố và tách CamelCase.
            int underscore = key.LastIndexOf('_');
            if (underscore >= 0 && underscore < key.Length - 1) key = key.Substring(underscore + 1);

            var sb = new System.Text.StringBuilder(key.Length + 8);
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(key[i - 1])) sb.Append(' ');
                sb.Append(c);
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------- vị trí

        private void Place(Entry entry, Vector3 world, float offset)
        {
            Vector3 screen = worldCamera.WorldToScreenPoint(world + Vector3.up * offset);
            if (screen.z <= 0f || screen.x < -100f || screen.x > Screen.width + 100f ||
                screen.y < -100f || screen.y > Screen.height + 100f)
            {
                Hide(entry);
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screen, null, out Vector2 local))
            {
                Hide(entry);
                return;
            }

            entry.Root.anchorMin = new Vector2(0.5f, 0.5f);
            entry.Root.anchorMax = new Vector2(0.5f, 0.5f);
            entry.Root.pivot = new Vector2(0.5f, 0f);

            // Nhãn của nhiều quái đứng sát nhau sẽ chồng lên nhau: đẩy dần lên theo bước 26 px.
            for (int guard = 0; guard < 8; guard++)
            {
                bool overlapped = false;
                for (int i = 0; i < m_UsedLocal.Count; i++)
                {
                    if (Mathf.Abs(m_UsedLocal[i].x - local.x) < 70f && Mathf.Abs(m_UsedLocal[i].y - local.y) < 22f)
                    {
                        local.y += 26f;
                        overlapped = true;
                        break;
                    }
                }

                if (!overlapped) break;
            }

            m_UsedLocal.Add(local);
            entry.Root.anchoredPosition = local;
            if (!entry.Root.gameObject.activeSelf) entry.Root.gameObject.SetActive(true);
        }

        private static void Hide(Entry entry)
        {
            if (entry != null && entry.Root != null && entry.Root.gameObject.activeSelf)
                entry.Root.gameObject.SetActive(false);
        }

        private static void SetBar(Entry entry, float fill, Color color)
        {
            if (entry.HpFill != null)
            {
                entry.HpFill.fillAmount = Mathf.Clamp01(fill);
                entry.HpFill.color = color;
            }
        }

        // ------------------------------------------------------------- quản lý entry

        private void Rescan()
        {
            // Giữ chỗ cho hook quét định kỳ; việc ẩn entry cũ do CleanupStaleEntries() lo mỗi frame.
        }

        /// <summary>
        /// Ẩn nhãn của mục tiêu đã bị huỷ hoặc đã chết/bị đưa về pool.
        /// Quái chết được EnemySpawner gọi SetActive(false) rồi cất vào pool, mà
        /// FindObjectsByType mặc định bỏ qua object inactive — nên nếu chỉ dựa vào
        /// vòng lặp mỗi frame thì nhãn của nó sẽ treo lại trên màn hình.
        /// </summary>
        internal void CleanupStaleEntries()
        {
            for (int i = 0; i < m_Entries.Count; i++)
            {
                Entry entry = m_Entries[i];
                if (IsStaleTarget(entry.Target)) Hide(entry);
            }
        }

        /// <summary>
        /// Mục tiêu không còn hiển thị được nữa: đã bị huỷ, đã chết, hoặc đã bị đưa về pool
        /// (EnemySpawner gọi SetActive(false) nên nó biến mất khỏi FindObjectsByType).
        /// </summary>
        private static bool IsStaleTarget(Component target)
        {
            if (target == null) return true;
            if (!target.gameObject.activeInHierarchy) return true;

            switch (target)
            {
                case EnemyBrain enemy:
                    return !enemy.IsAlive;
                case TankBrain tank:
                    return !tank.IsAlive;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Hook cho self-check: lấy (hoặc tạo) nhãn của một mục tiêu và trả về RectTransform gốc.
        /// </summary>
        internal RectTransform GetOrCreateEntryRoot(Component target)
        {
            Entry entry = GetEntry(target, false);
            return entry != null ? entry.Root : null;
        }

        /// <summary>Hook cho self-check: nhãn của mục tiêu này có đang hiện trên màn hình không.</summary>
        internal bool HasVisibleNameplate(Component target)
        {
            if (target == null) return false;
            return m_ByTarget.TryGetValue(target, out Entry entry)
                && entry.Root != null
                && entry.Root.gameObject.activeSelf;
        }

        private Entry GetEntry(Component target, bool isPlayer)
        {
            if (target == null) return null;

            // So sánh theo tham chiếu component (không dùng GetInstanceID vì Unity 6.5 đã đánh dấu obsolete).
            if (m_ByTarget.TryGetValue(target, out Entry existing))
            {
                existing.Target = target;
                return existing;
            }

            Entry created = CreateEntry();
            created.Target = target;
            m_ByTarget[target] = created;
            m_Entries.Add(created);
            return created;
        }

        /// <summary>Viền đen mảnh để chữ đọc được trên mọi loại nền.</summary>
        private static void AddOutline(TMP_Text text)
        {
            text.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
            text.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.9f));
        }

        private Entry CreateEntry()
        {
            if (container == null) container = transform as RectTransform;

            var go = new GameObject("Nameplate", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(container, false);
            rt.sizeDelta = new Vector2(140f, 34f);

            Entry entry = new Entry { Root = rt };

            RectTransform nameRt = new GameObject("Name", typeof(RectTransform)).GetComponent<RectTransform>();
            nameRt.SetParent(rt, false);
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = Vector2.zero;
            nameRt.sizeDelta = new Vector2(0f, 18f);

            entry.Name = nameRt.gameObject.AddComponent<TextMeshProUGUI>();
            entry.Name.fontSize = nameFontSize;
            entry.Name.alignment = TextAlignmentOptions.Center;
            entry.Name.fontStyle = FontStyles.Bold;
            entry.Name.raycastTarget = false;
            entry.Name.enableWordWrapping = false;
            entry.Name.overflowMode = TextOverflowModes.Overflow;
            entry.Name.enableAutoSizing = false;
            if (TMP_Settings.defaultFontAsset != null) entry.Name.font = TMP_Settings.defaultFontAsset;
            AddOutline(entry.Name);

            RectTransform tagRt = new GameObject("Tag", typeof(RectTransform)).GetComponent<RectTransform>();
            tagRt.SetParent(rt, false);
            tagRt.anchorMin = new Vector2(0.5f, 1f);
            tagRt.anchorMax = new Vector2(0.5f, 1f);
            tagRt.pivot = new Vector2(0.5f, 1f);
            tagRt.anchoredPosition = new Vector2(0f, -16f);
            tagRt.sizeDelta = new Vector2(80f, 14f);

            entry.Tag = tagRt.gameObject.AddComponent<TextMeshProUGUI>();
            entry.Tag.fontSize = 10f;
            entry.Tag.alignment = TextAlignmentOptions.Center;
            entry.Tag.fontStyle = FontStyles.Bold;
            entry.Tag.raycastTarget = false;
            entry.Tag.enableWordWrapping = false;
            if (TMP_Settings.defaultFontAsset != null) entry.Tag.font = TMP_Settings.defaultFontAsset;
            AddOutline(entry.Tag);

            RectTransform barRt = new GameObject("HpBg", typeof(RectTransform)).GetComponent<RectTransform>();
            barRt.SetParent(rt, false);
            barRt.anchorMin = new Vector2(0.5f, 1f);
            barRt.anchorMax = new Vector2(0.5f, 1f);
            barRt.pivot = new Vector2(0.5f, 1f);
            barRt.anchoredPosition = new Vector2(0f, -30f);
            barRt.sizeDelta = new Vector2(barWidth, barHeight);

            entry.HpBg = barRt.gameObject.AddComponent<Image>();
            entry.HpBg.color = new Color(0f, 0f, 0f, 0.65f);
            entry.HpBg.raycastTarget = false;

            RectTransform fillRt = new GameObject("HpFill", typeof(RectTransform)).GetComponent<RectTransform>();
            fillRt.SetParent(barRt, false);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(1f, 1f);
            fillRt.offsetMax = new Vector2(-1f, -1f);

            entry.HpFill = fillRt.gameObject.AddComponent<Image>();
            entry.HpFill.sprite = UISprites.Square();
            entry.HpFill.color = enemyBarColor;
            entry.HpFill.raycastTarget = false;
            entry.HpFill.type = Image.Type.Filled;
            entry.HpFill.fillMethod = Image.FillMethod.Horizontal;
            entry.HpFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            go.SetActive(false);
            return entry;
        }
    }
}
