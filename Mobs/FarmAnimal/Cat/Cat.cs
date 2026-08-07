using Godot;

public partial class Cat : MobBase
{
    protected override void OnReady()
    {
        TargetPolicy = MobTargetPolicy.Passive;
        RetaliateOnHit = false;
    }
}