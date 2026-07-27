using Godot;

public partial class Vampir3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}