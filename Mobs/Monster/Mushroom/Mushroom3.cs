using Godot;

public partial class Mushroom3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}