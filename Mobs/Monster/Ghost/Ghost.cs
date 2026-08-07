using Godot;

public partial class Ghost : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}