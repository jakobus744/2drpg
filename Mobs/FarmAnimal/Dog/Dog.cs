using Godot;

public partial class Dog : MobBase
{
    protected override void OnReady()
    {
        TargetPolicy = MobTargetPolicy.Passive;
        RetaliateOnHit = false;
    }
}