using Godot;

public partial class Lizardman2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}