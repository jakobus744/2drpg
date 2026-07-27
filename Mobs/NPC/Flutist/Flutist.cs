using Godot;

public partial class Flutist : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}