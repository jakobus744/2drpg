using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RPG2d.World;

public partial class NavigationManager : Node
{
    private static readonly Vector2I[] ZoneSteps = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private const string BlockedNameParts = "wall,obstacle,collision,water,ground2";
    private const string IgnoredNameParts = "ysort,y-sort,decoration,detail";
    private const int NearestSearchLimit = 48;

    private readonly Dictionary<Vector2I, ZoneData> _zones = new();

    private sealed class ZoneData
    {
        public AStarGrid2D Grid;
        public TileMapLayer Layer;
        public Vector2I CellOffset;
    }

    // Zone registration

    public void RegisterZone(Vector2I coord, Node zoneRoot)
    {
        if (zoneRoot == null)
            return;

        var layers = new List<TileMapLayer>();
        CollectLayers(zoneRoot, layers);
        if (layers.Count == 0)
            return;

        var blockedParts = BlockedNameParts.Split(',', StringSplitOptions.TrimEntries);
        var ignoredParts = IgnoredNameParts.Split(',', StringSplitOptions.TrimEntries);

        var refLayer = layers.FirstOrDefault(l => !NameMatches(l.Name, ignoredParts));
        if (refLayer == null)
            return;

        int cellSize = Math.Max(1, refLayer.TileSet?.TileSize.X ?? 16);

        Rect2I bounds = default;
        bool hasBounds = false;
        foreach (var layer in layers)
        {
            if (NameMatches(layer.Name, ignoredParts))
                continue;
            var used = layer.GetUsedRect();
            if (used.Size.X <= 0 || used.Size.Y <= 0)
                continue;
            bounds = hasBounds ? Union(bounds, used) : used;
            hasBounds = true;
        }
        if (!hasBounds)
            return;

        var cellOffset = -bounds.Position;

        var astar = new AStarGrid2D();
        astar.Region = new Rect2I(Vector2I.Zero, bounds.Size);
        astar.CellSize = new Vector2(cellSize, cellSize);
        astar.DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles;
        astar.Update();

        foreach (var layer in layers)
        {
            if (NameMatches(layer.Name, ignoredParts) || !NameMatches(layer.Name, blockedParts))
                continue;
            foreach (var cell in layer.GetUsedCells())
            {
                var offsetCell = cell + cellOffset;
                if (astar.IsInBounds(offsetCell.X, offsetCell.Y))
                    astar.SetPointSolid(offsetCell);
            }
        }

        var data = new ZoneData { Grid = astar, Layer = refLayer, CellOffset = cellOffset };
        _zones[coord] = data;

        MarkPhysicsBodies(data, zoneRoot);
        InflateBlocked(astar);

        GD.Print($"[Nav] zone {coord}: cellSize={cellSize}");

        int blocked = 0;
        foreach (var layer in layers)
        {
            if (NameMatches(layer.Name, ignoredParts) || !NameMatches(layer.Name, blockedParts))
                continue;
            blocked += layer.GetUsedCells().Count;
        }
        if (blocked > 0)
            GD.Print($"[Nav] zone {coord}: {blocked} blocked cells across layers");
    }

    public void UnregisterZone(Vector2I coord)
    {
        _zones.Remove(coord);
    }

    // Public API

    public Vector2[] FindPath(Vector2 fromWorld, Vector2 toWorld)
    {
        if (!TryFindZone(fromWorld, out var startZone, out var startData))
        {
            GD.Print($"[Nav] FindPath failed: no zone for fromWorld={fromWorld}");
            return Array.Empty<Vector2>();
        }
        if (!TryFindZone(toWorld, out var targetZone, out var targetData))
        {
            GD.Print($"[Nav] FindPath failed: no zone for toWorld={toWorld}");
            return Array.Empty<Vector2>();
        }

        if (startZone == targetZone)
            return FindPathInZone(startData, fromWorld, toWorld);

        var zoneRoute = FindZoneRoute(startZone, targetZone);
        if (zoneRoute.Length == 0)
        {
            GD.Print($"[Nav] No zone route from {startZone} to {targetZone}");
            return Array.Empty<Vector2>();
        }

        var points = new List<Vector2>();

        for (int i = 0; i < zoneRoute.Length; i++)
        {
            if (!_zones.TryGetValue(zoneRoute[i], out var data))
                return Array.Empty<Vector2>();

            Vector2 entry = i == 0 ? fromWorld : GetBorderPoint(zoneRoute[i], zoneRoute[i - 1]);
            Vector2 exit = i == zoneRoute.Length - 1 ? toWorld : GetBorderPoint(zoneRoute[i], zoneRoute[i + 1]);

            var segment = FindPathInZone(data, entry, exit);
            if (segment.Length == 0)
            {
                GD.Print($"[Nav] Cross-zone segment {i} failed in zone {zoneRoute[i]}: entry={entry} exit={exit}");
                return Array.Empty<Vector2>();
            }

            foreach (var pt in segment)
            {
                if (points.Count == 0 || points[^1].DistanceSquaredTo(pt) > 0.25f)
                    points.Add(pt);
            }
        }

        return points.ToArray();
    }

