using Godot;

public partial class Cobold : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}