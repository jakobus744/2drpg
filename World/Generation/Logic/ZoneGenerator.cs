using Godot;
using Godot.Collections;
using System.Collections.Generic;
using RPG2d.World.Generation.Data;

namespace RPG2d.World.Generation;

public partial class ZoneGenerator : Node
{
    [Export] public ZoneSettings Settings { get; set; }
    [Export] public TileMapLayer GroundLayer { get; set; }
    [Export] public int ZoneTileSize { get; set; } = 64;

    public override void _Ready()
    {
        if (GroundLayer != null && Settings != null)
        {
            GenerateZone(GroundLayer, Settings, Vector2I.Zero);
        }
    }

    public HashSet<Vector2I> GenerateZone(TileMapLayer groundLayer, ZoneSettings settings, Vector2I zoneCoord)
    {
        GroundLayer = groundLayer ?? GroundLayer;
        Settings = settings ?? Settings;

        if (GroundLayer == null || Settings == null)
        {
            GD.PrintErr("[ZoneGenerator] GroundLayer oder ZoneSettings fehlen!");
            return new HashSet<Vector2I>();
        }

        HashSet<Vector2I> reservedCells = new HashSet<Vector2I>();

        ScanHandPlacedTiles(GroundLayer, reservedCells);
        List<Vector2I> allWaypoints = CollectWaypoints(GroundLayer, Settings, reservedCells, zoneCoord);
        GeneratePaths(GroundLayer, Settings, allWaypoints, reservedCells);

        return reservedCells;
    }

    private void ScanHandPlacedTiles(TileMapLayer layer, HashSet<Vector2I> reserved)
    {
        foreach (Vector2I pos in layer.GetUsedCells())
        {
            reserved.Add(pos);
        }
    }

    private List<Vector2I> CollectWaypoints(TileMapLayer layer, ZoneSettings settings, HashSet<Vector2I> reserved, Vector2I zoneCoord)
    {
        var waypoints = new List<Vector2I>();

        Node rootNode = GetTree()?.CurrentScene ?? GetParent();
        if (rootNode != null)
        {
            FindMarker2DWaypoints(rootNode, layer, waypoints);
        }

        int halfSize = ZoneTileSize / 2;
        if (settings.PossibleLandmarks != null)
        {
            foreach (var landmark in settings.PossibleLandmarks)
            {
                if (landmark == null) continue;

                Vector2I landmarkCenter = new Vector2I(halfSize, halfSize);
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

        if (waypoints.Count == 0)
        {
            waypoints.Add(new Vector2I(0, halfSize));
            waypoints.Add(new Vector2I(ZoneTileSize - 1, halfSize));
        }

        return waypoints;
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

    private void GeneratePaths(TileMapLayer layer, ZoneSettings settings, List<Vector2I> waypoints, HashSet<Vector2I> reserved)
    {
        if (waypoints.Count < 2) return;

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            Vector2I start = waypoints[i];
            Vector2I end = waypoints[i + 1];

            HashSet<Vector2I> pathTiles = PathGenerator.ConnectWaypoints(
                layer,
                start,
                end,
                settings,
                terrainSet: 0,
                terrain: 0,
                pathWidth: 2,
                roughness: 0.2f
            );

            reserved.UnionWith(pathTiles);
        }
    }
}
