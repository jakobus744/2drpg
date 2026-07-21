using Godot;

public partial class TraderMagic : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}