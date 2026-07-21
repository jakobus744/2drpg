using Godot;

public partial class Archer : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}