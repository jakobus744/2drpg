using Godot;

public partial class SlimeCloud : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}