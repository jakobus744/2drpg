using System.Collections.Generic;
using System.Linq;
using Godot;
using RPG2d.World.Generation.Data;

namespace RPG2d.World.Generation.Logic;

public partial class ZoneGenerator : Node
{
    [Export] public ZoneSettings Settings { get; set; }
    [Export] public TileMapLayer GroundLayer { get; set; }
    public int ZoneTileSize { get; set; } = 64;

    [ExportGroup("Path Options")]
    [Export] public bool ConnectToZoneBorders { get; set; } = true;
    [Export] public int PathWidth { get; set; } = 2;
    [Export] public bool UseTerrainAutoTiling { get; set; } = true;

    [ExportGroup("Background Options")]
    [Export] public bool EnableBackground { get; set; } = true;
    [Export] public bool UseGradientBackground { get; set; } = true;
    [Export] public bool FillGroundTiles { get; set; } = false;

    [ExportGroup("Debug")]
    [Export] public bool DebugLogs { get; set; } = false;

    private bool _hasGenerated;

    [ExportGroup("Seed Options")]
    [Export] public string SeedString { get; set; } = "1337";

    public override void _Ready()
    {
        GroundLayer = AutoDetectGroundLayer(GroundLayer);
        if (GroundLayer != null && Settings != null)
        {
            Vector2I coord = Vector2I.Zero;
            Node parent = GetParent();
            ZoneBackground bg = parent?.GetNodeOrNull<ZoneBackground>("ZoneBackground");
            if (bg != null && bg.ZoneCoord != Vector2I.Zero)
            {
                coord = bg.ZoneCoord;
            }
            else if (parent is Node2D parent2D)
            {
                coord = WorldManager.WorldToZoneCell(parent2D.Position);
            }

            // Nur wenn die Zone allein geoeffnet wird. Sonst startet der WorldManager
            // die Generierung mit der richtigen Koordinate - sonst liefe sie doppelt.
            if (WorldManager.Instance == null)
            {
                GenerateZone(GroundLayer, Settings, coord);
            }
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

    public Vector2I GetEffectiveZoneTileSize(Vector2I zoneCoord)
    {
        Vector2 zoneSizePx = WorldManager.GetZoneSize(zoneCoord);
        int tileSizePx = GroundLayer?.TileSet != null ? GroundLayer.TileSet.TileSize.X : 16;
        if (tileSizePx <= 0) tileSizePx = 16;
        return new Vector2I(
            Mathf.CeilToInt(zoneSizePx.X / tileSizePx),
            Mathf.CeilToInt(zoneSizePx.Y / tileSizePx));
    }

    public int GetEffectiveZoneTileSize()
    {
        return GetEffectiveZoneTileSize(Vector2I.Zero).X;
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

        if (_hasGenerated) return new HashSet<Vector2I>();
        _hasGenerated = true;

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
                zoneCoord = WorldManager.WorldToZoneCell(parent2D.Position);
            }
        }

        Vector2I tileDims = GetEffectiveZoneTileSize(zoneCoord);
        ZoneTileSize = tileDims.X;
        Rect2I zoneBounds = GetZoneTileBounds(tileDims);

        if (DebugLogs) GD.Print($"[ZoneGenerator] Generiere Zone {zoneCoord} -> PxGroesse: {WorldManager.GetZoneSize(zoneCoord)}, TileRaster: {tileDims}, Zentrum: {WorldManager.GetZonePosition(zoneCoord)}");

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
        var (borderWaypoints, internalWaypoints) = CollectWaypoints(GroundLayer, Settings, reservedCells, zoneCoord, zoneBounds);

        if (FillGroundTiles)
        {
            FillGround(GroundLayer, Settings, reservedCells, zoneBounds);
        }

        GeneratePaths(GroundLayer, Settings, borderWaypoints, internalWaypoints, reservedCells, zoneSeed, zoneBounds);
        GenerateFoliage(GroundLayer, Settings, reservedCells, zoneSeed, zoneBounds, zoneCoord);

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

        Vector2 zoneSize = WorldManager.GetZoneSize(zoneCoord);
        bg.Setup(Settings, zoneSize, zoneCoord, UseGradientBackground);
    }

