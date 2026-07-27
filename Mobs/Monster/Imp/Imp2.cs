using Godot;

public partial class Imp2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}