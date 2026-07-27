using Godot;

public partial class Lich2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}