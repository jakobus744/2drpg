using Godot;

public partial class Beholder2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}