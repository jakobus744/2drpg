using Godot;

public partial class Herbalist : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}