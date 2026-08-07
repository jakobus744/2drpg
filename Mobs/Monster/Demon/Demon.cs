using Godot;

public partial class Demon : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}