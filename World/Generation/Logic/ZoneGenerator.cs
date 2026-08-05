using System.Collections.Generic;
using System.Linq;
using Godot;
using RPG2d.World.Generation.Data;

namespace RPG2d.World.Generation.Logic;

public partial class ZoneGenerator : Node
{
    [Export] public ZoneSettings Settings { get; set; }
    [Export] public TileMapLayer GroundLayer { get; set; }
    [Export] public int ZoneTileSize { get; set; } = 64;

    [ExportGroup("Path Options")]
    [Export] public bool ConnectToZoneBorders { get; set; } = true;
    [Export] public int PathWidth { get; set; } = 2;
    [Export] public bool UseTerrainAutoTiling { get; set; } = true;

    [ExportGroup("Background Options")]
    [Export] public bool EnableBackground { get; set; } = true;
    [Export] public bool UseGradientBackground { get; set; } = true;
    [Export] public bool FillGroundTiles { get; set; } = false;

    [ExportGroup("Seed Options")]
    [Export] public string SeedString { get; set; } = "1337";

    public override void _Ready()
    {
        GroundLayer = AutoDetectGroundLayer(GroundLayer);
        if (GroundLayer != null && Settings != null)
        {
            GenerateZone(GroundLayer, Settings, Vector2I.Zero);
        }
        else if (GroundLayer == null)
        {
            GD.PrintErr("[ZoneGenerator] Kein GroundLayer gefunden oder auto-detektiert!");
        }
    }

    private TileMapLayer AutoDetectGroundLayer(TileMapLayer assigned)
    {
        if (assigned != null) return assigned;

        Node parent = GetParent();
        if (parent == null) return null;

        var found = parent.FindChild("GroundLayer", recursive: true, owned: false) as TileMapLayer;
        if (found != null) return found;

        found = parent.FindChild("ground", recursive: true, owned: false) as TileMapLayer;
        if (found != null) return found;

        return FindFirstTileMapLayer(parent);
    }

    private TileMapLayer FindFirstTileMapLayer(Node node)
    {
        if (node is TileMapLayer tml) return tml;
        foreach (Node child in node.GetChildren())
        {
            var f = FindFirstTileMapLayer(child);
            if (f != null) return f;
        }
        return null;
    }

    public int GetEffectiveZoneTileSize()
    {
        int zoneSizePx = WorldManager.Instance != null ? WorldManager.Instance.ZoneSize : 3424;
        int tileSizePx = GroundLayer?.TileSet != null ? GroundLayer.TileSet.TileSize.X : 32;
        if (tileSizePx <= 0) tileSizePx = 32;
        return Mathf.CeilToInt((float)zoneSizePx / tileSizePx);
    }

    public HashSet<Vector2I> GenerateZone(TileMapLayer groundLayer, ZoneSettings settings, Vector2I zoneCoord)
    {
        GroundLayer = AutoDetectGroundLayer(groundLayer ?? GroundLayer);
        Settings = settings ?? Settings;

        if (GroundLayer == null || Settings == null)
        {
            GD.PrintErr("[ZoneGenerator] GroundLayer oder ZoneSettings fehlen!");
            return new HashSet<Vector2I>();
        }

        ZoneTileSize = GetEffectiveZoneTileSize();

        if (zoneCoord == Vector2I.Zero)
        {
            Node parent = GetParent();
            ZoneBackground bg = parent?.GetNodeOrNull<ZoneBackground>("ZoneBackground");
            if (bg != null && bg.ZoneCoord != Vector2I.Zero)
            {
                zoneCoord = bg.ZoneCoord;
            }
            else if (parent is Node2D parent2D)
            {
                zoneCoord = WorldManager.WorldToZoneCell(parent2D.GlobalPosition);
            }
        }

        int baseSeed = WorldManager.Instance != null 
            ? WorldManager.Instance.WorldSeed 
            : SeedUtils.ParseSeed(SeedString, 1337);

        int zoneSeed = SeedUtils.DeriveSeed(baseSeed, zoneCoord);

        if (EnableBackground)
        {
            CreateOrUpdateBackground(zoneCoord);
        }

        HashSet<Vector2I> reservedCells = new HashSet<Vector2I>();

        ScanHandPlacedTiles(GroundLayer, reservedCells);
        var (borderWaypoints, internalWaypoints) = CollectWaypoints(GroundLayer, Settings, reservedCells, zoneCoord);

        if (FillGroundTiles)
        {
            FillGround(GroundLayer, Settings, reservedCells);
        }

        GeneratePaths(GroundLayer, Settings, borderWaypoints, internalWaypoints, reservedCells, zoneSeed);
        GenerateFoliage(GroundLayer, Settings, reservedCells, zoneSeed);

        return reservedCells;
    }

