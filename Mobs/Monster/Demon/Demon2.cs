using Godot;

public partial class Demon2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}