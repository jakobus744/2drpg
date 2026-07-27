using Godot;

public partial class Plant2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}