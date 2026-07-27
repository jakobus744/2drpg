using Godot;

public partial class Ent : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}