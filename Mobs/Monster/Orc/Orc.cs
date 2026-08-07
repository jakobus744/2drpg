using Godot;

public partial class Orc : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}