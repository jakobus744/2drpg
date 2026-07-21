using Godot;

public partial class Ghost3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}