using Godot;

public partial class Skeleton : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}