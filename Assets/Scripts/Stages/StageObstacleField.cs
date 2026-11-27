using System.Collections.Generic;
using IronWasteland.Enemies.Navigation;
using IronWasteland.Utilities;
using UnityEngine;

namespace IronWasteland.Stages
{
    /// <summary>
    /// Dựng vật cản (đá/tường đổ) cho màn chơi để map bớt trống và có chỗ né đạn.
    ///
    /// Vị trí do <see cref="ObstacleLayout"/> sinh theo seed của mã màn (cùng màn ⇒ cùng
    /// layout mỗi lần chơi), nên không phải đặt tay 6–8 object cho từng scene.
    /// Mỗi vật cản:
    /// - Nằm ở layer Terrain để tank bị chặn (Physics2D matrix cho Terrain đè Tank/Enemy).
    /// - Đăng ký với <see cref="PathfindingGrid2D"/> để quái A* né thay vì đi xuyên.
    ///
    /// Gắn component này lên object "EnemySpawner" (hoặc object bất kỳ ở giữa map).
    /// Toạ độ vật cản tính tương đối so với transform của object này.
    /// </summary>
    [DisallowMultipleComponent]
    public class StageObstacleField : MonoBehaviour
    {
        [Header("Vật cản")]
        [Tooltip("Mã màn (1-1, 2-3...). Để trống sẽ lấy tên scene. Mã quyết định layout.")]
        [SerializeField] private string stageCode = string.Empty;

        [Tooltip("Số vật cản muốn có. 0 = không dựng gì.")]
        [SerializeField, Min(0)] private int obstacleCount = 6;

        [Tooltip("Màu vật cản (sprite trắng được nhuộm màu này).")]
        [SerializeField] private Color obstacleColor = new Color(0.26f, 0.27f, 0.30f, 1f);

        [SerializeField] private bool logBuild = true;

        private readonly List<GameObject> m_Spawned = new List<GameObject>();

        private void Awake()
        {
            Build();
        }

        /// <summary>Dựng vật cản. Gọi tự động trong Awake; gọi lại được khi cần đổi layout.</summary>
        public void Build()
        {
            if (obstacleCount <= 0 || m_Spawned.Count > 0) return;

            int seed = ResolveSeed();
            List<ObstaclePlacement> keepOut = CollectKeepOutAreas();
            List<ObstaclePlacement> layout = ObstacleLayout.Build(seed, obstacleCount, 32, keepOut);
            if (layout.Count == 0) return;

            int terrainLayer = LayerMask.NameToLayer("Terrain");
            if (terrainLayer < 0) terrainLayer = gameObject.layer;

            Transform container = new GameObject("Obstacles").transform;
            container.SetParent(transform, false);
            container.localPosition = Vector3.zero;

            Sprite sprite = SpriteFactory.Rock;

            for (int i = 0; i < layout.Count; i++)
            {
                ObstaclePlacement placement = layout[i];

                var go = new GameObject("Obstacle_" + (i + 1));
                go.layer = terrainLayer;
                go.transform.SetParent(container, false);
                go.transform.localPosition = new Vector3(placement.X, placement.Y, 0f);

                // Sprite sinh runtime có kích thước 1x1 unit; scale = 2 * bán kính mong muốn.
                go.transform.localScale = Vector3.one * (placement.Radius * 2f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = obstacleColor;
                renderer.sortingOrder = -1; // nằm dưới tank/quái

                var collider = go.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;

                PathfindingGrid2D.Instance.AddObstacle(collider);
                m_Spawned.Add(go);
            }

            if (logBuild)
            {
                Debug.Log($"[StageObstacleField] Dựng {layout.Count} vật cản cho màn '{ResolveStageKey()}' (seed {seed}).", this);
            }
        }

        /// <summary>
        /// Lấy các vùng cần chừa (toạ độ LOCAL so với object này): vùng spawn quái và
        /// vật thể mục tiêu (lõi/radar...). Đá không được lấn vào đó, nếu không quái sinh ra
        /// sẽ kẹt trong đá, hoặc đá che mất mục tiêu cần phá.
        /// </summary>
        private List<ObstaclePlacement> CollectKeepOutAreas()
        {
            var keepOut = new List<ObstaclePlacement>();

            var zones = Object.FindObjectsByType<SpawnZone>(FindObjectsInactive.Exclude);
            for (int i = 0; i < zones.Length; i++)
            {
                SpawnZone zone = zones[i];
                if (zone == null) continue;
                AddKeepOut(keepOut, zone.Center, zone.Radius);
            }

            const float objectiveRadius = 2.5f;
            var targets = Object.FindObjectsByType<DestructibleTarget>(FindObjectsInactive.Exclude);
            for (int i = 0; i < targets.Length; i++)
            {
                DestructibleTarget target = targets[i];
                if (target == null) continue;
                AddKeepOut(keepOut, target.transform.position, objectiveRadius);
            }

            const float escortLaneRadius = 2f;
            const float escortSampleStep = 1.5f;
            var escorts = Object.FindObjectsByType<EscortVehicle>(FindObjectsInactive.Exclude);
            for (int i = 0; i < escorts.Length; i++)
            {
                EscortVehicle escort = escorts[i];
                if (escort == null) continue;
                IReadOnlyList<Vector2> pts = escort.Waypoints;
                if (pts == null || pts.Count == 0)
                {
                    AddKeepOut(keepOut, escort.transform.position, escortLaneRadius);
                    continue;
                }
                AddKeepOut(keepOut, pts[0], escortLaneRadius);
                for (int w = 1; w < pts.Count; w++)
                {
                    Vector2 a = pts[w - 1];
                    Vector2 b = pts[w];
                    float dist = Vector2.Distance(a, b);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(dist / escortSampleStep));
                    for (int s = 1; s <= steps; s++)
                    {
                        AddKeepOut(keepOut, Vector2.Lerp(a, b, s / (float)steps), escortLaneRadius);
                    }
                }
            }

            return keepOut;
        }

        private void AddKeepOut(List<ObstaclePlacement> keepOut, Vector2 worldCenter, float radius)
        {
            // Layout sinh trong local space của object này, nên quy đổi vùng cấm về local.
            Vector3 local = transform.InverseTransformPoint(worldCenter);
            keepOut.Add(new ObstaclePlacement(local.x, local.y, radius));
        }

        private int ResolveSeed()
        {
            string key = ResolveStageKey();

            unchecked
            {
                uint hash = 2166136261u; // FNV-1a
                for (int i = 0; i < key.Length; i++)
                {
                    hash = (hash ^ key[i]) * 16777619u;
                }

                // Bỏ bit dấu để seed không bao giờ âm.
                return (int)(hash & 0x7FFFFFFFu);
            }
        }

        private string ResolveStageKey()
        {
            if (!string.IsNullOrWhiteSpace(stageCode)) return stageCode.Trim();
            return gameObject.scene.name;
        }
    }
}
