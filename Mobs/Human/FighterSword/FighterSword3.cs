using Godot;

public partial class FighterSword3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}