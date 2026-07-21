using Godot;

public partial class SlimeBomb : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}