using Godot;

public partial class Thief : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}