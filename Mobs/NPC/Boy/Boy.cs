using Godot;

public partial class Boy : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}