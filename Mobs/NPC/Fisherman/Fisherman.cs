using Godot;

public partial class Fisherman : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}