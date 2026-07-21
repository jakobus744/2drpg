using Godot;

public partial class TraderWeapon : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}