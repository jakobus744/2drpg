using Godot;

public partial class Mage4 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}