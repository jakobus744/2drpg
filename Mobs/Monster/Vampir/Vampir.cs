using Godot;

public partial class Vampir : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}