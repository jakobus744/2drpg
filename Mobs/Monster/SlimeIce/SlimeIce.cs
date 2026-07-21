using Godot;

public partial class SlimeIce : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}