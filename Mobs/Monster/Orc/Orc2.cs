using Godot;

public partial class Orc2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}