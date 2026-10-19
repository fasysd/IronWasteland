using System;
using System.Collections.Generic;
using UnityEngine;

namespace IronWasteland.Enemies.Navigation
{
    /// <summary>
    /// Thuật toán A* 2D cục bộ giới hạn trong vòng tròn tâm D, bán kính C.
    /// - Địa hình (Terrain): vật cản tuyệt đối (Walkable = false).
    /// - Kẻ địch (Enemy): vật cản mềm (Walkable = true + penalty cost cao trên toàn bộ diện tích Collider).
    /// - Vùng ngoài bán kính C: tuyệt đối không thể đi qua (Walkable = false).
    /// </summary>
    public class EnemyPathfinder2D
    {
        private class Node
        {
            public int x;
            public int y;
            public Vector2 worldPos;
            public bool isWalkable;
            public float penalty;

            public float gCost;
            public float hCost;
            public Node parent;

            public float FCost => gCost + hCost + penalty;

            public void Reset()
            {
                gCost = float.MaxValue;
                hCost = 0f;
                parent = null;
            }
        }

        private readonly float m_NodeSize;
        private readonly LayerMask m_TerrainLayer;
        private readonly LayerMask m_EnemyLayer;
        private readonly float m_EnemyPenalty;

        public EnemyPathfinder2D(float nodeSize, LayerMask terrainLayer, LayerMask enemyLayer, float enemyPenalty = 1.5f)
        {
            m_NodeSize = Mathf.Max(0.2f, nodeSize);
            m_TerrainLayer = terrainLayer;
            m_EnemyLayer = enemyLayer;
            m_EnemyPenalty = Mathf.Max(0f, enemyPenalty);
        }

        /// <summary>
        /// Tìm đường từ startPos tới một vị trí sao cho khoảng cách tới targetPos <= radiusA.
        /// Toàn bộ đường đi bắt buộc phải nằm trong đường tròn tâm anchorD bán kính radiusC.
        /// Nếu không tìm thấy đường đi thỏa mãn, trả về null.
        /// </summary>
        public List<Vector2> FindPathToRadiusA(
            Vector2 startPos,
            Vector2 targetPos,
            float radiusA,
            Vector2 anchorD,
            float radiusC,
            Collider2D ignoreEnemyCollider,
            float agentRadius = 0.3f)
        {
            // Kiểm tra điều kiện tiên quyết: Mục tiêu phải nằm trong phạm vi C quanh D
            if (Vector2.Distance(targetPos, anchorD) > radiusC)
            {
                return null;
            }

            // Nếu bản thân đã nằm trong bán kính A của mục tiêu
            if (Vector2.Distance(startPos, targetPos) <= radiusA)
            {
                return new List<Vector2> { startPos };
            }

            float effectiveRadiusA = Mathf.Max(radiusA, m_NodeSize * 0.8f);

            return SearchAStar(
                startPos,
                anchorD,
                radiusC,
                ignoreEnemyCollider,
                agentRadius,
                isGoalReached: (worldPos) => Vector2.Distance(worldPos, targetPos) <= effectiveRadiusA,
                heuristic: (worldPos) => Mathf.Max(0f, Vector2.Distance(worldPos, targetPos) - radiusA)
            );
        }

        /// <summary>
        /// Tìm đường từ startPos về thẳng điểm đích targetPos (dùng khi quay về tâm D).
        /// Toàn bộ đường đi bắt buộc phải nằm trong đường tròn tâm anchorD bán kính radiusC.
        /// </summary>
        public List<Vector2> FindPathToPoint(
            Vector2 startPos,
            Vector2 targetPos,
            Vector2 anchorD,
            float radiusC,
            Collider2D ignoreEnemyCollider,
            float stopDistance = 0.2f,
            float agentRadius = 0.3f)
        {
            if (Vector2.Distance(targetPos, anchorD) > radiusC)
            {
                return null;
            }

            if (Vector2.Distance(startPos, targetPos) <= stopDistance)
            {
                return new List<Vector2> { startPos };
            }

            // Khoảng cách tối thiểu để ít nhất một ô lưới (kích thước m_NodeSize) có thể chạm tới targetPos
            float effectiveStopDistance = Mathf.Max(stopDistance, m_NodeSize * 0.8f);

            var path = SearchAStar(
                startPos,
                anchorD,
                radiusC,
                ignoreEnemyCollider,
                agentRadius,
                isGoalReached: (worldPos) => Vector2.Distance(worldPos, targetPos) <= effectiveStopDistance,
                heuristic: (worldPos) => Vector2.Distance(worldPos, targetPos)
            );

            if (path != null && path.Count > 0)
            {
                // Thêm chính xác điểm targetPos vào cuối lộ trình
                if (Vector2.Distance(path[path.Count - 1], targetPos) > 0.05f)
                {
                    path.Add(targetPos);
                }
            }

            return path;
        }

