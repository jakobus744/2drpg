using System;
using System.Collections.Generic;
using Godot;

namespace RPG2d.World;

public partial class NavigationManager : Node
{
    private readonly Dictionary<Vector2I, NavigationRegion2D> _zoneRegions = new();

    [Export] public float AgentRadius { get; set; } = 12.0f;

    public async void RegisterZone(Vector2I coord, Node zoneRoot)
    {
        if (zoneRoot == null || _zoneRegions.ContainsKey(coord))
            return;

        // Wait a frame for node transforms to settle in the tree
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (!GodotObject.IsInstanceValid(zoneRoot) || _zoneRegions.ContainsKey(coord))
            return;

        var existingRegion = zoneRoot.GetNodeOrNull<NavigationRegion2D>("ZoneNavRegion");
        if (existingRegion != null)
        {
            _zoneRegions[coord] = existingRegion;
            return;
        }

        var layers = new List<TileMapLayer>();
        CollectLayers(zoneRoot, layers);

        Rect2I bounds = default;
        bool hasBounds = false;
        int cellSize = 16;

        foreach (var layer in layers)
        {
            var used = layer.GetUsedRect();
            if (used.Size.X <= 0 || used.Size.Y <= 0)
                continue;
            bounds = hasBounds ? Union(bounds, used) : used;
            hasBounds = true;
            if (layer.TileSet != null)
                cellSize = Math.Max(cellSize, layer.TileSet.TileSize.X);
        }

        var navPoly = new NavigationPolygon();
        navPoly.AgentRadius = AgentRadius;
        navPoly.ParsedGeometryType = NavigationPolygon.ParsedGeometryTypeEnum.StaticColliders;
        navPoly.SourceGeometryMode = NavigationPolygon.SourceGeometryModeEnum.RootNodeChildren;
        navPoly.ParsedCollisionMask = 1;

        var sourceGeometry = new NavigationMeshSourceGeometryData2D();

        // ParseSourceGeometryData clears sourceGeometry first, so outlines are added after
        NavigationServer2D.ParseSourceGeometryData(navPoly, sourceGeometry, zoneRoot);

        if (hasBounds)
        {
            float margin = cellSize * 2;
            float minX = bounds.Position.X * cellSize - margin;
            float minY = bounds.Position.Y * cellSize - margin;
            float maxX = (bounds.Position.X + bounds.Size.X) * cellSize + margin;
            float maxY = (bounds.Position.Y + bounds.Size.Y) * cellSize + margin;

            sourceGeometry.AddTraversableOutline(new Vector2[]
            {
                new(minX, minY),
                new(minX, maxY),
                new(maxX, maxY),
                new(maxX, minY)
            });
        }

        NavigationServer2D.BakeFromSourceGeometryData(navPoly, sourceGeometry);

        var region = new NavigationRegion2D();
        region.Name = "ZoneNavRegion";
        region.NavigationPolygon = navPoly;

        zoneRoot.AddChild(region);

        _zoneRegions[coord] = region;
        GD.Print($"[NavServer] Zone {coord}: Baked NavMesh ({navPoly.GetPolygonCount()} polygons, bounds: {bounds})");
    }

    public void UnregisterZone(Vector2I coord)
    {
        if (_zoneRegions.TryGetValue(coord, out var region))
        {
            if (GodotObject.IsInstanceValid(region))
                region.QueueFree();
            _zoneRegions.Remove(coord);
            GD.Print($"[NavServer] Unregistered zone {coord}");
        }
    }

    public Vector2[] FindPath(Vector2 fromWorld, Vector2 toWorld)
    {
        var map = GetViewport()?.GetWorld2D()?.NavigationMap ?? default;
        if (map.Id == 0)
            return Array.Empty<Vector2>();

        return NavigationServer2D.MapGetPath(map, fromWorld, toWorld, true);
    }

    public bool IsWalkable(Vector2 worldPos)
    {
        var map = GetViewport()?.GetWorld2D()?.NavigationMap ?? default;
        if (map.Id == 0) return true;

        var closest = NavigationServer2D.MapGetClosestPoint(map, worldPos);
        return closest.DistanceSquaredTo(worldPos) < 32f * 32f;
    }

    public Vector2 FindNearestWalkableCell(Vector2 worldPos)
    {
        var map = GetViewport()?.GetWorld2D()?.NavigationMap ?? default;
        if (map.Id == 0) return worldPos;

        return NavigationServer2D.MapGetClosestPoint(map, worldPos);
    }

    public Vector2[] GetRandomWalkablePositions(Vector2 center, float radius, int count)
    {
        if (count <= 0 || radius <= 0f)
            return Array.Empty<Vector2>();

        var map = GetViewport()?.GetWorld2D()?.NavigationMap ?? default;
        var positions = new List<Vector2>(count);
        int maxAttempts = Math.Max(32, count * 64);

        for (int i = 0; i < maxAttempts && positions.Count < count; i++)
        {
            float angle = (float)GD.RandRange(0.0, Math.PI * 2.0);
            float dist = radius * Mathf.Sqrt(GD.Randf());
            var candidate = center + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);

            if (map.Id != 0)
            {
                var closest = NavigationServer2D.MapGetClosestPoint(map, candidate);
                if (closest.DistanceSquaredTo(candidate) < 24f * 24f)
                    positions.Add(closest);
            }
            else
            {
                positions.Add(candidate);
            }
        }

        return positions.ToArray();
    }

    private static void CollectLayers(Node node, List<TileMapLayer> layers)
    {
        if (node is TileMapLayer layer)
            layers.Add(layer);
        foreach (Node child in node.GetChildren())
            CollectLayers(child, layers);
    }

    private static Rect2I Union(Rect2I a, Rect2I b)
    {
        int left = Math.Min(a.Position.X, b.Position.X);
        int top = Math.Min(a.Position.Y, b.Position.Y);
        int right = Math.Max(a.Position.X + a.Size.X, b.Position.X + b.Size.X);
        int bottom = Math.Max(a.Position.Y + a.Size.Y, b.Position.Y + b.Size.Y);
        return new Rect2I(left, top, right - left, bottom - top);
    }
}
