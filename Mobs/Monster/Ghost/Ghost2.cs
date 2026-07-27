using Godot;

public partial class Ghost2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}