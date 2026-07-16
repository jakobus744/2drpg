using System;
using System.Collections.Generic;
using Godot;

namespace RPG2d.World;

public partial class NavigationManager : Node
{
    private readonly Dictionary<Vector2I, WalkabilityGrid> _zoneGrids = new();
    private readonly Dictionary<string, WalkabilityGrid> _templateCache = new();

    private static readonly HashSet<Vector2I> ZoneCoordinates = new()
    {
        new(0, 0), new(1, 0), new(2, 0),
        new(0, 1), new(1, 1), new(2, 1),
        new(0, 2), new(1, 2), new(2, 2),
        new(0, 3), new(1, 3), new(2, 3),
    };

    private static readonly Vector2I[] AdjacencyDeltas =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    [Export] public int ZoneSize { get; set; } = 3424;

    public void RegisterZone(Vector2I coord, Node zoneRoot)
    {
        string scenePath = zoneRoot.SceneFilePath;
        if (string.IsNullOrEmpty(scenePath))
            return;

        if (_templateCache.TryGetValue(scenePath, out var cached))
        {
            _zoneGrids[coord] = cached;
            return;
        }

        var grid = WalkabilityGrid.Build(zoneRoot, coord);
        if (grid != null)
        {
            _zoneGrids[coord] = grid;
            _templateCache[scenePath] = grid;
        }
    }

    public void UnregisterZone(Vector2I coord)
    {
        _zoneGrids.Remove(coord);
    }

    public Vector2[] FindPath(Vector2 fromWorld, Vector2 toWorld)
    {
        Vector2I fromZone = WorldToZoneCoord(fromWorld);
        Vector2I toZone = WorldToZoneCoord(toWorld);

        if (fromZone == toZone)
            return FindPathInZone(fromZone, WorldToLocal(fromWorld, fromZone), WorldToLocal(toWorld, toZone));

        var zoneSequence = FindZonePath(fromZone, toZone);
        if (zoneSequence == null || zoneSequence.Length == 0)
            return Array.Empty<Vector2>();

        var allWaypoints = new List<Vector2>();

        for (int i = 0; i < zoneSequence.Length; i++)
        {
            Vector2I curZone = zoneSequence[i];
            if (!_zoneGrids.TryGetValue(curZone, out var grid))
                return allWaypoints.Count > 0 ? allWaypoints.ToArray() : Array.Empty<Vector2>();

            Vector2 entryWorld = i == 0 ? fromWorld : GetZoneBoundaryPoint(curZone, zoneSequence[i - 1]);
            Vector2 exitWorld = i == zoneSequence.Length - 1 ? toWorld : GetZoneBoundaryPoint(curZone, zoneSequence[i + 1]);

            var segment = grid.FindPath(WorldToLocal(entryWorld, curZone), WorldToLocal(exitWorld, curZone));
            if (segment == null || segment.Length == 0)
                return allWaypoints.Count > 0 ? allWaypoints.ToArray() : Array.Empty<Vector2>();

            for (int j = 0; j < segment.Length; j++)
            {
                Vector2 wp = LocalToWorld(segment[j], curZone);
                if (allWaypoints.Count == 0 || allWaypoints[^1].DistanceSquaredTo(wp) > 0.1f)
                    allWaypoints.Add(wp);
            }
        }

        return allWaypoints.ToArray();
    }

    public Vector2[] FindPathInZone(Vector2I zoneCoord, Vector2 fromLocal, Vector2 toLocal)
    {
        if (!_zoneGrids.TryGetValue(zoneCoord, out var grid))
            return Array.Empty<Vector2>();

        var segment = grid.FindPath(fromLocal, toLocal);
        if (segment == null || segment.Length == 0)
            return Array.Empty<Vector2>();

        var worldPath = new Vector2[segment.Length];
        for (int i = 0; i < segment.Length; i++)
            worldPath[i] = LocalToWorld(segment[i], zoneCoord);

        return worldPath;
    }

    public bool IsWalkable(Vector2 worldPos)
    {
        Vector2I zoneCoord = WorldToZoneCoord(worldPos);
        if (!_zoneGrids.TryGetValue(zoneCoord, out var grid))
            return false;
        return grid.IsWalkable(WorldToLocal(worldPos, zoneCoord));
    }

