using Godot;

public partial class Goblin3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}