using Godot;

public partial class Boar : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
        MoveSpeed = 70f;
        TargetPolicy = MobTargetPolicy.Neutral;
        RetaliateOnHit = true;
        AttackRange = 30f;
        AttackDamage = 12f;
    }
}