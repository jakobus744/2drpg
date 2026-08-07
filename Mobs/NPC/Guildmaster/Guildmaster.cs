using Godot;

public partial class Guildmaster : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}