    public Vector2 FindNearestWalkableCell(Vector2 worldPos)
    {
        Vector2I zoneCoord = WorldToZoneCoord(worldPos);
        if (!_zoneGrids.TryGetValue(zoneCoord, out var grid))
            return worldPos;
        return LocalToWorld(grid.FindNearestWalkable(WorldToLocal(worldPos, zoneCoord)), zoneCoord);
    }

    public Vector2[] GetRandomWalkablePositions(Vector2 center, float radius, int count)
    {
        var results = new List<Vector2>();
        int attempts = 0;
        while (results.Count < count && attempts < count * 20)
        {
            attempts++;
            float angle = (float)GD.RandRange(0, Mathf.Pi * 2);
            float dist = (float)GD.RandRange(0, radius);
            Vector2 candidate = center + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
            if (IsWalkable(candidate))
                results.Add(candidate);
        }
        return results.ToArray();
    }

    public Vector2I WorldToZoneCoord(Vector2 worldPos)
    {
        return new Vector2I(
            Mathf.RoundToInt(worldPos.X / ZoneSize),
            Mathf.RoundToInt(worldPos.Y / ZoneSize));
    }

    private static Vector2 WorldToLocal(Vector2 worldPos, Vector2I zoneCoord)
    {
        return worldPos - new Vector2(zoneCoord.X * 3424f, zoneCoord.Y * 3424f);
    }

    private static Vector2 LocalToWorld(Vector2 localPos, Vector2I zoneCoord)
    {
        return localPos + new Vector2(zoneCoord.X * 3424f, zoneCoord.Y * 3424f);
    }

    private static Vector2I[] FindZonePath(Vector2I from, Vector2I to)
    {
        if (from == to) return new[] { from };

        var visited = new HashSet<Vector2I> { from };
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var queue = new Queue<Vector2I>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == to)
            {
                var path = new List<Vector2I> { to };
                while (cameFrom.ContainsKey(path[^1]))
                    path.Add(cameFrom[path[^1]]);
                path.Reverse();
                return path.ToArray();
            }

            foreach (var delta in AdjacencyDeltas)
            {
                var neighbor = current + delta;
                if (!ZoneCoordinates.Contains(neighbor)) continue;
                if (!visited.Add(neighbor)) continue;
                cameFrom[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }

        return Array.Empty<Vector2I>();
    }

    private static Vector2 GetZoneBoundaryPoint(Vector2I zoneA, Vector2I zoneB)
    {
        Vector2I delta = zoneB - zoneA;
        Vector2 centerA = new(zoneA.X * 3424f, zoneA.Y * 3424f);
        return centerA + new Vector2(delta.X * 1712f, delta.Y * 1712f);
    }

    // ── WalkabilityGrid ─────────────────────────────────────────────

    private class WalkabilityGrid
    {
        private readonly bool[,] _solid;
        private readonly Vector2I _origin;
        private readonly int _cellSize;
        private readonly int _width;
        private readonly int _height;

        private WalkabilityGrid(bool[,] solid, Vector2I origin, int cellSize)
        {
            _solid = solid;
            _origin = origin;
            _cellSize = cellSize;
            _width = solid.GetLength(0);
            _height = solid.GetLength(1);
        }

        public static WalkabilityGrid Build(Node zoneRoot, Vector2I coord)
        {
            var layers = FindTileMapLayers(zoneRoot);
            if (layers.Count == 0) return null;

            TileSet tileSet = null;
            foreach (var layer in layers)
            {
                tileSet = layer.TileSet;
                if (tileSet != null) break;
            }
            if (tileSet == null && zoneRoot is TileMap tm)
                tileSet = tm.TileSet;

            int cellSize = tileSet?.TileSize.X ?? 16;

            Rect2I usedRect = default;
            bool rectSet = false;
            foreach (var layer in layers)
            {
                if (IsDecorationLayer(layer)) continue;
                var r = layer.GetUsedRect();
                if (r.Size == Vector2I.Zero) continue;
                usedRect = rectSet ? usedRect.Merge(r) : r;
                rectSet = true;
            }

            if (!rectSet) return null;

            // Expand by 1 cell in each direction for zone boundary margin
            Vector2I origin = usedRect.Position - Vector2I.One;
            int w = usedRect.Size.X + 2;
            int h = usedRect.Size.Y + 2;
            var solid = new bool[w, h];

            int solidCount = 0;
            foreach (var layer in layers)
            {
                if (IsDecorationLayer(layer)) continue;
                if (!IsObstacleLayer(layer) && !IsWaterLayer(layer)) continue;

                foreach (var cell in layer.GetUsedCells())
                {
                    int x = cell.X - origin.X;
                    int y = cell.Y - origin.Y;
                    if (x >= 0 && x < w && y >= 0 && y < h)
                    {
                        solid[x, y] = true;
                        solidCount++;
                    }
                }
            }

            GD.Print($"[Nav] Zone {coord}: size=({w},{h}) origin={origin} cellSize={cellSize} solids={solidCount}");
            return new WalkabilityGrid(solid, origin, cellSize);
        }

