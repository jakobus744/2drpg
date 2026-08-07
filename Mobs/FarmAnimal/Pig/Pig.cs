using Godot;

public partial class Pig : MobBase
{
    protected override void OnReady()
    {
        TargetPolicy = MobTargetPolicy.Passive;
        RetaliateOnHit = false;
    }
}