using Godot;

public partial class Skeleton2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}