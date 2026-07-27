using Godot;

public partial class Skeleton3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}