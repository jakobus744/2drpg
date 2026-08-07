using Godot;

namespace RPG2d.World.Generation.Data;

[GlobalClass]
public partial class MobSpawnEntry : Resource
{
    [Export] public string MobName { get; set; } = "Goblin";
    [Export] public PackedScene MobScene { get; set; }
    [Export(PropertyHint.Range, "0,1")] public float SpawnChance { get; set; } = 0.5f;
    [Export] public int MinGroupSize { get; set; } = 1;
    [Export] public int MaxGroupSize { get; set; } = 3;
}
