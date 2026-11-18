using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies;
using IronWasteland.Stages;
using IronWasteland.Tanks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IronWasteland.UI
{
    /// <summary>
    /// Bàn đồ chiến thuật góc trên-phải: gộp khung + nền bản đồ của màn đang chơi + lưới radar + mũi tên
    /// vị trí. Nền bản đồ lấy đúng sprite của object "Background" trong scene, nên mỗi màn hiện đúng
    /// địa hình của mình. Mũi tên xanh = tank người chơi (và đồng đội nếu có), chấm đỏ = quái/boss.
    /// Bấm M để bật/tắt.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinimapUI : MonoBehaviour
    {
        [Header("Khung bản đồ")]
        [Tooltip("Vùng đặt chấm + nền bản đồ (MapView bên trong panel).")]
        [SerializeField] private RectTransform mapView;

        [Tooltip("Ảnh nền bản đồ — tự đổi sang sprite nền của màn đang chơi.")]
        [SerializeField] private Image mapBackground;

        [Tooltip("Lưới radar phủ lên nền (có thể để trống).")]
        [SerializeField] private Image mapGrid;

        [Tooltip("Khung xanh = vùng camera đang nhìn. Tự bám theo camera mỗi frame.")]
        [SerializeField] private Image viewFrame;

        [Tooltip("Camera dùng để vẽ khung nhìn. Để trống = Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("Nền dự phòng khi màn không có object Background.")]
        [SerializeField] private Sprite fallbackBackground;

        [Tooltip("Object bật/tắt khi bấm M. Để trống thì dùng chính object này.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Bấm phím này để bật/tắt bản đồ.")]
        [SerializeField] private Key toggleKey = Key.M;

        [Header("Mũi tên / chấm")]
        [SerializeField] private Sprite blipPlayer;
        [SerializeField] private Sprite blipTeammate;
        [SerializeField] private Sprite blipEnemy;
        [SerializeField] private Sprite blipBoss;

        [Tooltip("Kích thước chấm quái (pixel UI).")]
        [SerializeField] private float blipSize = 18f;

        [Tooltip("Kích thước mũi tên tank người chơi.")]
        [SerializeField] private float playerBlipSize = 34f;

        [Tooltip("Kích thước mũi tên đồng đội.")]
        [SerializeField] private float teammateBlipSize = 30f;

        [Tooltip("Kích thước chấm boss.")]
        [SerializeField] private float bossBlipSize = 28f;

        [Tooltip("Hướng gốc của ảnh mũi tên (độ). Ảnh mặc định chĩa lên-phải 45°.")]
        [SerializeField] private float blipBaseAngle = 45f;

        [Tooltip("Giới hạn số chấm quái để bản đồ không bị rối.")]
        [SerializeField] private int maxEnemyBlips = 40;

        private readonly List<Blip> m_Pool = new List<Blip>();
        private int m_Used;
        private Rect m_Bounds;
        private bool m_HasBounds;
        private bool m_BackgroundApplied;

        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            if (mapView == null) mapView = transform as RectTransform;

            if (mapView != null && mapView.GetComponent<RectMask2D>() == null)
                mapView.gameObject.AddComponent<RectMask2D>();
        }

        private void Start()
        {
            RefreshBounds();
            ApplyStageBackground();
        }

        private void Update()
        {
            if (toggleKey != Key.None && Keyboard.current != null &&
                Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                ToggleMap();
            }

            if (panelRoot != null && !panelRoot.activeSelf) return;

            if (!m_HasBounds) RefreshBounds();
            if (!m_BackgroundApplied) ApplyStageBackground();

            DrawViewFrame();
            Draw();
        }

        /// <summary>Bật/tắt panel bản đồ (dùng cho phím M và cho nút bấm nếu cần).</summary>
        public void ToggleMap()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(!panelRoot.activeSelf);
        }

        /// <summary>
        /// Vẽ khung xanh đúng vùng camera đang nhìn: vị trí + kích thước lấy từ camera thật,
        /// nên khung di chuyển theo người chơi thay vì nằm chết ở giữa bản đồ.
        /// </summary>
        private void DrawViewFrame()
        {
            if (viewFrame == null || mapView == null) return;

            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null)
            {
                if (viewFrame.enabled) viewFrame.enabled = false;
                return;
            }

            if (!viewFrame.enabled) viewFrame.enabled = true;
            if (viewFrame.sprite == null) viewFrame.sprite = UISprites.Frame(6, 2);

            float height = cam.orthographicSize * 2f;
            float width = height * cam.aspect;

            Vector2 center = cam.transform.position;
            Vector2 size = mapView.rect.size;

            float nx = Mathf.InverseLerp(m_Bounds.xMin, m_Bounds.xMax, center.x);
            float ny = Mathf.InverseLerp(m_Bounds.yMin, m_Bounds.yMax, center.y);

            RectTransform rt = viewFrame.rectTransform;
            rt.anchorMin = new Vector2(nx, ny);
            rt.anchorMax = new Vector2(nx, ny);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            float w = Mathf.Clamp(width / Mathf.Max(0.01f, m_Bounds.width), 0.02f, 1f) * size.x;
            float h = Mathf.Clamp(height / Mathf.Max(0.01f, m_Bounds.height), 0.02f, 1f) * size.y;
            rt.sizeDelta = new Vector2(w, h);
        }

        private void RefreshBounds()
        {
            StageBounds bounds = FindAnyObjectByType<StageBounds>();
            if (bounds != null)
            {
                m_Bounds = bounds.Bounds;
                m_HasBounds = m_Bounds.width > 0.01f && m_Bounds.height > 0.01f;
            }

            if (!m_HasBounds) m_Bounds = new Rect(-18.8f, -10.5f, 37.6f, 21f);
        }

        /// <summary>Lấy đúng sprite nền của màn đang chơi làm nền bản đồ.</summary>
        private void ApplyStageBackground()
        {
            if (mapBackground == null) return;

            Sprite stageSprite = FindStageBackgroundSprite();
            if (stageSprite != null)
            {
                if (mapBackground.sprite != stageSprite) mapBackground.sprite = stageSprite;
                mapBackground.color = Color.white;
                m_BackgroundApplied = true;
            }
            else if (fallbackBackground != null && mapBackground.sprite != fallbackBackground)
            {
                mapBackground.sprite = fallbackBackground;
                mapBackground.color = Color.white;
                m_BackgroundApplied = true;
            }

            if (mapGrid != null && !mapGrid.gameObject.activeSelf) mapGrid.gameObject.SetActive(true);
        }

        private Sprite FindStageBackgroundSprite()
        {
            SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            SpriteRenderer best = null;

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || renderer.sprite == null) continue;
                if (renderer.transform.name != "Background" && renderer.sortingOrder > -500) continue;

                if (best == null || renderer.sortingOrder < best.sortingOrder) best = renderer;
            }

            return best != null ? best.sprite : null;
        }

        private void Draw()
        {
            if (mapView == null) return;

            m_Used = 0;

            GameController gc = FindAnyObjectByType<GameController>();
            TankBrain player = gc != null ? gc.PlayerTank : null;

            // Vẽ quái trước, tank người chơi/đồng đội sau để mũi tên luôn nằm trên.
            EnemyBrain[] enemies = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
            int enemyCount = 0;
            int limit = Mathf.Max(0, maxEnemyBlips);

            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;

                bool isBoss = enemy.GetComponent<BossTag>() != null;
                if (isBoss)
                {
                    Place(enemy.transform.position, blipBoss, bossBlipSize, false);
                    continue;
                }

                if (enemyCount >= limit) continue;
                enemyCount++;
                Place(enemy.transform.position, blipEnemy, blipSize, false);
            }

            if (player != null && player.IsAlive)
                Place(player, blipPlayer, playerBlipSize, Facing(player), true);

            // Đồng đội (khi có co-op): mọi tank khác tank người chơi.
            TankBrain[] tanks = FindObjectsByType<TankBrain>(FindObjectsSortMode.None);
            foreach (TankBrain tank in tanks)
            {
                if (tank == null || tank == player || !tank.IsAlive) continue;
                Place(tank, blipTeammate, teammateBlipSize, Facing(tank), true);
            }

            // Tắt phần chấm dư của lần vẽ trước.
            for (int i = m_Used; i < m_Pool.Count; i++)
            {
                Blip leftover = m_Pool[i];
                if (leftover != null && leftover.Root != null && leftover.Root.gameObject.activeSelf)
                    leftover.Root.gameObject.SetActive(false);
            }
        }

        /// <summary>Hướng nòng pháo của xe (dùng cho mũi tên trên bản đồ).</summary>
        private static Vector2 Facing(TankBrain tank)
        {
            if (tank.View != null && tank.View.Muzzle != null)
            {
                Vector2 dir = tank.View.Muzzle.position - tank.transform.position;
                if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
            }

            return tank.transform.up;
        }

        private void Place(Component unit, Sprite sprite, float size, Vector2 direction, bool withDisc)
        {
            if (unit == null) return;
            Place(unit.transform.position, sprite, size, direction, withDisc);
        }

        private void Place(Vector2 world, Sprite sprite, float size, bool rotateToFacing)
        {
            Place(world, sprite, size, Vector2.zero, false);
        }

        private void Place(Vector2 world, Sprite sprite, float size, Vector2 direction, bool withDisc)
        {
            if (sprite == null) return;

            Blip blip = GetBlip();
            if (blip == null) return;

            blip.Root.gameObject.SetActive(true);
            blip.Root.sizeDelta = new Vector2(size, size);

            if (blip.Icon.sprite != sprite) blip.Icon.sprite = sprite;
            blip.Icon.rectTransform.sizeDelta = new Vector2(size, size);

            // Nền tròn tối phía sau mũi tên: nhìn rõ trên cả nền bản đồ sáng.
            if (blip.Disc != null)
            {
                blip.Disc.enabled = withDisc;
                if (withDisc)
                {
                    blip.Disc.sprite = UISprites.Rounded(Mathf.Max(2, Mathf.RoundToInt(size * 0.5f)));
                    blip.Disc.rectTransform.sizeDelta = new Vector2(size * 1.25f, size * 1.25f);
                    blip.Disc.color = new Color(0.02f, 0.05f, 0.08f, 0.88f);
                }
            }

            float nx = Mathf.InverseLerp(m_Bounds.xMin, m_Bounds.xMax, world.x);
            float ny = Mathf.InverseLerp(m_Bounds.yMin, m_Bounds.yMax, world.y);

            RectTransform rt = blip.Root;
            rt.anchorMin = new Vector2(nx, ny);
            rt.anchorMax = new Vector2(nx, ny);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            // Mũi tên chỉ hướng nòng pháo của xe.
            if (direction.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                rt.localEulerAngles = new Vector3(0f, 0f, angle - blipBaseAngle);
            }
            else
            {
                rt.localEulerAngles = Vector3.zero;
            }
        }

        private class Blip
        {
            public RectTransform Root;
            public Image Disc;
            public Image Icon;
        }

        private Blip GetBlip()
        {
            while (m_Used < m_Pool.Count)
            {
                Blip existing = m_Pool[m_Used++];
                if (existing != null && existing.Root != null) return existing;
            }

            var rootGo = new GameObject("Blip", typeof(RectTransform));
            rootGo.transform.SetParent(mapView, false);

            var discGo = new GameObject("Disc", typeof(RectTransform), typeof(Image));
            discGo.transform.SetParent(rootGo.transform, false);
            Image disc = discGo.GetComponent<Image>();
            disc.type = Image.Type.Sliced;
            disc.raycastTarget = false;
            disc.enabled = false;
            Stretch(disc.rectTransform);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rootGo.transform, false);
            Image icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            Stretch(icon.rectTransform);

            // Bóng mờ phía sau để chấm/mũi tên vẫn đọc được trên nền bản đồ sáng.
            Shadow shadow = iconGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            var blip = new Blip { Root = (RectTransform)rootGo.transform, Disc = disc, Icon = icon };
            m_Pool.Add(blip);
            m_Used = m_Pool.Count;
            return blip;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
