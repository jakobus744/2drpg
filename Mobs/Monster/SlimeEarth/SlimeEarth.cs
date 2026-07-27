using Godot;

public partial class SlimeEarth : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}