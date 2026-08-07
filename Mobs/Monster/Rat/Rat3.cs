using Godot;

public partial class Rat3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}