using Godot;

public partial class Citizen1 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}