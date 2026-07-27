using Godot;

public partial class Beholder3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}