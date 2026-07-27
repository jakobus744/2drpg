using Godot;

public partial class Zombie2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}