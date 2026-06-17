using Godot;

public partial class Goblin : MobBase
{
    private Vector2 _spawnPos;
    private float _angle;

    protected override void OnReady()
    {
        _spawnPos = Position;
    }

    protected override void ProcessAI(double delta)
    {
        _angle += (float)delta * 2f; // ~1.2s per full revolution at 60fps
        Vector2 target = _spawnPos + new Vector2(
            Mathf.Cos(_angle) * 40f,
            Mathf.Sin(_angle) * 40f
        );
        Velocity = (target - Position).Normalized() * MoveSpeed;
    }
}
