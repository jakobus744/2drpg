using Godot;

public partial class TraderFruits : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}