    private static Rect2I GetZoneTileBounds(Vector2I tileDims)
    {
        return new Rect2I(-tileDims.X / 2, -tileDims.Y / 2, tileDims.X, tileDims.Y);
    }

    private void FillGround(TileMapLayer layer, ZoneSettings settings, HashSet<Vector2I> reserved, Rect2I bounds)
    {
        if (layer == null || settings == null) return;

        Vector2I groundCoords = settings.GroundTileCoords;
        int endX = bounds.Position.X + bounds.Size.X;
        int endY = bounds.Position.Y + bounds.Size.Y;

        for (int x = bounds.Position.X; x < endX; x++)
        {
            for (int y = bounds.Position.Y; y < endY; y++)
            {
                Vector2I cell = new(x, y);
                if (!reserved.Contains(cell))
                {
                    layer.SetCell(cell, settings.GroundSourceId, groundCoords);
                }
            }
        }
    }


    private (List<Vector2I> BorderWaypoints, List<Vector2I> InternalWaypoints) CollectWaypoints(
        TileMapLayer layer, ZoneSettings settings, HashSet<Vector2I> reserved, Vector2I zoneCoord, Rect2I bounds)
    {
        var borderWaypoints = new List<Vector2I>();
        var internalWaypoints = new List<Vector2I>();
        int minX = bounds.Position.X;
        int minY = bounds.Position.Y;
        int maxX = minX + bounds.Size.X - 1;
        int maxY = minY + bounds.Size.Y - 1;

        var markerWaypoints = new List<Vector2I>();
        Node rootNode = GetParent();
        if (rootNode != null)
        {
            FindMarker2DWaypoints(rootNode, layer, markerWaypoints);
        }

        internalWaypoints.AddRange(markerWaypoints.Where(bounds.HasPoint));
        internalWaypoints.Add(Vector2I.Zero);

        if (ConnectToZoneBorders)
        {
            int baseSeed = WorldManager.Instance != null 
                ? WorldManager.Instance.WorldSeed 
                : SeedUtils.ParseSeed(SeedString, 1337);

            int margin = 8;

            int seedNorth = SeedUtils.DeriveEdgeSeed(baseSeed, zoneCoord, zoneCoord + Vector2I.Up);
            int offsetNorth = SeedUtils.GetSeedOffset(seedNorth, margin, minX, maxX);
            borderWaypoints.Add(new Vector2I(offsetNorth, minY));

            int seedSouth = SeedUtils.DeriveEdgeSeed(baseSeed, zoneCoord, zoneCoord + Vector2I.Down);
            int offsetSouth = SeedUtils.GetSeedOffset(seedSouth, margin, minX, maxX);
            borderWaypoints.Add(new Vector2I(offsetSouth, maxY));

            int seedWest = SeedUtils.DeriveEdgeSeed(baseSeed, zoneCoord, zoneCoord + Vector2I.Left);
            int offsetWest = SeedUtils.GetSeedOffset(seedWest, margin, minY, maxY);
            borderWaypoints.Add(new Vector2I(minX, offsetWest));

            int seedEast = SeedUtils.DeriveEdgeSeed(baseSeed, zoneCoord, zoneCoord + Vector2I.Right);
            int offsetEast = SeedUtils.GetSeedOffset(seedEast, margin, minY, maxY);
            borderWaypoints.Add(new Vector2I(maxX, offsetEast));
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
        int zoneSeed,
        Rect2I zoneBounds)
    {
        var allPathTiles = new HashSet<Vector2I>();
        List<(Vector2I Start, Vector2I End, bool IsHighway)> edges = BuildMainHighwayConnections(borderWaypoints, zoneSeed, zoneBounds);

        // Connect internal markers to the nearest point on existing highway segments
        foreach (var internalWp in internalWaypoints)
        {
            Vector2I bestConnectTarget = Vector2I.Zero;
            float minDistSq = float.MaxValue;

            // Check distance to border waypoints
            foreach (var wp in borderWaypoints)
            {
                float distSq = internalWp.DistanceSquaredTo(wp);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestConnectTarget = wp;
                }
            }

            // Check distance to nearest point on existing highway edges
            foreach (var edge in edges)
            {
                Vector2I proj = GetClosestPointOnSegment(internalWp, edge.Start, edge.End);
                float distSq = internalWp.DistanceSquaredTo(proj);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestConnectTarget = proj;
                }
            }

            // Only add branch trail if internal waypoint is not already on/next to the highway (min 3 tiles away)
            if (minDistSq > 9f && minDistSq < float.MaxValue)
            {
                edges.Add((internalWp, bestConnectTarget, false));
            }
        }

