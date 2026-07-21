using Godot;

public partial class Vampir2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}