using Godot;

public partial class SlimeEvil : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}