    private void CreateOrUpdateBackground(Vector2I zoneCoord)
    {
        Node parent = GetParent();
        if (parent == null) return;

        ZoneBackground bg = parent.GetNodeOrNull<ZoneBackground>("ZoneBackground");
        if (bg == null)
        {
            bg = new ZoneBackground { Name = "ZoneBackground" };
            parent.CallDeferred(Node.MethodName.AddChild, bg);
        }

        int zoneSize = WorldManager.Instance != null ? WorldManager.Instance.ZoneSize : ZoneTileSize * 16;
        bg.Setup(Settings, zoneSize, zoneCoord, UseGradientBackground);
    }

    private void FillGround(TileMapLayer layer, ZoneSettings settings, HashSet<Vector2I> reserved)
    {
        if (layer == null || settings == null) return;

        int halfTiles = ZoneTileSize / 2;
        Vector2I groundCoords = settings.GroundTileCoords;

        for (int x = -halfTiles; x <= halfTiles; x++)
        {
            for (int y = -halfTiles; y <= halfTiles; y++)
            {
                Vector2I cell = new(x, y);
                if (!reserved.Contains(cell))
                {
                    layer.SetCell(cell, 0, groundCoords);
                }
            }
        }
    }

    private void ScanHandPlacedTiles(TileMapLayer layer, HashSet<Vector2I> reserved)
    {
        foreach (Vector2I pos in layer.GetUsedCells())
        {
            reserved.Add(pos);
        }
    }

    private (List<Vector2I> BorderWaypoints, List<Vector2I> InternalWaypoints) CollectWaypoints(
        TileMapLayer layer, ZoneSettings settings, HashSet<Vector2I> reserved, Vector2I zoneCoord)
    {
        var borderWaypoints = new List<Vector2I>();
        var internalWaypoints = new List<Vector2I>();
        int halfSize = ZoneTileSize / 2;

        var markerWaypoints = new List<Vector2I>();
        Node rootNode = GetParent();
        if (rootNode != null)
        {
            FindMarker2DWaypoints(rootNode, layer, markerWaypoints);
        }

        internalWaypoints.AddRange(markerWaypoints);
        internalWaypoints.Add(Vector2I.Zero); // Center waypoint

        if (ConnectToZoneBorders)
        {
            int baseSeed = WorldManager.Instance != null 
                ? WorldManager.Instance.WorldSeed 
                : SeedUtils.ParseSeed(SeedString, 1337);

            int borderThreshold = Mathf.RoundToInt(halfSize * 0.5f);
            int margin = 8; // Margin away from extreme corner vertices

            // North boundary
            int seedNorth = SeedUtils.DeriveEdgeSeed(baseSeed, zoneCoord, zoneCoord + Vector2I.Up);
            int offsetNorth = SeedUtils.GetSeedOffset(seedNorth, margin, halfSize);
            bool hasNorth = markerWaypoints.Exists(p => p.Y <= -borderThreshold);
            if (!hasNorth) borderWaypoints.Add(new Vector2I(offsetNorth, -halfSize));

            // South boundary
            int seedSouth = SeedUtils.DeriveEdgeSeed(baseSeed, zoneCoord, zoneCoord + Vector2I.Down);
            int offsetSouth = SeedUtils.GetSeedOffset(seedSouth, margin, halfSize);
            bool hasSouth = markerWaypoints.Exists(p => p.Y >= borderThreshold);
            if (!hasSouth) borderWaypoints.Add(new Vector2I(offsetSouth, halfSize));

            // West boundary
            int seedWest = SeedUtils.DeriveEdgeSeed(baseSeed, zoneCoord, zoneCoord + Vector2I.Left);
            int offsetWest = SeedUtils.GetSeedOffset(seedWest, margin, halfSize);
            bool hasWest = markerWaypoints.Exists(p => p.X <= -borderThreshold);
            if (!hasWest) borderWaypoints.Add(new Vector2I(-halfSize, offsetWest));

            // East boundary
            int seedEast = SeedUtils.DeriveEdgeSeed(baseSeed, zoneCoord, zoneCoord + Vector2I.Right);
            int offsetEast = SeedUtils.GetSeedOffset(seedEast, margin, halfSize);
            bool hasEast = markerWaypoints.Exists(p => p.X >= borderThreshold);
            if (!hasEast) borderWaypoints.Add(new Vector2I(halfSize, offsetEast));
        }

        if (settings?.PossibleLandmarks != null)
        {
            foreach (var landmark in settings.PossibleLandmarks)
            {
                if (landmark == null) continue;

                Vector2I landmarkCenter = new Vector2I(0, 0);
                int clearingRadiusTiles = Mathf.CeilToInt(landmark.ClearingRadius / 16f);

                for (int x = -clearingRadiusTiles; x <= clearingRadiusTiles; x++)
                {
                    for (int y = -clearingRadiusTiles; y <= clearingRadiusTiles; y++)
                    {
                        Vector2I p = landmarkCenter + new Vector2I(x, y);
                        if (p.DistanceTo(landmarkCenter) <= clearingRadiusTiles)
                        {
                            reserved.Add(p);
                        }
                    }
                }
            }
        }

        return (MergeCloseWaypoints(borderWaypoints, 4f), MergeCloseWaypoints(internalWaypoints, 4f));
    }

