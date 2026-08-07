using Godot;
using Godot.Collections;

namespace RPG2d.World.Generation.Data;

[GlobalClass]
public partial class ZoneSettings : Resource
{
    [ExportGroup("General")]
    [Export] public string ZoneName { get; set; } = "Forest";
    [Export] public Color PrimaryColor { get; set; } = new(0.1f, 0.4f, 0.1f);
    [Export] public Color SecondaryColor { get; set; } = new(0.05f, 0.2f, 0.05f);

    [ExportGroup("Climate")]
    [Export(PropertyHint.Range, "0,1")] public float Temperature { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0,1")] public float Moisture { get; set; } = 0.5f;

    [ExportGroup("Terrain Tiles")]
    [Export] public TileSet ZoneTileSet { get; set; }
    [Export] public Vector2I GroundTileCoords { get; set; } = new(0, 0);
    [Export] public Vector2I PathTileCoords { get; set; } = new(1, 0);
    [Export] public Vector2I DetailTileCoords { get; set; } = new(2, 0);

    [ExportGroup("Fauna & Bepflanzung")]
    [Export(PropertyHint.Range, "0,1")] public float VegetationDensity { get; set; } = 0.4f;
    [Export] public float VegetationNoiseScale { get; set; } = 0.05f;
    [Export] public Array<Data.FoliageEntry> FoliageTypes { get; set; } = new();

    [ExportGroup("Landmarks & Mobs")]
    [Export] public Array<Data.LandmarkSettings> PossibleLandmarks { get; set; } = new();
    [Export] public Data.MobSpawnSettings MobSettings { get; set; }
}