        private List<Vector2> SearchAStar(
            Vector2 startPos,
            Vector2 anchorD,
            float radiusC,
            Collider2D ignoreEnemyCollider,
            float agentRadius,
            Func<Vector2, bool> isGoalReached,
            Func<Vector2, float> heuristic,
            bool ignoreOtherEnemies = false)
        {
            // Xây dựng lưới cục bộ quanh tâm anchorD với bán kính radiusC
            float halfExtent = radiusC + m_NodeSize;
            Vector2 gridOrigin = anchorD - new Vector2(halfExtent, halfExtent);
            int width = Mathf.CeilToInt((halfExtent * 2f) / m_NodeSize) + 1;
            int height = width;

            Node[,] grid = new Node[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 worldPos = gridOrigin + new Vector2((x + 0.5f) * m_NodeSize, (y + 0.5f) * m_NodeSize);
                    bool insideCircle = Vector2.Distance(worldPos, anchorD) <= radiusC;
                    bool blockedByTerrain = false;

                    if (insideCircle)
                    {
                        // Kiểm tra va chạm với địa hình (Terrain)
                        Collider2D hit = Physics2D.OverlapBox(worldPos, new Vector2(m_NodeSize * 0.9f, m_NodeSize * 0.9f), 0f, m_TerrainLayer);
                        if (hit != null)
                        {
                            blockedByTerrain = true;
                        }
                    }

                    grid[x, y] = new Node
                    {
                        x = x,
                        y = y,
                        worldPos = worldPos,
                        isWalkable = insideCircle && !blockedByTerrain,
                        penalty = 0f,
                        gCost = float.MaxValue,
                        hCost = 0f,
                        parent = null
                    };
                }
            }

            // Ánh xạ Collider của các Enemy khác vào lưới dưới dạng Soft Penalty (nếu không bỏ qua)
            if (!ignoreOtherEnemies)
            {
                StampEnemyColliders(grid, gridOrigin, width, height, anchorD, radiusC, ignoreEnemyCollider, startPos);
            }

            // Tìm node bắt đầu
            GetGridCoordinates(startPos, gridOrigin, out int startX, out int startY);
            startX = Mathf.Clamp(startX, 0, width - 1);
            startY = Mathf.Clamp(startY, 0, height - 1);

            Node startNode = grid[startX, startY];
            // Nếu startNode bị coi là unwalkable do nằm sát tường, cố gắng nới lỏng startNode để quái có thể thoát ra
            startNode.isWalkable = true;
            startNode.gCost = 0f;
            startNode.hCost = heuristic(startNode.worldPos);

            List<Node> openSet = new List<Node> { startNode };
            HashSet<Node> closedSet = new HashSet<Node>();

            Node bestGoalNode = null;
            Node closestNode = startNode;
            float minH = startNode.hCost;

            int maxIterations = Mathf.Max(2500, width * height);
            int iteration = 0;

            int[] dx = { 0, 1, 0, -1, 1, 1, -1, -1 };
            int[] dy = { 1, 0, -1, 0, 1, -1, 1, -1 };
            float[] moveCost = { 1f, 1f, 1f, 1f, 1.414f, 1.414f, 1.414f, 1.414f };

