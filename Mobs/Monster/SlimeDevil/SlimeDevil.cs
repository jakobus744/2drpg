using Godot;

public partial class SlimeDevil : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}