using Godot;

public partial class Mage1 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}