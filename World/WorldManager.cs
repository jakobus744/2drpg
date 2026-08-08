using System.Collections.Generic;
using Godot;
using RPG2d.World.Generation.Data;
using RPG2d.World.Generation.Logic;

namespace RPG2d.World;

// Lädt Zonen rund um den lokalen Spieler, entlädt ferne.
// Terrain ist statisch/deterministisch → läuft auf jedem Peer lokal (kein Sync nötig).
public partial class WorldManager : Node2D
{
    private const float DefaultFallbackZoneSize = 3424f;

    [Export] public ZoneEntry[] Zones = []; // optionaler Override
    [Export] public int LoadRadius = 1; // Zonen im Umkreis von N laden
    [Export] public string SeedString { get; set; } = "LetsGO";

    public int WorldSeed => SeedUtils.ParseSeed(SeedString, 1337);

    // Standard-Layout (3×4 Raster). Wird genutzt wenn Zones leer ist.
    private static readonly (Vector2I Coord, string Path)[] DefaultLayout =
    {
        (new(0, 0), "res://World/Zones/Coast/Coast.tscn"),
        (new(1, 0), "res://World/Zones/Village/Village.tscn"),
        (new(2, 0), "res://World/Zones/Winter/Winter.tscn"),
        (new(0, 1), "res://World/Zones/GlowingCave/GlowingCave.tscn"),
        (new(1, 1), "res://World/Zones/Forest/Forest.tscn"),
        (new(2, 1), "res://World/Zones/Swamp/Swamp.tscn"),
        (new(0, 2), "res://World/Zones/SkeletonPoison/SkeletonPoison.tscn"),
        (new(1, 2), "res://World/Zones/Desert/Desert.tscn"),
        (new(2, 2), "res://World/Zones/Grassland/Grassland.tscn"),
        (new(0, 3), "res://World/Zones/LavaCave/LavaCave.tscn"),
        (new(1, 3), "res://World/Zones/CursedLands/Curese.tscn"),
        (new(2, 3), "res://World/Zones/SkyEndgame/SkyEndgame.tscn"),
    };

    private readonly Dictionary<Vector2I, PackedScene> _registry = new();
    private readonly Dictionary<Vector2I, Node> _loaded = new();

    private static readonly Dictionary<Vector2I, Vector2> _zoneSizes = new();
    private static readonly Dictionary<Vector2I, Vector2> _zonePositions = new();
    private static readonly Dictionary<Vector2I, ZoneSettings> _zoneSettings = new();

    private static readonly Dictionary<long, HashSet<Vector2I>> _peerLoadedZones = new();

    public static void RegisterZoneSettings(Vector2I coord, ZoneSettings settings)
    {
        if (settings != null)
            _zoneSettings[coord] = settings;
    }

    public static ZoneSettings GetZoneSettings(Vector2I coord)
    {
        if (_zoneSettings.TryGetValue(coord, out var settings) && settings != null)
            return settings;
        return null;
    }

    public Node GetLoadedZoneNode(Vector2I coord)
    {
        if (_loaded.TryGetValue(coord, out var node))
            return node;
        return null;
    }

