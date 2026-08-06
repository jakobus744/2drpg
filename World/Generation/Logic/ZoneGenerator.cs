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
        internalWaypoints.Add(Vector2I.Zero);

        if (ConnectToZoneBorders)
        {
            int baseSeed = WorldManager.Instance != null 
                ? WorldManager.Instance.WorldSeed 
                : SeedUtils.ParseSeed(SeedString, 1337);

            int borderThreshold = Mathf.RoundToInt(halfSize * 0.5f);
            int margin = 8;

            int seedNorth = SeedUtils.DeriveEdgeSeed(baseSeed, zoneCoord, zoneCoord + Vector2I.Up);
            int offsetNorth = SeedUtils.GetSeedOffset(seedNorth, margin, halfSize);
            bool hasNorth = markerWaypoints.Exists(p => p.Y <= -borderThreshold);
            if (!hasNorth) borderWaypoints.Add(new Vector2I(offsetNorth, -halfSize));

            int seedSouth = SeedUtils.DeriveEdgeSeed(baseSeed, zoneCoord, zoneCoord + Vector2I.Down);
            int offsetSouth = SeedUtils.GetSeedOffset(seedSouth, margin, halfSize);
            bool hasSouth = markerWaypoints.Exists(p => p.Y >= borderThreshold);
            if (!hasSouth) borderWaypoints.Add(new Vector2I(offsetSouth, halfSize));

            int seedWest = SeedUtils.DeriveEdgeSeed(baseSeed, zoneCoord, zoneCoord + Vector2I.Left);
            int offsetWest = SeedUtils.GetSeedOffset(seedWest, margin, halfSize);
            bool hasWest = markerWaypoints.Exists(p => p.X <= -borderThreshold);
            if (!hasWest) borderWaypoints.Add(new Vector2I(-halfSize, offsetWest));

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

        List<(Vector2I Start, Vector2I End, bool IsHighway)> highwayEdges = BuildMainHighwayConnections(borderWaypoints, zoneSeed);
        List<(Vector2I Start, Vector2I End, bool IsHighway)> mstEdges = BuildPathConnections(mergedWaypoints, maxProximityDist: 25f);

        var allEdges = new List<(Vector2I Start, Vector2I End, bool IsHighway)>(highwayEdges);
        foreach (var edge in mstEdges)
        {
            if (!allEdges.Exists(e => (e.Start == edge.Start && e.End == edge.End) || (e.Start == edge.End && e.End == edge.Start)))
            {
                allEdges.Add(edge);
            }
        }

        int halfZone = ZoneTileSize / 2;
        Rect2I zoneBounds = new Rect2I(-halfZone, -halfZone, ZoneTileSize, ZoneTileSize);

        foreach (var edge in allEdges)
        {
            HashSet<Vector2I> pathTiles = PathGenerator.ConnectWaypoints(
                layer, edge.Start, edge.End, settings,
                terrainSet: 0, terrain: 0,
                pathWidth: PathWidth, roughness: 0.2f, seed: zoneSeed,
                obstacles: reserved, existingPaths: allPathTiles, bounds: zoneBounds,
                isHighway: edge.IsHighway
            );
            allPathTiles.UnionWith(pathTiles);
        }

        BuildCrossroadPlazas(allPathTiles, allEdges, reserved);
        MergeClosePathTiles(allPathTiles);
        ApplyPathTilesToLayer(layer, settings, allPathTiles);
        ClearObstaclesFromPath(layer, allPathTiles);

        // Reserve path tiles plus a 1-2 tile safety margin buffer around roads so foliage stands back from paths
        int pathBufferMargin = 1;
        foreach (var pathTile in allPathTiles)
        {
            for (int dx = -pathBufferMargin; dx <= pathBufferMargin; dx++)
            {
                for (int dy = -pathBufferMargin; dy <= pathBufferMargin; dy++)
                {
                    reserved.Add(pathTile + new Vector2I(dx, dy));
                }
            }
        }
    }

    private void BuildCrossroadPlazas(HashSet<Vector2I> pathTiles, List<(Vector2I Start, Vector2I End, bool IsHighway)> edges, HashSet<Vector2I> reserved)
    {
        var nodeDegrees = new Dictionary<Vector2I, int>();
        foreach (var edge in edges)
        {
            nodeDegrees[edge.Start] = nodeDegrees.GetValueOrDefault(edge.Start, 0) + 1;
            nodeDegrees[edge.End] = nodeDegrees.GetValueOrDefault(edge.End, 0) + 1;
        }

        foreach (var (waypoint, degree) in nodeDegrees)
        {
            if (degree >= 3)
            {
                int plazaRadius = 2;
                for (int dx = -plazaRadius; dx <= plazaRadius; dx++)
                {
                    for (int dy = -plazaRadius; dy <= plazaRadius; dy++)
                    {
                        Vector2I plazaCell = waypoint + new Vector2I(dx, dy);
                        if (reserved != null && reserved.Contains(plazaCell)) continue;
                        if (dx * dx + dy * dy <= plazaRadius * plazaRadius + 1)
                        {
                            pathTiles.Add(plazaCell);
                        }
                    }
                }
            }
        }
    }

    private List<(Vector2I Start, Vector2I End, bool IsHighway)> BuildMainHighwayConnections(List<Vector2I> borderWaypoints, int zoneSeed)
    {
        var edges = new List<(Vector2I Start, Vector2I End, bool IsHighway)>();
        if (borderWaypoints.Count < 2) return edges;

        int halfSize = ZoneTileSize / 2;

        Vector2I? north = borderWaypoints.Find(p => p.Y <= -halfSize + 2);
        Vector2I? south = borderWaypoints.Find(p => p.Y >= halfSize - 2);
        Vector2I? west  = borderWaypoints.Find(p => p.X <= -halfSize + 2);
        Vector2I? east  = borderWaypoints.Find(p => p.X >= halfSize - 2);

        var paired = new HashSet<Vector2I>();

        if (north.HasValue && south.HasValue)
        {
            edges.Add((north.Value, south.Value, true));
            paired.Add(north.Value);
            paired.Add(south.Value);
        }

        if (west.HasValue && east.HasValue)
        {
            edges.Add((west.Value, east.Value, true));
            paired.Add(west.Value);
            paired.Add(east.Value);
        }

        foreach (var wp in borderWaypoints)
        {
            if (paired.Contains(wp)) continue;

            var closest = borderWaypoints.Where(p => p != wp).OrderBy(p => p.DistanceSquaredTo(wp)).ToList();
            if (closest.Count > 0)
            {
                edges.Add((wp, closest[0], true));
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

    private void ScanHandPlacedTiles(TileMapLayer layer, HashSet<Vector2I> reserved)
    {
        Node parent = layer?.GetParent();
        if (parent == null) return;

        foreach (Node child in parent.GetChildren())
        {
            if (child is TileMapLayer otherLayer && otherLayer != layer && otherLayer.Name != "Ground")
            {
                foreach (Vector2I pos in otherLayer.GetUsedCells())
                {
                    reserved.Add(pos);
                }
            }
        }
    }

    private void GenerateFoliage(TileMapLayer groundLayer, ZoneSettings settings, HashSet<Vector2I> reserved, int zoneSeed)
    {
        if (settings?.FoliageTypes == null || settings.FoliageTypes.Count == 0)
        {
            GD.Print($"[ZoneGenerator] Keine FoliageTypes in ZoneSettings definiert!");
            return;
        }
        if (settings.VegetationDensity <= 0f)
        {
            GD.Print($"[ZoneGenerator] VegetationDensity ist 0!");
            return;
        }

        var validEntries = settings.FoliageTypes.Where(e => e != null).ToList();
        if (validEntries.Count == 0)
        {
            GD.PrintErr($"[ZoneGenerator] FoliageTypes enthaelt nur null-Eintraege!");
            return;
        }

        TileMapLayer ySortLayer = AutoDetectYSortLayer(groundLayer);
        Node parentNode = groundLayer?.GetParent() ?? GetParent();
        if (ySortLayer == null && parentNode == null)
        {
            GD.PrintErr($"[ZoneGenerator] Weder ySortLayer noch parentNode gefunden!");
            return;
        }

        FastNoiseLite noise = new FastNoiseLite();
        noise.Seed = zoneSeed;
        float noiseScale = settings.VegetationNoiseScale > 0 ? settings.VegetationNoiseScale : 0.05f;
        noise.Frequency = noiseScale;

        int halfTiles = ZoneTileSize / 2;
        int prefabCount = 0;
        int tileCount = 0;
        int maxPrefabsPerZone = 250;

        Vector2 playerPos = RPG2d.Player.Player.LocalPlayer != null 
            ? RPG2d.Player.Player.LocalPlayer.GlobalPosition 
            : Vector2.Zero;

        for (int x = -halfTiles; x <= halfTiles; x += 2)
        {
            for (int y = -halfTiles; y <= halfTiles; y += 2)
            {
                Vector2I cell = new(x, y);
                if (reserved.Contains(cell)) continue;

                float noiseVal = (noise.GetNoise2D(x * 3f, y * 3f) + 1f) * 0.5f;

                if (noiseVal < settings.VegetationDensity)
                {
                    int pseudoRand = Mathf.Abs(x * 73856093 ^ y * 19349663 ^ zoneSeed);

                    // ~20% spawn chance per candidate 2x2 grid point for balanced 150-200 trees per zone
                    if ((pseudoRand % 100) > 20) continue;

                    float totalWeight = 0f;
                    foreach (var e in validEntries) totalWeight += (e.SpawnWeight > 0 ? e.SpawnWeight : 0.5f);

                    float targetWeight = (pseudoRand % 1000) / 1000f * totalWeight;
                    FoliageEntry selectedEntry = validEntries[0];
                    float accumulated = 0f;

                    foreach (var entry in validEntries)
                    {
                        float weight = entry.SpawnWeight > 0 ? entry.SpawnWeight : 0.5f;
                        accumulated += weight;
                        if (targetWeight <= accumulated)
                        {
                            selectedEntry = entry;
                            break;
                        }
                    }

                    if (selectedEntry != null)
                    {
                        int radius = selectedEntry.ClearingRadius > 0 
                            ? Mathf.Clamp(Mathf.CeilToInt(selectedEntry.ClearingRadius / 32f), 2, 4) 
                            : 2;

                        if (ySortLayer != null && selectedEntry.TileCoords != new Vector2I(-1, -1))
                        {
                            ySortLayer.SetCell(cell, 0, selectedEntry.TileCoords);
                            tileCount++;

                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                for (int dy = -radius; dy <= radius; dy++)
                                {
                                    reserved.Add(cell + new Vector2I(dx, dy));
                                }
                            }
                        }
                        else if (selectedEntry.PrefabScene != null && parentNode != null && prefabCount < maxPrefabsPerZone)
                        {
                            var instance = selectedEntry.PrefabScene.Instantiate<Node2D>();
                            if (instance != null)
                            {
                                Node targetContainer = ySortLayer ?? parentNode;
                                targetContainer.AddChild(instance);

                                Vector2 localPos = groundLayer.MapToLocal(cell);
                                instance.GlobalPosition = groundLayer.ToGlobal(localPos);
                                prefabCount++;

                                float distToPlayer = playerPos.DistanceTo(instance.GlobalPosition);
                                if (prefabCount <= 10)
                                {
                                    GD.Print($"[ZoneGenerator] Tree #{prefabCount} ({selectedEntry.Name}) -> Zelle {cell}, GlobalPos: {instance.GlobalPosition} (Distanz zum Spieler: {distToPlayer:F0}px)");
                                }

                                for (int dx = -radius; dx <= radius; dx++)
                                {
                                    for (int dy = -radius; dy <= radius; dy++)
                                    {
                                        reserved.Add(cell + new Vector2I(dx, dy));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        GD.Print($"[ZoneGenerator] Foliage beendet: {prefabCount} Prefabs instanziiert, {tileCount} Tiles platziert. (SpielerPos war {playerPos})");
    }

    private TileMapLayer AutoDetectYSortLayer(TileMapLayer groundLayer)
    {
        Node parent = groundLayer?.GetParent() ?? GetParent();
        if (parent == null) return null;

        var found = parent.FindChild("Y-sort", recursive: true, owned: false) as TileMapLayer;
        if (found != null) return found;

        return parent.FindChild("ysort", recursive: true, owned: false) as TileMapLayer;
    }

    private List<(Vector2I Start, Vector2I End, bool IsHighway)> BuildPathConnections(List<Vector2I> waypoints, float maxProximityDist)
    {
        var edges = new List<(Vector2I Start, Vector2I End, bool IsHighway)>();
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
                edges.Add((waypoints[bestFrom], waypoints[bestTo], false));
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
                    edges.Add((waypoints[i], waypoints[j], false));
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
