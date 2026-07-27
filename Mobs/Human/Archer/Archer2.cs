using Godot;

public partial class Archer2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}