        public bool IsWalkable(Vector2 localPos)
        {
            var (x, y) = LocalToIndex(localPos);
            if (x < 0 || x >= _width || y < 0 || y >= _height) return false;
            return !_solid[x, y];
        }

        public Vector2[] FindPath(Vector2 fromLocal, Vector2 toLocal)
        {
            var (fx, fy) = LocalToIndex(fromLocal);
            var (tx, ty) = LocalToIndex(toLocal);

            // Clamp out-of-bounds cells
            if (fx < 0 || fx >= _width || fy < 0 || fy >= _height)
                (fx, fy) = FindNearestWalkableIndex(fx, fy);
            if (tx < 0 || tx >= _width || ty < 0 || ty >= _height)
                (tx, ty) = FindNearestWalkableIndex(tx, ty);

            // Only clamp start if solid (can't start on a wall)
            if (_solid[fx, fy]) (fx, fy) = FindNearestWalkableIndex(fx, fy);

            // If target is solid, path to nearest walkable then add actual target at end
            int origTx = tx, origTy = ty;
            if (_solid[tx, ty])
            {
                var (ntx, nty) = FindNearestWalkableIndex(tx, ty);
                tx = ntx; ty = nty;
            }

            var cells = AStar.FindPath(_solid, _width, _height, fx, fy, tx, ty);
            if (cells == null || cells.Count == 0)
                return Array.Empty<Vector2>();

            cells = SmoothPath(cells);

            if (_solid[origTx, origTy])
                cells.Add((origTx, origTy));

            var result = new Vector2[cells.Count];
            for (int i = 0; i < cells.Count; i++)
                result[i] = IndexToLocal(cells[i].Item1, cells[i].Item2);
            return result;
        }

        public Vector2 FindNearestWalkable(Vector2 localPos)
        {
            var (x, y) = LocalToIndex(localPos);
            if (x >= 0 && x < _width && y >= 0 && y < _height && !_solid[x, y])
                return localPos;
            var (nx, ny) = FindNearestWalkableIndex(x, y);
            return IndexToLocal(nx, ny);
        }

        private (int, int) LocalToIndex(Vector2 localPos)
        {
            return (
                Mathf.FloorToInt(localPos.X / _cellSize) - _origin.X,
                Mathf.FloorToInt(localPos.Y / _cellSize) - _origin.Y
            );
        }

        private Vector2 IndexToLocal(int x, int y)
        {
            return new Vector2(
                (x + _origin.X) * _cellSize + _cellSize * 0.5f,
                (y + _origin.Y) * _cellSize + _cellSize * 0.5f
            );
        }

        private List<(int, int)> SmoothPath(List<(int, int)> path)
        {
            if (path.Count <= 2) return path;

            var smoothed = new List<(int, int)> { path[0] };
            int anchor = 0;

            for (int i = 1; i < path.Count; i++)
            {
                if (i == path.Count - 1 || !LineOfSight(path[anchor].Item1, path[anchor].Item2, path[i + 1].Item1, path[i + 1].Item2))
                {
                    smoothed.Add(path[i]);
                    anchor = i;
                }
            }

            return smoothed;
        }

        private bool LineOfSight(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int stepX = x0 < x1 ? 1 : -1;
            int stepY = y0 < y1 ? 1 : -1;
            float tDeltaX = dx > 0 ? 1f / dx : float.MaxValue;
            float tDeltaY = dy > 0 ? 1f / dy : float.MaxValue;
            float tMaxX = dx > 0 ? (0.5f + 0.5f * stepX) * tDeltaX : float.MaxValue;
            float tMaxY = dy > 0 ? (0.5f + 0.5f * stepY) * tDeltaY : float.MaxValue;

            int x = x0, y = y0;
            while (x != x1 || y != y1)
            {
                if (tMaxX < tMaxY)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    y += stepY;
                    tMaxY += tDeltaY;
                }

                if ((x != x1 || y != y1) && _solid[x, y])
                    return false;
            }

