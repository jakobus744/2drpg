using Godot;

public partial class Fox : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}