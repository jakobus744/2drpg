using Godot;

public partial class SlimeCrystal : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}