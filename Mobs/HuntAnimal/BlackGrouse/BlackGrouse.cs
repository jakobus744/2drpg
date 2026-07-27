using Godot;

public partial class BlackGrouse : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}