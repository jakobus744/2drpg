using Godot;

public partial class Deer : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}