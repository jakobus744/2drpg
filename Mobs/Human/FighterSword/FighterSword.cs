using Godot;

public partial class FighterSword : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}