            return true;
        }

        private (int, int) FindNearestWalkableIndex(int sx, int sy)
        {
            if (sx >= 0 && sx < _width && sy >= 0 && sy < _height && !_solid[sx, sy])
                return (sx, sy);

            for (int r = 1; r <= 30; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        int nx = sx + dx;
                        int ny = sy + dy;
                        if (nx >= 0 && nx < _width && ny >= 0 && ny < _height && !_solid[nx, ny])
                            return (nx, ny);
                    }
                }
            }

            return (sx, sy);
        }

        private static List<TileMapLayer> FindTileMapLayers(Node root)
        {
            var result = new List<TileMapLayer>();
            FindTileMapLayersRecursive(root, result);
            return result;
        }

        private static void FindTileMapLayersRecursive(Node node, List<TileMapLayer> result)
        {
            if (node is TileMapLayer layer)
                result.Add(layer);
            foreach (var child in node.GetChildren())
                FindTileMapLayersRecursive(child, result);
        }

        private static bool IsDecorationLayer(TileMapLayer layer)
        {
            string name = layer.Name.ToString().ToLowerInvariant();
            return name.Contains("y-sort") || name.Contains("ysort");
        }

        private static bool IsObstacleLayer(TileMapLayer layer)
        {
            return layer.Name.ToString().ToLowerInvariant().Contains("ground2");
        }

        private static bool IsWaterLayer(TileMapLayer layer)
        {
            return layer.Name.ToString().ToLowerInvariant().Contains("water");
        }
    }

    // ── A* Pathfinder ───────────────────────────────────────────────

    private static class AStar
    {
        private static readonly (int dx, int dy, float cost)[] Neighbors =
        {
            ( 0, -1, 1f), ( 1,  0, 1f), ( 0,  1, 1f), (-1,  0, 1f),  // cardinal
            (-1, -1, 1.414f), ( 1, -1, 1.414f), ( 1,  1, 1.414f), (-1,  1, 1.414f),  // diagonal
        };

        public static List<(int, int)> FindPath(bool[,] solid, int w, int h, int sx, int sy, int tx, int ty)
        {
            if (sx < 0 || sx >= w || sy < 0 || sy >= h) return null;
            if (tx < 0 || tx >= w || ty < 0 || ty >= h) return null;
            if (solid[sx, sy] || solid[tx, ty]) return null;

            var open = new PriorityQueue<int, float>();
            var gScore = new Dictionary<int, float>();
            var cameFrom = new Dictionary<int, int>();
            var closed = new HashSet<int>();

            int startKey = sy * w + sx;
            int targetKey = ty * w + tx;

            open.Enqueue(startKey, 0);
            gScore[startKey] = 0;

            while (open.Count > 0)
            {
                int current = open.Dequeue();
                if (!closed.Add(current)) continue;
                if (current == targetKey)
                    return ReconstructPath(cameFrom, current, w);

                int cx = current % w;
                int cy = current / w;
                float currentG = gScore[current];

                foreach (var (dx, dy, moveCost) in Neighbors)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    if (solid[nx, ny]) continue;

                    // Block diagonal if either adjacent cardinal is solid (no corner cutting)
                    if (dx != 0 && dy != 0)
                    {
                        if (solid[cx + dx, cy] || solid[cx, cy + dy]) continue;
                    }

                    int neighborKey = ny * w + nx;
                    float tentativeG = currentG + moveCost;

                    if (!gScore.TryGetValue(neighborKey, out float existing) || tentativeG < existing)
                    {
                        gScore[neighborKey] = tentativeG;
                        cameFrom[neighborKey] = current;
                        float heuristic = Heuristic(nx, ny, tx, ty);
                        open.Enqueue(neighborKey, tentativeG + heuristic);
                    }
                }
            }

            return null;
        }

        private static float Heuristic(int x1, int y1, int x2, int y2)
        {
            int dx = Math.Abs(x1 - x2);
            int dy = Math.Abs(y1 - y2);
            return (dx + dy) + (1.414f - 2f) * Math.Min(dx, dy);
        }

        private static List<(int, int)> ReconstructPath(Dictionary<int, int> cameFrom, int current, int w)
        {
            var path = new List<(int, int)>();
            while (cameFrom.ContainsKey(current))
            {
                path.Add((current % w, current / w));
                current = cameFrom[current];
            }
            path.Reverse();
            return path;
        }
    }
}
