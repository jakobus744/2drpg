using Godot;

public partial class OldMan : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}