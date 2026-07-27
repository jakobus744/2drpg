using Godot;

public partial class SlimeLava : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}