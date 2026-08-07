using Godot;

public partial class Goblin2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}