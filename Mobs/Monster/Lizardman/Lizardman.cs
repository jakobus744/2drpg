using Godot;

public partial class Lizardman : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}