using Godot;

public partial class Golem2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}