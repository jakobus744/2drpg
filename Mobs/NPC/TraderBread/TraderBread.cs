using Godot;

public partial class TraderBread : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}