using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Enemies.Navigation
{
    /// <summary>
    /// Node trong bản đồ tìm đường A*.
    /// </summary>
    public class PathfindingNode
    {
        public int x;
        public int y;
        public Vector2 worldPos;
        public bool isWalkable;

        // Dữ liệu dùng cho A* (sử dụng versioning để Zero GC Allocation)
        public float gCost;
        public float hCost;
        public float penalty;
        public PathfindingNode parent;
        public int searchVersion;
        public int closedVersion;

        public float FCost => gCost + hCost + penalty;

        public PathfindingNode(int x, int y, Vector2 worldPos, bool isWalkable)
        {
            this.x = x;
            this.y = y;
            this.worldPos = worldPos;
            this.isWalkable = isWalkable;
            searchVersion = 0;
            closedVersion = 0;
        }
    }

    /// <summary>
    /// Grid Map Singleton 2D quản lý toàn bộ hệ thống tìm đường trong Scene.
    /// - Terrain (địa hình tĩnh): chỉ quét 1 lần duy nhất lúc khởi tạo.
    /// - Enemy Presence: mảng 2D đếm số lượng quái vật trên từng ô lưới, được cập nhật real-time khi Enemy di chuyển.
    /// - A* Zero-Allocation: tái sử dụng mảng Node và versioning để tránh hoàn toàn GC Alloc khi tìm đường.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PathfindingGrid2D : MonoBehaviour
    {
        private static PathfindingGrid2D s_Instance;

        public static PathfindingGrid2D Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindFirstObjectByType<PathfindingGrid2D>();
                    if (s_Instance == null)
                    {
                        GameObject go = new GameObject("[PathfindingGrid2D]");
                        s_Instance = go.AddComponent<PathfindingGrid2D>();
                    }
                }
                return s_Instance;
            }
        }

        [Header("Grid Configuration")]
        [Tooltip("Kích thước của một ô lưới (mét).")]
        [SerializeField, Range(0.2f, 1f)] private float nodeSize = 0.5f;

        [Tooltip("Tự động tính toán kích thước bao quanh toàn bộ Collider thuộc TerrainLayer.")]
        [SerializeField] private bool autoCalculateBounds = true;

        [Tooltip("Vùng bao thủ công nếu không bật auto hoặc không tìm thấy terrain collider.")]
        [SerializeField] private Bounds manualBounds = new Bounds(Vector3.zero, new Vector3(80f, 80f, 0f));

        [Tooltip("Phần mở rộng viền ngoài của bản đồ (mét).")]
        [SerializeField, Min(1f)] private float boundsPadding = 10f;

        [Header("Layers & Penalties")]
        [SerializeField] private LayerMask terrainLayer;
        [SerializeField] private float enemyAvoidancePenalty = 2.0f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = false;
        [SerializeField] private bool drawEnemyPresenceOnly = true;

        private PathfindingNode[,] m_Grid;
        private int[,] m_EnemyPresence;
        private int m_Width;
        private int m_Height;
        private Vector2 m_GridOrigin;
        private Bounds m_CurrentBounds;
        private bool m_IsInitialized = false;

        private int m_CurrentSearchVersion = 0;
        private readonly List<PathfindingNode> m_OpenSet = new List<PathfindingNode>(1024);

        private static readonly int[] s_Dx = { 0, 1, 0, -1, 1, 1, -1, -1 };
        private static readonly int[] s_Dy = { 1, 0, -1, 0, 1, -1, 1, -1 };
        private static readonly float[] s_MoveCost = { 1f, 1f, 1f, 1f, 1.41421356f, 1.41421356f, 1.41421356f, 1.41421356f };

        public float NodeSize => nodeSize;
        public LayerMask TerrainLayer => terrainLayer;
        public float EnemyAvoidancePenalty { get => enemyAvoidancePenalty; set => enemyAvoidancePenalty = value; }
        public bool IsInitialized => m_IsInitialized;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;

            InitializeGrid();
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        /// <summary>
        /// Quét địa hình và khởi tạo lưới Node.
        /// </summary>
        [ContextMenu("Bake / Rebuild Grid")]
        public void InitializeGrid()
        {
            if (terrainLayer.value == 0)
            {
                terrainLayer = LayerMask.GetMask("Terrain");
            }

            nodeSize = Mathf.Max(0.2f, nodeSize);

            // Xác định Bounds của bản đồ
            m_CurrentBounds = manualBounds;
            if (autoCalculateBounds)
            {
                Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
                bool foundTerrain = false;
                Bounds combinedBounds = new Bounds();

                for (int i = 0; i < colliders.Length; i++)
                {
                    if ((terrainLayer.value & (1 << colliders[i].gameObject.layer)) != 0)
                    {
                        if (!foundTerrain)
                        {
                            combinedBounds = colliders[i].bounds;
                            foundTerrain = true;
                        }
                        else
                        {
                            combinedBounds.Encapsulate(colliders[i].bounds);
                        }
                    }
                }

                if (foundTerrain)
                {
                    combinedBounds.Expand(boundsPadding * 2f);
                    m_CurrentBounds = combinedBounds;
                }
            }

            m_Width = Mathf.CeilToInt(m_CurrentBounds.size.x / nodeSize);
            m_Height = Mathf.CeilToInt(m_CurrentBounds.size.y / nodeSize);
            m_GridOrigin = (Vector2)m_CurrentBounds.min;

            m_Grid = new PathfindingNode[m_Width, m_Height];
            m_EnemyPresence = new int[m_Width, m_Height];

            Vector2 boxSize = new Vector2(nodeSize * 0.9f, nodeSize * 0.9f);

            // Quét Terrain duy nhất 1 lần
            for (int x = 0; x < m_Width; x++)
            {
                for (int y = 0; y < m_Height; y++)
                {
                    Vector2 worldPos = m_GridOrigin + new Vector2((x + 0.5f) * nodeSize, (y + 0.5f) * nodeSize);
                    Collider2D hit = Physics2D.OverlapBox(worldPos, boxSize, 0f, terrainLayer);
                    bool isWalkable = (hit == null);

                    m_Grid[x, y] = new PathfindingNode(x, y, worldPos, isWalkable);
                }
            }

            m_IsInitialized = true;
        }

        #region Enemy Presence Management

        /// <summary>
        /// Đăng ký vị trí đứng của kẻ địch lên ô lưới.
        /// </summary>
        public Vector2Int RegisterEnemy(Vector2 worldPos)
        {
            if (!m_IsInitialized) InitializeGrid();

            Vector2Int coord = GetGridCoordinates(worldPos);
            if (IsValidCoord(coord.x, coord.y))
            {
                m_EnemyPresence[coord.x, coord.y]++;
            }
            return coord;
        }

        /// <summary>
        /// Hủy đăng ký vị trí của kẻ địch khỏi ô lưới khi rời đi hoặc chết.
        /// </summary>
        public void UnregisterEnemy(Vector2Int coord)
        {
            if (IsValidCoord(coord.x, coord.y))
            {
                if (m_EnemyPresence[coord.x, coord.y] > 0)
                {
                    m_EnemyPresence[coord.x, coord.y]--;
                }
            }
        }

        /// <summary>
        /// Cập nhật vị trí ô lưới khi kẻ địch di chuyển.
        /// Chỉ tốn chi phí O(1) khi kẻ địch bước sang một ô lưới mới.
        /// </summary>
        public void UpdateEnemyPosition(ref Vector2Int currentCoord, Vector2 newWorldPos)
        {
            if (!m_IsInitialized) InitializeGrid();

            Vector2Int newCoord = GetGridCoordinates(newWorldPos);
            if (newCoord == currentCoord) return;

            UnregisterEnemy(currentCoord);
            if (IsValidCoord(newCoord.x, newCoord.y))
            {
                m_EnemyPresence[newCoord.x, newCoord.y]++;
            }
            currentCoord = newCoord;
        }

        #endregion

        #region Pathfinding API

        /// <summary>
        /// Tìm đường từ startPos tới mục tiêu sao cho khoảng cách tới targetPos <= radiusA.
        /// Toàn bộ lộ trình bắt buộc nằm trong vòng tròn tâm anchorD bán kính radiusC.
        /// Tích hợp bước chốt điểm đích sâu vào trong phạm vi A (khoảng cách nodeSize * căn 2).
        /// </summary>
        public List<Vector2> FindPathToRadiusA(
            Vector2 startPos,
            Vector2 targetPos,
            float radiusA,
            Vector2 anchorD,
            float radiusC,
            float agentRadius = 0.3f,
            float penaltyMultiplier = 1f)
        {
            if (!m_IsInitialized) InitializeGrid();

            if (Vector2.Distance(targetPos, anchorD) > radiusC)
            {
                return null;
            }

            if (Vector2.Distance(startPos, targetPos) <= radiusA)
            {
                return new List<Vector2> { startPos };
            }

            float effectiveRadiusA = Mathf.Max(radiusA, nodeSize * 0.8f);
            Vector2Int ignoreCoord = GetGridCoordinates(startPos);

            var path = SearchAStar(
                startPos,
                anchorD,
                radiusC,
                ignoreCoord,
                agentRadius,
                isGoalReached: (worldPos) => Vector2.Distance(worldPos, targetPos) <= effectiveRadiusA,
                heuristic: (worldPos) => Mathf.Max(0f, Vector2.Distance(worldPos, targetPos) - radiusA),
                penaltyMultiplier: penaltyMultiplier
            );

            // Bổ sung điểm đích nội suy sâu vào trong bán kính A
            if (path != null && path.Count > 0)
            {
                Vector2 lastNode = path[path.Count - 1];
                Vector2 dir = targetPos - lastNode;
                float distToTarget = dir.magnitude;

                if (distToTarget > 0.001f)
                {
                    float step = nodeSize * Mathf.Sqrt(2f);
                    float actualStep = Mathf.Min(step, distToTarget);
                    Vector2 dirNorm = dir / distToTarget;

                    RaycastHit2D hit = Physics2D.CircleCast(lastNode, agentRadius * 0.8f, dirNorm, actualStep, terrainLayer);
                    if (hit.collider != null)
                    {
                        actualStep = Mathf.Max(0f, hit.distance - 0.05f);
                    }

                    if (actualStep > 0.05f)
                    {
                        Vector2 goalPoint = lastNode + dirNorm * actualStep;

                        if (path.Count >= 2)
                        {
                            Vector2 prevNode = path[path.Count - 2];
                            Vector2 dirPrev = goalPoint - prevNode;
                            float distPrev = dirPrev.magnitude;
                            if (distPrev > 0.001f)
                            {
                                RaycastHit2D hitPrev = Physics2D.CircleCast(prevNode, agentRadius * 0.8f, dirPrev / distPrev, distPrev, terrainLayer);
                                if (hitPrev.collider == null)
                                {
                                    path[path.Count - 1] = goalPoint;
                                    return path;
                                }
                            }
                        }

                        path.Add(goalPoint);
                    }
                }
            }

            return path;
        }

        /// <summary>
        /// Tìm đường về thẳng một điểm cố định (vd: tâm D).
        /// </summary>
        public List<Vector2> FindPathToPoint(
            Vector2 startPos,
            Vector2 targetPos,
            Vector2 anchorD,
            float radiusC,
            float stopDistance = 0.2f,
            float agentRadius = 0.3f,
            float penaltyMultiplier = 1f)
        {
            if (!m_IsInitialized) InitializeGrid();

            if (Vector2.Distance(targetPos, anchorD) > radiusC)
            {
                return null;
            }

            if (Vector2.Distance(startPos, targetPos) <= stopDistance)
            {
                return new List<Vector2> { startPos };
            }

            float effectiveStopDistance = Mathf.Max(stopDistance, nodeSize * 0.8f);
            Vector2Int ignoreCoord = GetGridCoordinates(startPos);

            var path = SearchAStar(
                startPos,
                anchorD,
                radiusC,
                ignoreCoord,
                agentRadius,
                isGoalReached: (worldPos) => Vector2.Distance(worldPos, targetPos) <= effectiveStopDistance,
                heuristic: (worldPos) => Vector2.Distance(worldPos, targetPos),
                penaltyMultiplier: penaltyMultiplier
            );

            if (path != null && path.Count > 0)
            {
                if (Vector2.Distance(path[path.Count - 1], targetPos) > 0.05f)
                {
                    path.Add(targetPos);
                }
            }

            return path;
        }

        #endregion

        #region Internal A* Core

        private List<Vector2> SearchAStar(
            Vector2 startPos,
            Vector2 anchorD,
            float radiusC,
            Vector2Int ignoreCoord,
            float agentRadius,
            Func<Vector2, bool> isGoalReached,
            Func<Vector2, float> heuristic,
            float penaltyMultiplier = 1f,
            bool ignoreOtherEnemies = false)
        {
            Vector2Int startCoord = GetGridCoordinates(startPos);
            startCoord.x = Mathf.Clamp(startCoord.x, 0, m_Width - 1);
            startCoord.y = Mathf.Clamp(startCoord.y, 0, m_Height - 1);

            PathfindingNode startNode = m_Grid[startCoord.x, startCoord.y];

            m_CurrentSearchVersion++;
            int currentVersion = m_CurrentSearchVersion;

            startNode.searchVersion = currentVersion;
            startNode.gCost = 0f;
            startNode.hCost = heuristic(startNode.worldPos);
            startNode.penalty = 0f;
            startNode.parent = null;

            m_OpenSet.Clear();
            m_OpenSet.Add(startNode);

            PathfindingNode bestGoalNode = null;
            int maxIterations = Mathf.Max(2500, m_Width * m_Height);
            int iteration = 0;

            float activePenaltyFactor = (enemyAvoidancePenalty * penaltyMultiplier);

            while (m_OpenSet.Count > 0 && iteration++ < maxIterations)
            {
                int bestIndex = 0;
                float lowestFCost = m_OpenSet[0].FCost;
                for (int i = 1; i < m_OpenSet.Count; i++)
                {
                    float f = m_OpenSet[i].FCost;
                    if (f < lowestFCost || (Mathf.Approximately(f, lowestFCost) && m_OpenSet[i].hCost < m_OpenSet[bestIndex].hCost))
                    {
                        lowestFCost = f;
                        bestIndex = i;
                    }
                }

                PathfindingNode current = m_OpenSet[bestIndex];
                m_OpenSet.RemoveAt(bestIndex);
                current.closedVersion = currentVersion;

                if (isGoalReached(current.worldPos))
                {
                    bestGoalNode = current;
                    break;
                }

                for (int i = 0; i < 8; i++)
                {
                    int nx = current.x + s_Dx[i];
                    int ny = current.y + s_Dy[i];

                    if (!IsValidCoord(nx, ny)) continue;

                    PathfindingNode neighbor = m_Grid[nx, ny];

                    // Điều kiện 1: Phải đi được (không bị cản bởi địa hình tĩnh)
                    if (!neighbor.isWalkable) continue;

                    // Điều kiện 2: Phải nằm trong bán kính lãnh thổ C quanh tâm D của quái
                    if (Vector2.Distance(neighbor.worldPos, anchorD) > radiusC) continue;

                    // Điều kiện 3: Đã nằm trong Closed Set của lượt tìm kiếm này
                    if (neighbor.closedVersion == currentVersion) continue;

                    // Điều kiện 4: Chống đi xuyên góc tường (corner-cutting)
                    if (i >= 4)
                    {
                        if (!m_Grid[current.x, ny].isWalkable || !m_Grid[nx, current.y].isWalkable)
                            continue;
                    }

                    // Khởi tạo node nếu chưa được thăm trong phiên tìm kiếm này
                    if (neighbor.searchVersion != currentVersion)
                    {
                        neighbor.searchVersion = currentVersion;
                        neighbor.gCost = float.MaxValue;
                        neighbor.hCost = heuristic(neighbor.worldPos);
                        neighbor.parent = null;

                        // Soft Penalty từ Enemy Presence (trừ ô xuất phát của bản thân)
                        if (!ignoreOtherEnemies && !(nx == ignoreCoord.x && ny == ignoreCoord.y))
                        {
                            int count = m_EnemyPresence[nx, ny];
                            neighbor.penalty = count > 0 ? count * activePenaltyFactor : 0f;
                        }
                        else
                        {
                            neighbor.penalty = 0f;
                        }
                    }

                    float tentativeGCost = current.gCost + s_MoveCost[i] * nodeSize;
                    if (tentativeGCost < neighbor.gCost)
                    {
                        neighbor.parent = current;
                        neighbor.gCost = tentativeGCost;

                        if (!m_OpenSet.Contains(neighbor))
                        {
                            m_OpenSet.Add(neighbor);
                        }
                    }
                }
            }

            // Fallback lần 2: Nếu không thấy đường do bị quái khác đứng chặn kín, tìm lại và bỏ qua kẻ địch
            if (bestGoalNode == null && !ignoreOtherEnemies)
            {
                return SearchAStar(
                    startPos,
                    anchorD,
                    radiusC,
                    ignoreCoord,
                    agentRadius,
                    isGoalReached,
                    heuristic,
                    penaltyMultiplier,
                    ignoreOtherEnemies: true
                );
            }

            if (bestGoalNode == null)
            {
                return null;
            }

            // Truy vết đường đi
            List<Vector2> path = new List<Vector2>();
            PathfindingNode curr = bestGoalNode;
            while (curr != null)
            {
                path.Add(curr.worldPos);
                curr = curr.parent;
            }
            path.Reverse();

            // Làm mịn lộ trình (String pulling)
            return SmoothPath(path, agentRadius);
        }

        private List<Vector2> SmoothPath(List<Vector2> rawPath, float agentRadius)
        {
            if (rawPath == null || rawPath.Count <= 2)
            {
                return rawPath;
            }

            List<Vector2> smoothed = new List<Vector2> { rawPath[0] };
            int currentIndex = 0;

            while (currentIndex < rawPath.Count - 1)
            {
                int furthestVisible = currentIndex + 1;

                for (int next = rawPath.Count - 1; next > currentIndex + 1; next--)
                {
                    Vector2 from = rawPath[currentIndex];
                    Vector2 to = rawPath[next];
                    Vector2 dir = to - from;
                    float dist = dir.magnitude;

                    if (dist > 0.001f)
                    {
                        RaycastHit2D hit = Physics2D.CircleCast(from, agentRadius * 0.8f, dir.normalized, dist, terrainLayer);
                        if (hit.collider == null)
                        {
                            furthestVisible = next;
                            break;
                        }
                    }
                }

                smoothed.Add(rawPath[furthestVisible]);
                currentIndex = furthestVisible;
            }

            return smoothed;
        }

        #endregion

        #region Helpers

        public Vector2Int GetGridCoordinates(Vector2 worldPos)
        {
            int x = Mathf.FloorToInt((worldPos.x - m_GridOrigin.x) / nodeSize);
            int y = Mathf.FloorToInt((worldPos.y - m_GridOrigin.y) / nodeSize);
            return new Vector2Int(x, y);
        }

        public bool IsValidCoord(int x, int y)
        {
            return x >= 0 && x < m_Width && y >= 0 && y < m_Height;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || m_Grid == null) return;

            for (int x = 0; x < m_Width; x++)
            {
                for (int y = 0; y < m_Height; y++)
                {
                    int enemyCount = m_EnemyPresence != null ? m_EnemyPresence[x, y] : 0;
                    if (drawEnemyPresenceOnly && enemyCount == 0) continue;

                    PathfindingNode node = m_Grid[x, y];
                    if (enemyCount > 0)
                    {
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawCube(node.worldPos, Vector3.one * (nodeSize * 0.8f));
                    }
                    else if (!node.isWalkable)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireCube(node.worldPos, Vector3.one * (nodeSize * 0.9f));
                    }
                    else if (!drawEnemyPresenceOnly)
                    {
                        Gizmos.color = Color.gray;
                        Gizmos.DrawWireCube(node.worldPos, Vector3.one * (nodeSize * 0.9f));
                    }
                }
            }
        }

        #endregion
    }
}

