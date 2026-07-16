using Godot;
using RPG2d.Player;

public partial class Goblin : MobBase
{
    private const float AggroRange = 300f;
    private const float AttackRange = 32f;

    protected override void OnReady()
    {
        UsePathfinding = true;
        PathRecalcInterval = 0.3f;
        TargetReachedDistance = AttackRange;
        MoveSpeed = 60f;
    }

    protected override void ProcessAI(double delta)
    {
        var player = Player.LocalPlayer;
        if (player == null || !IsInstanceValid(player))
            return;

        float dist = GlobalPosition.DistanceTo(player.GlobalPosition);

        if (dist > AggroRange || dist <= AttackRange)
        {
            Velocity = Vector2.Zero;
            return;
        }

        SetDestination(player.GlobalPosition);
        MoveAlongPath(delta);
        CheckStuck();
    }
}