    public bool IsWalkable(Vector2 worldPos)
    {
        if (!TryFindZone(worldPos, out _, out var data))
            return false;
        var cell = WorldToCell(worldPos, data);
        return data.Grid.IsInBounds(cell.X, cell.Y) && !data.Grid.IsPointSolid(cell);
    }

    public Vector2 FindNearestWalkableCell(Vector2 worldPos)
    {
        if (!TryFindZone(worldPos, out _, out var data))
            return worldPos;
        var cell = WorldToCell(worldPos, data);
        if (TryFindNearestWalkable(data.Grid, cell, out var nearest))
            return CellToWorld(nearest, data);
        return worldPos;
    }

    public Vector2[] GetRandomWalkablePositions(Vector2 center, float radius, int count)
    {
        if (count <= 0 || radius <= 0f)
            return Array.Empty<Vector2>();

        var positions = new List<Vector2>(count);
        int maxAttempts = Math.Max(32, count * 64);

        for (int i = 0; i < maxAttempts && positions.Count < count; i++)
        {
            float angle = (float)GD.RandRange(0.0, Math.PI * 2.0);
            float dist = radius * Mathf.Sqrt(GD.Randf());
            var candidate = center + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
            if (IsWalkable(candidate))
                positions.Add(candidate);
        }

        return positions.ToArray();
    }

    // Single-zone path

    private Vector2[] FindPathInZone(ZoneData data, Vector2 fromWorld, Vector2 toWorld)
    {
        var fromCell = WorldToCell(fromWorld, data);
        var toCell = WorldToCell(toWorld, data);

        if (!TryFindNearestWalkable(data.Grid, fromCell, out var start))
            return Array.Empty<Vector2>();
        if (!TryFindNearestWalkable(data.Grid, toCell, out var target))
            return Array.Empty<Vector2>();
        if (!data.Grid.IsInBounds(start.X, start.Y) || !data.Grid.IsInBounds(target.X, target.Y))
            return Array.Empty<Vector2>();
        if (data.Grid.IsPointSolid(start) || data.Grid.IsPointSolid(target))
            return Array.Empty<Vector2>();

        var cells = FindPathOnGrid(data.Grid, start, target);
        if (cells.Count == 0)
            return Array.Empty<Vector2>();

        cells = RemoveCollinear(cells);

        var points = new Vector2[cells.Count];
        for (int i = 0; i < cells.Count; i++)
            points[i] = CellToWorld(cells[i], data);

        return points;
    }

    // Coordinate conversion using TileMapLayer transform hierarchy

    private static Vector2I WorldToCell(Vector2 worldPos, ZoneData data)
    {
        return data.Layer.LocalToMap(data.Layer.ToLocal(worldPos)) + data.CellOffset;
    }

    private static Vector2 CellToWorld(Vector2I cell, ZoneData data)
    {
        return data.Layer.ToGlobal(data.Layer.MapToLocal(cell - data.CellOffset));
    }

    // Zone lookup

    private int _zoneSize = 3424;

    public override void _Ready()
    {
        var wm = GetNodeOrNull<WorldManager>("/root/MainWorld");
        if (wm != null)
            _zoneSize = wm.ZoneSize;
    }

    private bool TryFindZone(Vector2 worldPos, out Vector2I coord, out ZoneData data)
    {
        coord = new Vector2I(
            Mathf.RoundToInt(worldPos.X / _zoneSize),
            Mathf.RoundToInt(worldPos.Y / _zoneSize));

        if (_zones.TryGetValue(coord, out data))
            return true;

        foreach (var step in ZoneSteps)
        {
            if (_zones.TryGetValue(coord + step, out data))
                return true;
        }

        foreach (var (zCoord, zData) in _zones)
        {
            coord = zCoord;
            data = zData;
            return true;
        }

        data = null;
        return false;
    }

    // Cross-zone routing

    private Vector2I[] FindZoneRoute(Vector2I start, Vector2I target)
    {
        if (!_zones.ContainsKey(start) || !_zones.ContainsKey(target))
            return Array.Empty<Vector2I>();

        var queue = new Queue<Vector2I>();
        var visited = new HashSet<Vector2I> { start };
        var previous = new Dictionary<Vector2I, Vector2I>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target)
            {
                var route = new List<Vector2I> { target };
                while (previous.TryGetValue(route[^1], out var parent))
                    route.Add(parent);
                route.Reverse();
                return route.ToArray();
            }