            while (openSet.Count > 0 && iteration++ < maxIterations)
            {
                // Lấy node có FCost nhỏ nhất
                int bestIndex = 0;
                float lowestFCost = openSet[0].FCost;
                for (int i = 1; i < openSet.Count; i++)
                {
                    float f = openSet[i].FCost;
                    if (f < lowestFCost || (Mathf.Approximately(f, lowestFCost) && openSet[i].hCost < openSet[bestIndex].hCost))
                    {
                        lowestFCost = f;
                        bestIndex = i;
                    }
                }

                Node current = openSet[bestIndex];
                openSet.RemoveAt(bestIndex);
                closedSet.Add(current);

                // Kiểm tra đã đạt mục tiêu chưa
                if (isGoalReached(current.worldPos))
                {
                    bestGoalNode = current;
                    break;
                }

                // Duyệt 8 hướng lân cận
                for (int i = 0; i < 8; i++)
                {
                    int nx = current.x + dx[i];
                    int ny = current.y + dy[i];

                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;

                    Node neighbor = grid[nx, ny];
                    if (!neighbor.isWalkable || closedSet.Contains(neighbor)) continue;

                    // Chặn đi chéo góc qua khe tường hẹp (corner cutting)
                    if (i >= 4)
                    {
                        if (!grid[current.x, ny].isWalkable || !grid[nx, current.y].isWalkable)
                            continue;
                    }

                    float tentativeGCost = current.gCost + moveCost[i] * m_NodeSize;
                    if (tentativeGCost < neighbor.gCost)
                    {
                        neighbor.parent = current;
                        neighbor.gCost = tentativeGCost;
                        neighbor.hCost = heuristic(neighbor.worldPos);

                        if (neighbor.hCost < minH)
                        {
                            minH = neighbor.hCost;
                            closestNode = neighbor;
                        }

                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }
            }

            // Nếu không tìm thấy đường đi tới đích do các kẻ địch khác cản trở:
            // Thử tìm lại lần 2 và bỏ qua toàn bộ kẻ địch vì tránh đè nhau chỉ là tiêu chí phụ.
            if (bestGoalNode == null && !ignoreOtherEnemies)
            {
                var fallbackPath = SearchAStar(
                    startPos,
                    anchorD,
                    radiusC,
                    ignoreEnemyCollider,
                    agentRadius,
                    isGoalReached,
                    heuristic,
                    ignoreOtherEnemies: true
                );

                if (fallbackPath != null)
                {
                    return fallbackPath;
                }
            }

            if (bestGoalNode == null)
            {
                return null;
            }

            // Truy vết đường đi
            List<Vector2> path = new List<Vector2>();
            Node curr = bestGoalNode;
            while (curr != null)
            {
                path.Add(curr.worldPos);
                curr = curr.parent;
            }
            path.Reverse();

            // Làm mịn đường đi (String pulling)
            return SmoothPath(path, agentRadius);
        }

        /// <summary>
        /// Duyệt các quái vật khác trong bán kính C và gán Soft Penalty lên toàn bộ các node bị che phủ bởi Collider.
        /// Quái vật đang vô tình đè lên bản thân sẽ được bỏ qua để không gây kẹt thoát hiểm.
        /// </summary>
        private void StampEnemyColliders(
            Node[,] grid,
            Vector2 gridOrigin,
            int width,
            int height,
            Vector2 anchorD,
            float radiusC,
            Collider2D ignoreEnemyCollider,
            Vector2 startPos)
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(anchorD, radiusC, m_EnemyLayer);
            if (colliders == null || colliders.Length == 0) return;

            Transform ignoreRoot = ignoreEnemyCollider != null ? ignoreEnemyCollider.transform.root : null;
            Bounds? ignoreBounds = ignoreEnemyCollider != null ? (Bounds?)ignoreEnemyCollider.bounds : null;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null || col == ignoreEnemyCollider) continue;
                if (ignoreRoot != null && col.transform.root == ignoreRoot) continue;

                Bounds bounds = col.bounds;

                // Nếu quái vật khác này đang vô tình đè lên vị trí xuất phát hoặc va chạm với collider của bản thân:
                // Bỏ qua không phạt quái này để cả 2 quái có thể thoát ra và di chuyển bình thường ("cứ đi bình thường").
                if (bounds.Contains(startPos)) continue;
                if (ignoreBounds.HasValue && bounds.Intersects(ignoreBounds.Value)) continue;

                Vector2 min = (Vector2)bounds.min;
                Vector2 max = (Vector2)bounds.max;

                GetGridCoordinates(min, gridOrigin, out int startX, out int startY);
                GetGridCoordinates(max, gridOrigin, out int endX, out int endY);

                startX = Mathf.Clamp(startX, 0, width - 1);
                endX = Mathf.Clamp(endX, 0, width - 1);
                startY = Mathf.Clamp(startY, 0, height - 1);
                endY = Mathf.Clamp(endY, 0, height - 1);

                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        if (grid[x, y].isWalkable)
                        {
                            grid[x, y].penalty += m_EnemyPenalty;
                        }
                    }
                }
            }
        }

        private void GetGridCoordinates(Vector2 worldPos, Vector2 gridOrigin, out int x, out int y)
        {
            x = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / m_NodeSize);
            y = Mathf.FloorToInt((worldPos.y - gridOrigin.y) / m_NodeSize);
        }

        /// <summary>
        /// Làm mịn đường đi bằng cách chiếu CircleCast giữa các điểm nút để loại bỏ các waypoint dư thừa.
        /// </summary>
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
                        RaycastHit2D hit = Physics2D.CircleCast(from, agentRadius * 0.8f, dir.normalized, dist, m_TerrainLayer);
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
    }
}

