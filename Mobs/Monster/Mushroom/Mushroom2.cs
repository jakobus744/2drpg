using Godot;

public partial class Mushroom2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}