            foreach (var step in ZoneSteps)
            {
                var next = current + step;
                if (!_zones.ContainsKey(next) || !visited.Add(next))
                    continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        return Array.Empty<Vector2I>();
    }

    private Vector2 GetBorderPoint(Vector2I fromZone, Vector2I toZone)
    {
        if (!_zones.TryGetValue(fromZone, out var data))
            return Vector2.Zero;

        Vector2I dir = toZone - fromZone;
        var region = data.Grid.Region;

        int edgeX = dir.X > 0 ? region.Size.X - 1 :
                    dir.X < 0 ? 0 :
                    region.Size.X / 2;
        int edgeY = dir.Y > 0 ? region.Size.Y - 1 :
                    dir.Y < 0 ? 0 :
                    region.Size.Y / 2;

        var edgeCell = new Vector2I(edgeX, edgeY);
        if (TryFindNearestWalkable(data.Grid, edgeCell, out var nearest))
            return CellToWorld(nearest, data);

        return CellToWorld(edgeCell, data);
    }

    // Nearest-walkable search

    private static bool TryFindNearestWalkable(AStarGrid2D grid, Vector2I start, out Vector2I result)
    {
        if (grid.IsInBounds(start.X, start.Y) && !grid.IsPointSolid(start))
        {
            result = start;
            return true;
        }

        for (int radius = 1; radius <= NearestSearchLimit; radius++)
        {
            int minX = start.X - radius, maxX = start.X + radius;
            int minY = start.Y - radius, maxY = start.Y + radius;

            for (int x = minX; x <= maxX; x++)
            {
                if (TryCell(grid, new Vector2I(x, minY), out result) ||
                    TryCell(grid, new Vector2I(x, maxY), out result))
                    return true;
            }
            for (int y = minY + 1; y <= maxY - 1; y++)
            {
                if (TryCell(grid, new Vector2I(minX, y), out result) ||
                    TryCell(grid, new Vector2I(maxX, y), out result))
                    return true;
            }
        }

        result = default;
        return false;
    }

    private static bool TryCell(AStarGrid2D grid, Vector2I cell, out Vector2I result)
    {
        if (grid.IsInBounds(cell.X, cell.Y) && !grid.IsPointSolid(cell))
        {
            result = cell;
            return true;
        }
        result = default;
        return false;
    }

    // Pathfinding on AStarGrid2D solid data

    private static readonly Vector2I[] PathSteps =
    {
        new(0, -1), new(1, 0), new(0, 1), new(-1, 0),
        new(-1, -1), new(1, -1), new(1, 1), new(-1, 1),
    };

    private static List<Vector2I> FindPathOnGrid(AStarGrid2D grid, Vector2I start, Vector2I target)
    {
        if (start == target)
            return new List<Vector2I> { start };

        var open = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var costSoFar = new Dictionary<Vector2I, float>();
        var closed = new HashSet<Vector2I>();

        open.Enqueue(start, 0f);
        costSoFar[start] = 0f;
        int iterations = 0;
        const int maxIterations = 5000;

        while (open.Count > 0 && ++iterations < maxIterations)
        {
            var current = open.Dequeue();
            if (!closed.Add(current))
                continue;

            if (current == target)
            {
                var path = new List<Vector2I> { current };
                while (cameFrom.TryGetValue(current, out var prev))
                {
                    current = prev;
                    path.Add(current);
                }
                path.Reverse();
                return path;
            }

            for (int i = 0; i < PathSteps.Length; i++)
            {
                var next = current + PathSteps[i];
                if (!grid.IsInBounds(next.X, next.Y) || grid.IsPointSolid(next) || closed.Contains(next))
                    continue;

                bool diagonal = i >= 4;
                if (diagonal)
                {
                    var horiz = current + new Vector2I(PathSteps[i].X, 0);
                    var vert = current + new Vector2I(0, PathSteps[i].Y);
                    if (grid.IsPointSolid(horiz) || grid.IsPointSolid(vert))
                        continue;
                }

                float stepCost = diagonal ? 1.4142135f : 1f;
                float newCost = costSoFar[current] + stepCost;

                if (costSoFar.TryGetValue(next, out var known) && newCost >= known)
                    continue;

                costSoFar[next] = newCost;
                cameFrom[next] = current;
                float priority = newCost + Heuristic(next, target);
                open.Enqueue(next, priority);
            }
        }

        return new List<Vector2I>();
    }

    private static float Heuristic(Vector2I a, Vector2I b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        int diag = Math.Min(dx, dy);
        int straight = Math.Max(dx, dy) - diag;
        return diag * 1.4142135f + straight;
    }

    // Helpers

