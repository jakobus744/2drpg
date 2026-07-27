using Godot;

public partial class Gnoll2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}