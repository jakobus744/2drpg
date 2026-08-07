using Godot;

public partial class Mushroom : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}