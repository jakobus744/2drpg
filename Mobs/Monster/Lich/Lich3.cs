using Godot;

public partial class Lich3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}