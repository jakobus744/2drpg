using Godot;

public partial class Orc3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}