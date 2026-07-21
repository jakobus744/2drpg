using Godot;

public partial class Imp3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}