    private List<Vector2I> MergeCloseWaypoints(List<Vector2I> waypoints, float minDistance)
    {
        var merged = new List<Vector2I>();
        float minSq = minDistance * minDistance;

        foreach (var wp in waypoints)
        {
            bool isDuplicate = false;
            foreach (var existing in merged)
            {
                if (existing.DistanceSquaredTo(wp) <= minSq)
                {
                    isDuplicate = true;
                    break;
                }
            }
            if (!isDuplicate)
            {
                merged.Add(wp);
            }
        }
        return merged;
    }

    private void FindMarker2DWaypoints(Node parent, TileMapLayer layer, List<Vector2I> waypoints)
    {
        if (parent is Marker2D marker)
        {
            Vector2I tilePos = layer.LocalToMap(layer.ToLocal(marker.GlobalPosition));
            waypoints.Add(tilePos);
        }

        foreach (Node child in parent.GetChildren())
        {
            FindMarker2DWaypoints(child, layer, waypoints);
        }
    }

    private void GeneratePaths(
        TileMapLayer layer,
        ZoneSettings settings,
        List<Vector2I> borderWaypoints,
        List<Vector2I> internalWaypoints,
        HashSet<Vector2I> reserved,
        int zoneSeed)
    {
        var allWaypoints = new List<Vector2I>();
        allWaypoints.AddRange(borderWaypoints);
        allWaypoints.AddRange(internalWaypoints);

        var mergedWaypoints = MergeCloseWaypoints(allWaypoints, 3f);
        if (mergedWaypoints.Count < 2) return;

        var allPathTiles = new HashSet<Vector2I>();

        // 1. Primary Highway connections across opposite borders (North-South, West-East)
        List<(Vector2I Start, Vector2I End)> highwayEdges = BuildMainHighwayConnections(borderWaypoints, zoneSeed);

        // 2. Minimum Spanning Tree across ALL waypoints (ensures 100% connectivity)
        List<(Vector2I Start, Vector2I End)> mstEdges = BuildPathConnections(mergedWaypoints, maxProximityDist: 25f);

        // Merge highway edges and MST edges
        var allEdges = new List<(Vector2I Start, Vector2I End)>(highwayEdges);
        foreach (var edge in mstEdges)
        {
            if (!allEdges.Exists(e => (e.Start == edge.Start && e.End == edge.End) || (e.Start == edge.End && e.End == edge.Start)))
            {
                allEdges.Add(edge);
            }
        }

        foreach (var (start, end) in allEdges)
        {
            HashSet<Vector2I> pathTiles = PathGenerator.ConnectWaypoints(
                layer, start, end, settings,
                terrainSet: 0, terrain: 0,
                pathWidth: PathWidth, roughness: 0.2f, seed: zoneSeed
            );
            allPathTiles.UnionWith(pathTiles);
        }

        MergeClosePathTiles(allPathTiles);
        ApplyPathTilesToLayer(layer, settings, allPathTiles);
        ClearObstaclesFromPath(layer, allPathTiles);

        reserved.UnionWith(allPathTiles);
    }

