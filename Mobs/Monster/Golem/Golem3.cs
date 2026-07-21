using Godot;

public partial class Golem3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}