    private static void MarkPhysicsBodies(ZoneData data, Node root)
    {
        var bodies = new List<Node>();
        CollectBodies(root, bodies);

        int marked = 0;

        foreach (var body in bodies)
        {
            if (body is not CollisionShape2D shape || shape.Shape == null)
                continue;

            if (IsMobShape(shape))
                continue;

            Vector2 halfSize;

            if (shape.Shape is RectangleShape2D rect)
                halfSize = rect.Size * 0.5f;
            else if (shape.Shape is CircleShape2D circle)
                halfSize = new Vector2(circle.Radius, circle.Radius);
            else if (shape.Shape is CapsuleShape2D capsule)
                halfSize = new Vector2(capsule.Radius, capsule.Radius + capsule.Height * 0.5f);
            else
                continue;

            var worldCenter = shape.GlobalPosition;
            var tileTopLeft = data.Layer.LocalToMap(data.Layer.ToLocal(worldCenter - halfSize));
            var tileBottomRight = data.Layer.LocalToMap(data.Layer.ToLocal(worldCenter + halfSize));
            var topLeft = tileTopLeft + data.CellOffset;
            var bottomRight = tileBottomRight + data.CellOffset;

            for (int x = topLeft.X; x <= bottomRight.X; x++)
                for (int y = topLeft.Y; y <= bottomRight.Y; y++)
                    if (data.Grid.IsInBounds(x, y) && !data.Grid.IsPointSolid(new Vector2I(x, y)))
                    {
                        data.Grid.SetPointSolid(new Vector2I(x, y));
                        marked++;
                    }
        }

        if (marked > 0)
            GD.Print($"[Nav] Marked {marked} physics cells blocked");
    }

    private static bool IsMobShape(CollisionShape2D shape)
    {
        var parent = shape.GetParent();
        while (parent != null)
        {
            if (parent is CharacterBody2D)
                return true;
            parent = parent.GetParent();
        }
        return false;
    }

    private static readonly Vector2I[] NeighborOffsets =
    {
        new(-1,-1), new(0,-1), new(1,-1),
        new(-1, 0),            new(1, 0),
        new(-1, 1), new(0, 1), new(1, 1),
    };

    private static void InflateBlocked(AStarGrid2D grid)
    {
        var region = grid.Region;
        var toBlock = new HashSet<Vector2I>();

        for (int x = 0; x < region.Size.X; x++)
        {
            for (int y = 0; y < region.Size.Y; y++)
            {
                var cell = new Vector2I(x, y);
                if (!grid.IsPointSolid(cell))
                    continue;

                foreach (var offset in NeighborOffsets)
                {
                    var neighbor = cell + offset;
                    if (grid.IsInBounds(neighbor.X, neighbor.Y) && !grid.IsPointSolid(neighbor))
                        toBlock.Add(neighbor);
                }
            }
        }

        foreach (var cell in toBlock)
            grid.SetPointSolid(cell);
    }

    private static void CollectBodies(Node node, List<Node> bodies)
    {
        if (node is CollisionShape2D)
            bodies.Add(node);
        foreach (Node child in node.GetChildren())
            CollectBodies(child, bodies);
    }

    private static void CollectLayers(Node node, List<TileMapLayer> layers)
    {
        if (node is TileMapLayer layer)
            layers.Add(layer);
        foreach (Node child in node.GetChildren())
            CollectLayers(child, layers);
    }

    private static bool NameMatches(string name, string[] parts)
    {
        var lower = name.ToLowerInvariant();
        foreach (var p in parts)
            if (lower.Contains(p.Trim().ToLowerInvariant()))
                return true;
        return false;
    }

    private static Rect2I Union(Rect2I a, Rect2I b)
    {
        int left = Math.Min(a.Position.X, b.Position.X);
        int top = Math.Min(a.Position.Y, b.Position.Y);
        int right = Math.Max(a.Position.X + a.Size.X, b.Position.X + b.Size.X);
        int bottom = Math.Max(a.Position.Y + a.Size.Y, b.Position.Y + b.Size.Y);
        return new Rect2I(left, top, right - left, bottom - top);
    }

    // Path simplification

    private static List<Vector2I> RemoveCollinear(List<Vector2I> cells)
    {
        if (cells.Count <= 2)
            return cells;

        var result = new List<Vector2I> { cells[0] };
        var prevDir = Direction(cells[0], cells[1]);

        for (int i = 2; i < cells.Count; i++)
        {
            var dir = Direction(cells[i - 1], cells[i]);
            if (dir != prevDir)
            {
                result.Add(cells[i - 1]);
                prevDir = dir;
            }
        }

        result.Add(cells[^1]);
        return result;
    }

    private static Vector2I Direction(Vector2I from, Vector2I to)
    {
        var delta = to - from;
        return new Vector2I(Math.Sign(delta.X), Math.Sign(delta.Y));
    }
}
