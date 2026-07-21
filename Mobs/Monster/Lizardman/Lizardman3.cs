using Godot;

public partial class Lizardman3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}