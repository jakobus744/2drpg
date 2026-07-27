using Godot;

public partial class SlimeWater : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}