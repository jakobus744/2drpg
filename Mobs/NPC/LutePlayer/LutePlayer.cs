using Godot;

public partial class LutePlayer : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
    }
}