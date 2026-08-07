using Godot;

public partial class Slime : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}