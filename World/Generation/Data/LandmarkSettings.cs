using Godot;
using Godot.Collections;

namespace RPG2d.World.Generation.Data;

[GlobalClass]
public partial class LandmarkSettings : Resource
{
    [ExportGroup("General")]
    [Export] public string LandmarkName { get; set; } = "Ancient Shrine";
    [Export] public PackedScene LandmarkScene { get; set; }

    [ExportGroup("Dimensions & Blending")]
    [Export] public float Radius { get; set; } = 128f;
    [Export] public float TransitionRadius { get; set; } = 64f;
    [Export] public float ClearingRadius { get; set; } = 160f;

    [ExportGroup("Path Generation")]
    [Export] public Array<Vector2I> Waypoints { get; set; } = new() { new Vector2I(0, 0) };

    [ExportGroup("Mini-Zone Overrides")]
    [Export] public bool OverrideBackgroundColor { get; set; } = false;
    [Export] public Color TargetBackgroundColor { get; set; } = Colors.Purple;
    [Export(PropertyHint.Range, "0,1")] public float OverrideVegetationDensity { get; set; } = 0.1f;
}
