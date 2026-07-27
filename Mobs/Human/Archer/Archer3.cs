using Godot;

public partial class Archer3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}