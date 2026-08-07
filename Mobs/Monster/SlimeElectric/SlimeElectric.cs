using Godot;

public partial class SlimeElectric : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}