    public static (float Temperature, float Moisture) GetClimateAtWorldPosition(Vector2 worldPos)
    {
        Vector2I centerCell = WorldToZoneCell(worldPos);
        float totalWeight = 0f;
        float weightedTemp = 0f;
        float weightedMoist = 0f;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                Vector2I coord = centerCell + new Vector2I(dx, dy);
                ZoneSettings settings = GetZoneSettings(coord);
                float temp = settings != null ? settings.Temperature : 0.5f;
                float moist = settings != null ? settings.Moisture : 0.5f;

                Vector2 centerPos = GetZonePosition(coord);
                float distSq = worldPos.DistanceSquaredTo(centerPos);
                float weight = 1f / (distSq + 10000f);

                totalWeight += weight;
                weightedTemp += temp * weight;
                weightedMoist += moist * weight;
            }
        }

        if (totalWeight <= 0f) return (0.5f, 0.5f);
        return (weightedTemp / totalWeight, weightedMoist / totalWeight);
    }

    public static List<FoliageEntry> GetNeighboringFoliageEntries(Vector2I centerCoord)
    {
        var entries = new List<FoliageEntry>();
        var unique = new HashSet<FoliageEntry>();

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                Vector2I coord = centerCoord + new Vector2I(dx, dy);
                ZoneSettings settings = GetZoneSettings(coord);
                if (settings?.FoliageTypes == null) continue;

                foreach (var foliage in settings.FoliageTypes)
                {
                    if (foliage != null && unique.Add(foliage))
                    {
                        entries.Add(foliage);
                    }
                }
            }
        }

        return entries;
    }


    public static Vector2 GetZoneSize(Vector2I coord)
    {
        if (_zoneSizes.TryGetValue(coord, out var size))
            return size;
        return new Vector2(DefaultFallbackZoneSize, DefaultFallbackZoneSize);
    }

    public static Vector2 GetZonePosition(Vector2I coord)
    {
        if (_zonePositions.TryGetValue(coord, out var pos))
            return pos;
        Vector2 size = GetZoneSize(coord);
        return new Vector2(coord.X * size.X, coord.Y * size.Y);
    }

    public static Rect2 GetZoneBounds(Vector2I coord)
    {
        Vector2 center = GetZonePosition(coord);
        Vector2 size = GetZoneSize(coord);
        return new Rect2(center - size / 2f, size);
    }

    public static bool IsZoneLoadedForPeer(long peerId, Vector2I coord)
    {
        if (peerId <= 1) return true;
        return _peerLoadedZones.TryGetValue(peerId, out var set) && set.Contains(coord);
    }

    public static void ClearPeerZones(long peerId)
    {
        _peerLoadedZones.Remove(peerId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientNotifyZoneLoaded(int x, int y)
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        if (!_peerLoadedZones.TryGetValue(senderId, out var set))
        {
            set = new HashSet<Vector2I>();
            _peerLoadedZones[senderId] = set;
        }

        set.Add(new Vector2I(x, y));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientNotifyZoneUnloaded(int x, int y)
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        if (_peerLoadedZones.TryGetValue(senderId, out var set))
        {
            set.Remove(new Vector2I(x, y));
        }
    }

    public static WorldManager Instance { get; private set; }

    public static Vector2I WorldToZoneCell(Vector2 worldPos)
    {
        if (_zonePositions.Count > 0)
        {
            foreach (var (coord, pos) in _zonePositions)
            {
                Vector2 size = GetZoneSize(coord);
                Rect2 bounds = new Rect2(pos - size / 2f, size);
                if (bounds.HasPoint(worldPos))
                    return coord;
            }

            Vector2I closestCoord = Vector2I.Zero;
            float minSq = float.MaxValue;
            foreach (var (coord, pos) in _zonePositions)
            {
                float distSq = pos.DistanceSquaredTo(worldPos);
                if (distSq < minSq)
                {
                    minSq = distSq;
                    closestCoord = coord;
                }
            }

            return closestCoord;
        }

        int defaultSize = (int)DefaultFallbackZoneSize;
        return new Vector2I(
            Mathf.RoundToInt(worldPos.X / defaultSize),
            Mathf.RoundToInt(worldPos.Y / defaultSize));
    }

    public static Color GetZonePrimaryColor(Vector2I coord)
    {
        if (Instance != null && Instance._loaded.TryGetValue(coord, out var zoneNode))
        {
            var gen =
                zoneNode.FindChild("ZoneGenerator", recursive: true) as ZoneGenerator;
            if (gen?.Settings != null && gen.Settings.PrimaryColor.A > 0)
            {
                return gen.Settings.PrimaryColor;
            }
        }

        return GetDefaultBiomeColor(coord);
    }

    public static Color GetDefaultBiomeColor(Vector2I coord)
    {
        return (coord.X, coord.Y) switch
        {
            (0, 0) => new Color(0.2f, 0.5f, 0.6f), // Coast
            (1, 0) => new Color(0.35f, 0.45f, 0.25f), // Village
            (2, 0) => new Color(0.75f, 0.85f, 0.9f), // Winter
            (0, 1) => new Color(0.15f, 0.1f, 0.3f), // GlowingCave
            (1, 1) => new Color(0.18f, 0.48f, 0.2f), // Forest
            (2, 1) => new Color(0.2f, 0.3f, 0.15f), // Swamp
            (0, 2) => new Color(0.2f, 0.15f, 0.25f), // SkeletonPoison
            (1, 2) => new Color(0.75f, 0.65f, 0.4f), // Desert
            (2, 2) => new Color(0.3f, 0.6f, 0.2f), // Grassland
            (0, 3) => new Color(0.4f, 0.1f, 0.05f), // LavaCave
            (1, 3) => new Color(0.15f, 0.05f, 0.2f), // CursedLands
            (2, 3) => new Color(0.4f, 0.6f, 0.85f), // SkyEndgame
            _ => new Color(0.2f, 0.4f, 0.2f)
        };
    }

    public override void _Ready()
    {
        Instance = this;
        // Registry aus Export-Array ODER (wenn leer) aus dem Standard-Layout bauen
        if (Zones.Length > 0)
        {
            foreach (var e in Zones)
                if (e?.Scene != null)
                    _registry[e.Coord] = e.Scene;
        }
        else
        {
            foreach (var (coord, path) in DefaultLayout)
            {
                var scene = GD.Load<PackedScene>(path);
                if (scene != null) _registry[coord] = scene;
                else GD.PrintErr($"[WorldManager] Zone nicht gefunden: {path}");
            }
        }

        // Measure zone bounds upfront and compute dynamic layout grid
        foreach (var (coord, scene) in _registry)
        {
            _zoneSizes[coord] = MeasureSceneBounds(scene, coord);
        }

        CalculateLayoutPositions();

        GD.Print($"[WorldManager] _Ready: {_registry.Count} Zonen registriert:");
        foreach (var (coord, pos) in _zonePositions)
        {
            Vector2 size = GetZoneSize(coord);
            Rect2 bounds = GetZoneBounds(coord);
            GD.Print($"[WorldManager]   Zelle {coord} -> Pos: {pos}, Groesse: {size}, Bounds: {bounds}");
        }
    }

    private void CalculateLayoutPositions()
    {
        var colWidths = new Dictionary<int, float>();
        var rowHeights = new Dictionary<int, float>();

        foreach (var (coord, size) in _zoneSizes)
        {
            if (!colWidths.TryGetValue(coord.X, out float w) || size.X > w)
                colWidths[coord.X] = size.X;
            if (!rowHeights.TryGetValue(coord.Y, out float h) || size.Y > h)
                rowHeights[coord.Y] = size.Y;
        }

        foreach (var (coord, size) in _zoneSizes)
        {
            float posX = ComputeCenterOffset(coord.X, colWidths);
            float posY = ComputeCenterOffset(coord.Y, rowHeights);
            _zonePositions[coord] = new Vector2(posX, posY);
        }
    }

    private float ComputeCenterOffset(int index, Dictionary<int, float> sizes)
    {
        if (index == 0) return 0f;

        float currentHalf = sizes.GetValueOrDefault(index, DefaultFallbackZoneSize) / 2f;
        float center0Half = sizes.GetValueOrDefault(0, DefaultFallbackZoneSize) / 2f;

        if (index > 0)
        {
            float sum = center0Half;
            for (int i = 1; i < index; i++)
            {
                sum += sizes.GetValueOrDefault(i, DefaultFallbackZoneSize);
            }

            sum += currentHalf;
            return sum;
        }
        else
        {
            float sum = center0Half;
            for (int i = -1; i > index; i--)
            {
                sum += sizes.GetValueOrDefault(i, DefaultFallbackZoneSize);
            }

            sum += currentHalf;
            return -sum;
        }
    }

    private Vector2 MeasureSceneBounds(PackedScene scene, Vector2I coord)
    {
        if (scene == null) return new Vector2(DefaultFallbackZoneSize, DefaultFallbackZoneSize);

        var tempNode = scene.Instantiate();
        if (tempNode == null) return new Vector2(DefaultFallbackZoneSize, DefaultFallbackZoneSize);

        Vector2 detectedSize = Vector2.Zero;

        if (tempNode.FindChild("ZoneBackground", recursive: true) is ZoneBackground { EffectiveZoneSize: { X: > 0, Y: > 0 } } bg)
        {
            detectedSize = bg.EffectiveZoneSize;
        }
        else
        {
            if (tempNode.FindChild("ZoneGenerator", recursive: true) is ZoneGenerator gen)
            {
                if (gen.Settings != null) RegisterZoneSettings(coord, gen.Settings);
                if (gen.ZoneTileSize > 0)
                {
                    int tileSize = 16;
                    if (gen.GroundLayer?.TileSet != null)
                        tileSize = gen.GroundLayer.TileSet.TileSize.X;
                    detectedSize = new Vector2(gen.ZoneTileSize * tileSize, gen.ZoneTileSize * tileSize);
                }
            }
        }

        if (detectedSize == Vector2.Zero)
        {
            var layers = new List<TileMapLayer>();
            CollectLayers(tempNode, layers);

            bool hasBounds = false;
            Rect2I tileBounds = default;
            int maxTileSize = 16;

            foreach (var layer in layers)
            {
                var used = layer.GetUsedRect();
                if (used.Size.X <= 0 || used.Size.Y <= 0) continue;

                tileBounds = hasBounds ? UnionRects(tileBounds, used) : used;
                hasBounds = true;
                if (layer.TileSet != null)
                    maxTileSize = Mathf.Max(maxTileSize, layer.TileSet.TileSize.X);
            }

            if (hasBounds && tileBounds.Size.X * maxTileSize >= 512 && tileBounds.Size.Y * maxTileSize >= 512)
            {
                detectedSize = new Vector2(tileBounds.Size.X * maxTileSize, tileBounds.Size.Y * maxTileSize);
            }
        }

        tempNode.Free();

        if (detectedSize.X > 0 && detectedSize.Y > 0)
        {
            GD.Print($"[WorldManager] Zone {coord}: Groesse ermittelt -> {detectedSize}");
            return detectedSize;
        }

        GD.Print(
            $"[WorldManager] Zone {coord}: Groesse Fallback -> {new Vector2(DefaultFallbackZoneSize, DefaultFallbackZoneSize)}");
        return new Vector2(DefaultFallbackZoneSize, DefaultFallbackZoneSize);
    }

    private static void CollectLayers(Node node, List<TileMapLayer> layers)
    {
        if (node is TileMapLayer layer)
            layers.Add(layer);
        foreach (Node child in node.GetChildren())
            CollectLayers(child, layers);
    }

    private static Rect2I UnionRects(Rect2I a, Rect2I b)
    {
        int left = Mathf.Min(a.Position.X, b.Position.X);
        int top = Mathf.Min(a.Position.Y, b.Position.Y);
        int right = Mathf.Max(a.Position.X + a.Size.X, b.Position.X + b.Size.X);
        int bottom = Mathf.Max(a.Position.Y + a.Size.Y, b.Position.Y + b.Size.Y);
        return new Rect2I(left, top, right - left, bottom - top);
    }

    private Vector2I _lastCell = new(int.MinValue, int.MinValue);

    public override void _PhysicsProcess(double delta)
    {
        var player = Player.Player.LocalPlayer;
        if (player == null) return;

        Vector2I current = WorldToZoneCell(player.Position);

        if (current != _lastCell)
        {
            GD.Print($"[WorldManager] Spieler@{player.Position} → Zelle {current}, geladen: {_loaded.Count}");
            _lastCell = current;
        }

        LoadNearby(current);
        UnloadFar(current);
    }

    private void LoadNearby(Vector2I current)
    {
        foreach (var (coord, scene) in _registry)
        {
            if (_loaded.ContainsKey(coord)) continue;

            var d = coord - current;
            if (Mathf.Abs(d.X) > LoadRadius || Mathf.Abs(d.Y) > LoadRadius) continue;

            var inst = scene.Instantiate();
            inst.Name = $"Zone_{coord.X}_{coord.Y}";
            var zonePos = GetZonePosition(coord);
            if (inst is Node2D n2D)
                n2D.Position = zonePos;

            if (inst.FindChild("ZoneBackground", recursive: true) is ZoneBackground bg) bg.ZoneCoord = coord;

            AddChild(inst);
            _loaded[coord] = inst;
            GD.Print($"[WorldManager] geladen: Zelle {coord} @ {zonePos}");

            if (inst.FindChild("ZoneGenerator", recursive: true) is ZoneGenerator
                {
                    GroundLayer: not null, Settings: not null
                } gen)
            {
                RegisterZoneSettings(coord, gen.Settings);
                gen.GenerateZone(gen.GroundLayer, gen.Settings, coord);
            }

            if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer())
            {
                RpcId(1, MethodName.ClientNotifyZoneLoaded, coord.X, coord.Y);
            }

            // Register zone with NavigationManager for pathfinding
            var nav = GetNodeOrNull<NavigationManager>("/root/NavigationManager");
            nav?.RegisterZone(coord, inst);

            MeasureZone(inst, coord);
            RefreshZoneBackgroundsAround(coord);
        }
    }

    private void RefreshZoneBackgroundsAround(Vector2I centerCoord)
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                var neighbor = centerCoord + new Vector2I(dx, dy);
                if (!_loaded.TryGetValue(neighbor, out var zoneNode)) continue;
                if (zoneNode.FindChild("ZoneBackground", recursive: true) is not ZoneBackground bg) continue;
                bg.RebuildTexture();
                bg.QueueRedraw();
            }
        }
    }

    // DEBUG: misst wo der Tile-Inhalt einer Zone tatsächlich liegt (Welt-Koordinaten)
    private void MeasureZone(Node inst, Vector2I coord)
    {
        var tm = FindTileMapLayer(inst);
        if (tm == null)
        {
            GD.Print($"[Measure] {coord}: kein TileMapLayer gefunden (evtl. altes TileMap?)");
            return;
        }

        var r = tm.GetUsedRect();
        var ts = tm.TileSet.TileSize;
        var topLeft = tm.GlobalPosition + new Vector2(r.Position.X * ts.X, r.Position.Y * ts.Y);
        Vector2 size = new(r.Size.X * ts.X, r.Size.Y * ts.Y);
        var expectedPos = GetZonePosition(coord);
        var expectedSize = GetZoneSize(coord);
        GD.Print(
            $"[Measure] {coord}: Inhalt TopLeft={topLeft} Size={size} (erwartet @ {expectedPos}, Size {expectedSize})");
    }

    private TileMapLayer FindTileMapLayer(Node n)
    {
        if (n is TileMapLayer t) return t;
        foreach (var c in n.GetChildren())
        {
            var f = FindTileMapLayer(c);
            if (f != null) return f;
        }

        return null;
    }

    private void UnloadFar(Vector2I current)
    {
        var remove = new List<Vector2I>();
        foreach (var (coord, node) in _loaded)
        {
            var d = coord - current;
            if (Mathf.Abs(d.X) > LoadRadius || Mathf.Abs(d.Y) > LoadRadius)
            {
                // Unregister zone grid before freeing
                var nav = GetNodeOrNull<NavigationManager>("/root/NavigationManager");
                nav?.UnregisterZone(coord);

                if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer())
                {
                    RpcId(1, MethodName.ClientNotifyZoneUnloaded, coord.X, coord.Y);
                }

                node.QueueFree();
                remove.Add(coord);
                GD.Print($"[WorldManager] entladen: Zelle {coord}");
            }
        }

        foreach (var c in remove) _loaded.Remove(c);
    }
}