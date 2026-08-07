using Godot;

public partial class Hare : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}