        foreach (var edge in edges)
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

        BuildCrossroadPlazas(allPathTiles, edges, reserved, zoneBounds);
        MergeClosePathTiles(allPathTiles, zoneBounds);
        ApplyPathTilesToLayer(layer, settings, allPathTiles);
        ClearObstaclesFromPath(layer, allPathTiles);

        int pathBufferMargin = 1;
        foreach (var pathTile in allPathTiles)
        {
            for (int dx = -pathBufferMargin; dx <= pathBufferMargin; dx++)
            {
                for (int dy = -pathBufferMargin; dy <= pathBufferMargin; dy++)
                {
                    Vector2I bufferedCell = pathTile + new Vector2I(dx, dy);
                    if (zoneBounds.HasPoint(bufferedCell)) reserved.Add(bufferedCell);
                }
            }
        }
    }

    private void BuildCrossroadPlazas(
        HashSet<Vector2I> pathTiles,
        List<(Vector2I Start, Vector2I End, bool IsHighway)> edges,
        HashSet<Vector2I> reserved,
        Rect2I bounds)
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
                        if (!bounds.HasPoint(plazaCell)) continue;
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

    private List<(Vector2I Start, Vector2I End, bool IsHighway)> BuildMainHighwayConnections(
        List<Vector2I> borderWaypoints,
        int zoneSeed,
        Rect2I bounds)
    {
        var edges = new List<(Vector2I Start, Vector2I End, bool IsHighway)>();
        if (borderWaypoints.Count < 2) return edges;

        int minX = bounds.Position.X;
        int minY = bounds.Position.Y;
        int maxX = minX + bounds.Size.X - 1;
        int maxY = minY + bounds.Size.Y - 1;

        Vector2I? north = borderWaypoints.Find(p => p.Y <= minY + 2);
        Vector2I? south = borderWaypoints.Find(p => p.Y >= maxY - 2);
        Vector2I? west  = borderWaypoints.Find(p => p.X <= minX + 2);
        Vector2I? east  = borderWaypoints.Find(p => p.X >= maxX - 2);

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

    private Vector2I GetClosestPointOnSegment(Vector2I point, Vector2I segStart, Vector2I segEnd)
    {
        Vector2 p = new Vector2(point.X, point.Y);
        Vector2 a = new Vector2(segStart.X, segStart.Y);
        Vector2 b = new Vector2(segEnd.X, segEnd.Y);

        Vector2 ab = b - a;
        float lenSq = ab.LengthSquared();
        if (lenSq < 0.001f) return segStart;

        float t = Mathf.Clamp((p - a).Dot(ab) / lenSq, 0.0f, 1.0f);
        Vector2 proj = a + t * ab;
        return new Vector2I(Mathf.RoundToInt(proj.X), Mathf.RoundToInt(proj.Y));
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

    private void GenerateFoliage(TileMapLayer groundLayer, ZoneSettings settings, HashSet<Vector2I> reserved, int zoneSeed, Rect2I bounds, Vector2I zoneCoord)
    {
        if (settings == null || settings.VegetationDensity <= 0f)
        {
            return;
        }

        var candidateEntries = WorldManager.GetNeighboringFoliageEntries(zoneCoord);
        if (candidateEntries.Count == 0 && settings.FoliageTypes != null)
        {
            candidateEntries = settings.FoliageTypes.Where(e => e != null).ToList();
        }

        var validEntries = candidateEntries.Where(e => e != null).ToList();
        if (validEntries.Count == 0)
        {
            return;
        }

        TileMapLayer ySortLayer = AutoDetectYSortLayer(groundLayer);
        Node parentNode = groundLayer?.GetParent() ?? GetParent();
        if (ySortLayer == null && parentNode == null)
        {
            GD.PrintErr($"[ZoneGenerator] Weder ySortLayer noch parentNode gefunden!");
            return;
        }

        if (DebugLogs) GD.Print($"[ZoneGenerator LOG] === STARTE FOLIAGE FÜR ZONE {zoneCoord} ===");
        if (DebugLogs) GD.Print($"[ZoneGenerator LOG] Zone Settings: Temp={settings.Temperature}, Moist={settings.Moisture}, Density={settings.VegetationDensity}");
        if (DebugLogs) GD.Print($"[ZoneGenerator LOG] Candidates ({validEntries.Count}): " + string.Join(", ", validEntries.Select(e => $"{e.Name}[T:{e.IdealTemperature},M:{e.IdealMoisture},Tile:{e.TileCoords}]")));

        FastNoiseLite noise = new FastNoiseLite();
        noise.Seed = zoneSeed;
        float noiseScale = settings.VegetationNoiseScale > 0 ? settings.VegetationNoiseScale : 0.05f;
        noise.Frequency = noiseScale;

        int endX = bounds.Position.X + bounds.Size.X;
        int endY = bounds.Position.Y + bounds.Size.Y;
        int prefabCount = 0;
        int tileCount = 0;
        int maxPrefabsPerZone = 250;
        Vector2 zoneCenterWorldPos = WorldManager.GetZonePosition(zoneCoord);
        int logCount = 0;

        for (int x = bounds.Position.X; x < endX; x += 2)
        {
            for (int y = bounds.Position.Y; y < endY; y += 2)
            {
                Vector2I cell = new(x, y);
                if (reserved.Contains(cell)) continue;

                Vector2 localPos = groundLayer.MapToLocal(cell);
                Vector2 cellWorldPos = zoneCenterWorldPos + localPos;

                var (temp, moist) = WorldManager.GetClimateAtWorldPosition(cellWorldPos);

                float totalWeight = 0f;
                float maxSuitability = 0f;
                List<(FoliageEntry Entry, float Weight)> suitableEntries = new();

                foreach (var entry in validEntries)
                {
                    float suitability = entry.CalculateSuitability(temp, moist);
                    if (suitability < 0.05f) continue;

                    float weight = (entry.SpawnWeight > 0 ? entry.SpawnWeight : 0.5f) * suitability;
                    if (weight > 0.001f)
                    {
                        suitableEntries.Add((entry, weight));
                        totalWeight += weight;
                        if (suitability > maxSuitability) maxSuitability = suitability;
                    }
                }

                if (logCount < 5 && suitableEntries.Count > 0)
                {
                    logCount++;
                    if (DebugLogs) GD.Print($"[ZoneGenerator LOG] Zone {zoneCoord} Cell({x},{y}) Pos={cellWorldPos}: Climate=(T:{temp:F2}, M:{moist:F2})");
                    foreach (var entry in validEntries)
                    {
                        float s = entry.CalculateSuitability(temp, moist);
                        if (DebugLogs) GD.Print($"[ZoneGenerator LOG]   -> Entry '{entry.Name}': Suit={s:F3}, Weight={((entry.SpawnWeight > 0 ? entry.SpawnWeight : 0.5f) * s):F3}");
                    }
                }

                if (suitableEntries.Count == 0 || totalWeight <= 0f) continue;

                float noiseVal = (noise.GetNoise2D(x * 3f, y * 3f) + 1f) * 0.5f;

                if (noiseVal < settings.VegetationDensity * Mathf.Max(0.2f, maxSuitability))
                {
                    int pseudoRand = Mathf.Abs(x * 73856093 ^ y * 19349663 ^ zoneSeed);
                    if ((pseudoRand % 100) > 20) continue;

                    float targetWeight = (pseudoRand % 1000) / 1000f * totalWeight;
                    FoliageEntry selectedEntry = suitableEntries[0].Entry;
                    float accumulated = 0f;

                    foreach (var (entry, weight) in suitableEntries)
                    {
                        accumulated += weight;
                        if (targetWeight <= accumulated)
                        {
                            selectedEntry = entry;
                            break;
                        }
                    }

                    if (selectedEntry != null)
                    {
                        int radius = selectedEntry.GetClearingTileRadius(groundLayer);

                        if (selectedEntry.TileCoords != new Vector2I(-1, -1))
                        {
                            TileMapLayer target = ResolveTargetLayer(groundLayer, selectedEntry.TargetLayer);
                            if (target != null)
                            {
                                target.SetCell(cell, selectedEntry.SourceId, selectedEntry.TileCoords);
                                tileCount++;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else if (selectedEntry.PrefabScene != null && parentNode != null && prefabCount < maxPrefabsPerZone)
                        {
                            var instance = selectedEntry.PrefabScene.Instantiate<Node2D>();
                            if (instance != null)
                            {
                                Node targetContainer = ySortLayer ?? parentNode;
                                targetContainer.AddChild(instance);

                                instance.GlobalPosition = cellWorldPos;
                                prefabCount++;
                            }
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

        if (DebugLogs) GD.Print($"[ZoneGenerator] Foliage beendet für Zone {zoneCoord}: {prefabCount} Prefabs instanziiert, {tileCount} Tiles platziert.");
    }

    private TileMapLayer ResolveTargetLayer(TileMapLayer groundLayer, FoliageTargetLayer targetLayer)
    {
        Node root = GetParent() ?? groundLayer?.GetParent();
        if (root == null) return null;

        string[] names = targetLayer switch
        {
            FoliageTargetLayer.YSort => new[] { "ysort", "Y-sort", "Y-Sort" },
            FoliageTargetLayer.Detail => new[] { "detail", "Detail" },
            FoliageTargetLayer.Water => new[] { "water", "Water" },
            FoliageTargetLayer.Overlay => new[] { "overlay", "Overlay" },
            _ => System.Array.Empty<string>()
        };

        foreach (string name in names)
        {
            if (root.FindChild(name, recursive: true, owned: false) is TileMapLayer layer)
            {
                return layer;
            }
        }

        GD.PrintErr($"[ZoneGenerator] Ziel-Layer '{targetLayer}' fehlt in Zone '{root.Name}'. Tile wird nicht platziert.");
        return null;
    }

    private TileMapLayer AutoDetectYSortLayer(TileMapLayer groundLayer)
    {
        Node parent = GetParent() ?? groundLayer?.GetParent();
        if (parent == null) return null;

        foreach (string name in new[] { "ysort", "Y-sort", "Y-Sort" })
        {
            if (parent.FindChild(name, recursive: true, owned: false) is TileMapLayer layer)
            {
                return layer;
            }
        }

        return null;
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

    private void MergeClosePathTiles(HashSet<Vector2I> pathTiles, Rect2I bounds)
    {
        var gapsToFill = new List<Vector2I>();
        Vector2I[] dirs = { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };

        foreach (var cell in pathTiles)
        {
            foreach (var dir in dirs)
            {
                Vector2I target = cell + dir * 2;
                Vector2I gap = cell + dir;

                if (bounds.HasPoint(gap) && pathTiles.Contains(target) && !pathTiles.Contains(gap))
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
            int pathSource = settings?.PathSourceId ?? 0;
            foreach (var cell in pathTiles)
            {
                layer.SetCell(cell, pathSource, pathCoords);
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
