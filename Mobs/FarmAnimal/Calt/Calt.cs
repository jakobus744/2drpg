using Godot;

public partial class Calt : MobBase
{
    protected override void OnReady()
    {
        TargetPolicy = MobTargetPolicy.Passive;
        RetaliateOnHit = false;
    }
}