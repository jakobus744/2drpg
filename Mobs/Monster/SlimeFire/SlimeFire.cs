using Godot;

public partial class SlimeFire : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}