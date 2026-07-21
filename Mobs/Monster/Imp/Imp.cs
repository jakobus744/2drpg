using Godot;

public partial class Imp : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}