using Godot;

public partial class Rat2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}