using Godot;

public partial class FighterSword2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}