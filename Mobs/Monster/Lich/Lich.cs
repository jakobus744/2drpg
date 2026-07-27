using Godot;

public partial class Lich : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}