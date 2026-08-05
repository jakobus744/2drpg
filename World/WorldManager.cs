using System.Collections.Generic;
using Godot;

namespace RPG2d.World;

// Lädt Zonen rund um den lokalen Spieler, entlädt ferne.
// Terrain ist statisch/deterministisch → läuft auf jedem Peer lokal (kein Sync nötig).
public partial class WorldManager : Node2D
{
    [Export] public ZoneEntry[] Zones = System.Array.Empty<ZoneEntry>(); // optionaler Override
    [Export] public int ZoneSize = 3424;   // einheitliche Zonengröße in px (= echte Inhaltsgröße)
    [Export] public int LoadRadius = 1;     // Zonen im Umkreis von N laden
    [Export] public string SeedString { get; set; } = "LetsGO";

    public int WorldSeed => RPG2d.World.Generation.Logic.SeedUtils.ParseSeed(SeedString, 1337);

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

    private static readonly Dictionary<long, HashSet<Vector2I>> _peerLoadedZones = new();

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
        int size = Instance != null ? Instance.ZoneSize : 3424;
        return new Vector2I(
            Mathf.RoundToInt(worldPos.X / size),
            Mathf.RoundToInt(worldPos.Y / size));
    }

    public static Color GetZonePrimaryColor(Vector2I coord)
    {
        if (Instance != null && Instance._loaded.TryGetValue(coord, out var zoneNode))
        {
            var gen = zoneNode.FindChild("ZoneGenerator", recursive: true) as RPG2d.World.Generation.Logic.ZoneGenerator;
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
            (0, 0) => new Color(0.2f, 0.5f, 0.6f),   // Coast
            (1, 0) => new Color(0.35f, 0.45f, 0.25f), // Village
            (2, 0) => new Color(0.75f, 0.85f, 0.9f),  // Winter
            (0, 1) => new Color(0.15f, 0.1f, 0.3f),   // GlowingCave
            (1, 1) => new Color(0.18f, 0.48f, 0.2f),  // Forest
            (2, 1) => new Color(0.2f, 0.3f, 0.15f),   // Swamp
            (0, 2) => new Color(0.2f, 0.15f, 0.25f), // SkeletonPoison
            (1, 2) => new Color(0.75f, 0.65f, 0.4f),  // Desert
            (2, 2) => new Color(0.3f, 0.6f, 0.2f),   // Grassland
            (0, 3) => new Color(0.4f, 0.1f, 0.05f),  // LavaCave
            (1, 3) => new Color(0.15f, 0.05f, 0.2f),  // CursedLands
            (2, 3) => new Color(0.4f, 0.6f, 0.85f),  // SkyEndgame
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
                if (e?.Scene != null) _registry[e.Coord] = e.Scene;
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

        GD.Print($"[WorldManager] _Ready: {_registry.Count} Zonen registriert");
    }

    private Vector2I _lastCell = new(int.MinValue, int.MinValue);

    public override void _PhysicsProcess(double delta)
    {
        var player = Player.Player.LocalPlayer;
        if (player == null) return;

        // round (nicht floor): Zonen sind mittig zentriert, Zelle×ZoneSize = Zentrum.
        // Grenze liegt damit an der Biom-Kante, nicht in dessen Mitte.
        Vector2I current = new(
            Mathf.RoundToInt(player.Position.X / ZoneSize),
            Mathf.RoundToInt(player.Position.Y / ZoneSize));

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
            if (inst is Node2D n2d)
                n2d.Position = new Vector2(coord.X * ZoneSize, coord.Y * ZoneSize);
            else
                GD.PrintErr($"[WorldManager] Zone {coord} Root ist KEIN Node2D ({inst.GetType()}) → Position greift nicht!");
            AddChild(inst);
            _loaded[coord] = inst;
            GD.Print($"[WorldManager] geladen: Zelle {coord} @ ({coord.X * ZoneSize},{coord.Y * ZoneSize})");

            if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer())
            {
                RpcId(1, MethodName.ClientNotifyZoneLoaded, coord.X, coord.Y);
            }

            // Register zone with NavigationManager for pathfinding
            var nav = GetNodeOrNull<NavigationManager>("/root/NavigationManager");
            nav?.RegisterZone(coord, inst);

            MeasureZone(inst, coord);
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
        Vector2 topLeft = tm.GlobalPosition + new Vector2(r.Position.X * ts.X, r.Position.Y * ts.Y);
        Vector2 size = new(r.Size.X * ts.X, r.Size.Y * ts.Y);
        GD.Print($"[Measure] {coord}: Inhalt TopLeft={topLeft} Size={size} (erwartet @ {coord.X * ZoneSize},{coord.Y * ZoneSize}, Size {ZoneSize})");
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
