using Godot;

public partial class Citizen2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}