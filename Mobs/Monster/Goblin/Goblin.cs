using Godot;

public partial class Goblin : MobBase
{
    protected override void OnReady()
    {
        UsePathfinding = true;
        PathRecalcInterval = 0.3f;
        MoveSpeed = 60f;
        TargetPolicy = MobTargetPolicy.ClosestPlayer;
        RetaliateOnHit = true;
        AggroRange = 300f;
        DeaggroRange = 450f;
        AttackRange = 32f;
        AttackDamage = 15f;
        AttackCooldown = 1.2f;
        AttackWindupTime = 0.3f;
    }
}
