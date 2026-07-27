using Godot;

public partial class Ent3 : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}