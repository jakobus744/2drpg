using Godot;

public partial class Rat : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}