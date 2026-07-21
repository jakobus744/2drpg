using Godot;

public partial class Demon3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}