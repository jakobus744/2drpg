using Godot;

public partial class Mage3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}