    private List<(Vector2I Start, Vector2I End)> BuildMainHighwayConnections(List<Vector2I> borderWaypoints, int zoneSeed)
    {
        var edges = new List<(Vector2I Start, Vector2I End)>();
        if (borderWaypoints.Count < 2) return edges;

        int halfSize = ZoneTileSize / 2;

        Vector2I? north = borderWaypoints.Find(p => p.Y <= -halfSize + 2);
        Vector2I? south = borderWaypoints.Find(p => p.Y >= halfSize - 2);
        Vector2I? west  = borderWaypoints.Find(p => p.X <= -halfSize + 2);
        Vector2I? east  = borderWaypoints.Find(p => p.X >= halfSize - 2);

        var paired = new HashSet<Vector2I>();

        if (north.HasValue && south.HasValue)
        {
            edges.Add((north.Value, south.Value));
            paired.Add(north.Value);
            paired.Add(south.Value);
        }

        if (west.HasValue && east.HasValue)
        {
            edges.Add((west.Value, east.Value));
            paired.Add(west.Value);
            paired.Add(east.Value);
        }

        foreach (var wp in borderWaypoints)
        {
            if (paired.Contains(wp)) continue;

            var closest = borderWaypoints.Where(p => p != wp).OrderBy(p => p.DistanceSquaredTo(wp)).ToList();
            if (closest.Count > 0)
            {
                edges.Add((wp, closest[0]));
                paired.Add(wp);
            }
        }

        return edges;
    }

    private Vector2I FindClosestTile(HashSet<Vector2I> pathTiles, Vector2I target)
    {
        if (pathTiles.Count == 0) return target;

        Vector2I closest = target;
        float minSq = float.MaxValue;

        foreach (var tile in pathTiles)
        {
            float distSq = tile.DistanceSquaredTo(target);
            if (distSq < minSq)
            {
                minSq = distSq;
                closest = tile;
            }
        }

        return closest;
    }

    private void GenerateFoliage(TileMapLayer groundLayer, ZoneSettings settings, HashSet<Vector2I> reserved, int zoneSeed)
    {
        if (settings?.FoliageTypes == null || settings.FoliageTypes.Count == 0) return;
        if (settings.VegetationDensity <= 0f) return;

        TileMapLayer ySortLayer = AutoDetectYSortLayer(groundLayer);
        Node parentNode = groundLayer?.GetParent() ?? GetParent();
        if (ySortLayer == null && parentNode == null) return;

        FastNoiseLite noise = new FastNoiseLite();
        noise.Seed = zoneSeed;
        float noiseScale = settings.VegetationNoiseScale > 0 ? settings.VegetationNoiseScale : 0.05f;
        noise.Frequency = noiseScale;

        int halfTiles = ZoneTileSize / 2;

        for (int x = -halfTiles; x <= halfTiles; x++)
        {
            for (int y = -halfTiles; y <= halfTiles; y++)
            {
                Vector2I cell = new(x, y);
                if (reserved.Contains(cell)) continue;

                float noiseVal = (noise.GetNoise2D(x * 10f, y * 10f) + 1f) * 0.5f;

                if (noiseVal < settings.VegetationDensity)
                {
                    int foliageIdx = Mathf.Abs((x * 73856093 ^ y * 19349663 ^ zoneSeed) % settings.FoliageTypes.Count);
                    var entry = settings.FoliageTypes[foliageIdx];

                    if (entry != null)
                    {
                        if (ySortLayer != null && entry.TileCoords != new Vector2I(-1, -1))
                        {
                            ySortLayer.SetCell(cell, 0, entry.TileCoords);
                            reserved.Add(cell);
                        }
                        else if (entry.PrefabScene != null && parentNode != null)
                        {
                            var instance = entry.PrefabScene.Instantiate<Node2D>();
                            if (instance != null)
                            {
                                instance.Position = new Vector2(cell.X * 32, cell.Y * 32);
                                (ySortLayer ?? parentNode).AddChild(instance);
                                reserved.Add(cell);
                            }
                        }
                    }
                }
            }
        }
    }

