using Godot;

public partial class Golem : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}