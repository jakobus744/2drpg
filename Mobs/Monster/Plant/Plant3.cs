using Godot;

public partial class Plant3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}