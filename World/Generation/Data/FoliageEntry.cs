using Godot;

namespace RPG2d.World.Generation.Data;

[GlobalClass]
public partial class FoliageEntry : Resource
{
    [Export] public string Name { get; set; } = "Tree";
    [Export] public PackedScene PrefabScene { get; set; }
    [Export] public Vector2I TileCoords { get; set; } = new(-1, -1);

    [Export(PropertyHint.Range, "0,1")] public float SpawnWeight { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0,1")] public float MinNoiseThreshold { get; set; } = 0.2f;
    [Export] public float ClearingRadius { get; set; } = 16f;
}
