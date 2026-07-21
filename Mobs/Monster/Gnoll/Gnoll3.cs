using Godot;

public partial class Gnoll3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}