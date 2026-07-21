using Godot;

public partial class Zombie : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}