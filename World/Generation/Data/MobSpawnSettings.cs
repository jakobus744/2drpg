using Godot;
using Godot.Collections;

namespace RPG2d.World.Generation.Data;

[GlobalClass]
public partial class MobSpawnSettings : Resource
{
    [ExportGroup("Spawn Table")]
    [Export] public Array<MobSpawnEntry> MobTable { get; set; } = new();

    [ExportGroup("Area Rules")]
    [Export] public float ActivationRange { get; set; } = 600f;
    [Export] public float DeactivationRange { get; set; } = 900f;
    [Export] public int MaxMobLimit { get; set; } = 8;
    [Export] public float SpawnRadius { get; set; } = 250f;
    [Export] public float RespawnCooldown { get; set; } = 30f;
}
