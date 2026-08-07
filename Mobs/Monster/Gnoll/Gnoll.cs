using Godot;

public partial class Gnoll : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}