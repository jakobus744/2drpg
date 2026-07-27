using Godot;

public partial class Cow : MobBase
{
    protected override void OnReady()
    {
        TargetPolicy = MobTargetPolicy.Passive;
        RetaliateOnHit = false;
    }
}