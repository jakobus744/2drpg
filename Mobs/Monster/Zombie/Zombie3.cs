using Godot;

public partial class Zombie3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}