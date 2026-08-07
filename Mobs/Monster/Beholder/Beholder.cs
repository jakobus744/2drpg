using Godot;

public partial class Beholder : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}