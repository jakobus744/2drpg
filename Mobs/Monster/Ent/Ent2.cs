using Godot;

public partial class Ent2 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}