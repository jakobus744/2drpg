using Godot;

public partial class Citizen3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}