    private TileMapLayer AutoDetectYSortLayer(TileMapLayer groundLayer)
    {
        Node parent = groundLayer?.GetParent() ?? GetParent();
        if (parent == null) return null;

        var found = parent.FindChild("Y-sort", recursive: true, owned: false) as TileMapLayer;
        if (found != null) return found;

        return parent.FindChild("ysort", recursive: true, owned: false) as TileMapLayer;
    }

    private List<(Vector2I Start, Vector2I End)> BuildPathConnections(List<Vector2I> waypoints, float maxProximityDist)
    {
        var edges = new List<(Vector2I Start, Vector2I End)>();
        if (waypoints.Count < 2) return edges;

        int n = waypoints.Count;
        var connected = new HashSet<int>();
        var edgeSet = new HashSet<(int, int)>();

        connected.Add(0);

        while (connected.Count < n)
        {
            int bestFrom = -1;
            int bestTo = -1;
            float minEdgeDist = float.MaxValue;

            foreach (int u in connected)
            {
                for (int v = 0; v < n; v++)
                {
                    if (connected.Contains(v)) continue;

                    float dist = waypoints[u].DistanceSquaredTo(waypoints[v]);
                    if (dist < minEdgeDist)
                    {
                        minEdgeDist = dist;
                        bestFrom = u;
                        bestTo = v;
                    }
                }
            }

            if (bestTo != -1)
            {
                connected.Add(bestTo);
                int u = Mathf.Min(bestFrom, bestTo);
                int v = Mathf.Max(bestFrom, bestTo);
                edgeSet.Add((u, v));
                edges.Add((waypoints[bestFrom], waypoints[bestTo]));
            }
            else break;
        }

        float maxSq = maxProximityDist * maxProximityDist;
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (edgeSet.Contains((i, j))) continue;

                if (waypoints[i].DistanceSquaredTo(waypoints[j]) <= maxSq)
                {
                    edgeSet.Add((i, j));
                    edges.Add((waypoints[i], waypoints[j]));
                }
            }
        }

        return edges;
    }

    private void MergeClosePathTiles(HashSet<Vector2I> pathTiles)
    {
        var gapsToFill = new List<Vector2I>();
        Vector2I[] dirs = { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };

        foreach (var cell in pathTiles)
        {
            foreach (var dir in dirs)
            {
                Vector2I target = cell + dir * 2;
                Vector2I gap = cell + dir;

                if (pathTiles.Contains(target) && !pathTiles.Contains(gap))
                {
                    gapsToFill.Add(gap);
                }
            }
        }

        foreach (var gap in gapsToFill)
        {
            pathTiles.Add(gap);
        }
    }

    private void ApplyPathTilesToLayer(TileMapLayer layer, ZoneSettings settings, HashSet<Vector2I> pathTiles)
    {
        if (layer == null || pathTiles.Count == 0) return;

        bool hasTerrains = UseTerrainAutoTiling && layer.TileSet != null && layer.TileSet.GetTerrainSetsCount() > 0;
        if (hasTerrains)
        {
            var godotArray = new Godot.Collections.Array<Vector2I>();
            foreach (var cell in pathTiles)
            {
                godotArray.Add(cell);
            }
            layer.SetCellsTerrainConnect(godotArray, 0, 0);
        }
        else
        {
            Vector2I pathCoords = settings?.PathTileCoords ?? new Vector2I(1, 0);
            foreach (var cell in pathTiles)
            {
                layer.SetCell(cell, 0, pathCoords);
            }
        }
    }

    private void ClearObstaclesFromPath(TileMapLayer groundLayer, HashSet<Vector2I> pathTiles)
    {
        if (groundLayer == null) return;
        Node tileMapNode = groundLayer.GetParent();
        if (tileMapNode == null) return;

        foreach (Node child in tileMapNode.GetChildren())
        {
            if (child is TileMapLayer otherLayer && otherLayer != groundLayer)
            {
                foreach (Vector2I cell in pathTiles)
                {
                    otherLayer.SetCell(cell, -1);
                }
            }
        }
    }
}
