using Godot;

public partial class Plant : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}