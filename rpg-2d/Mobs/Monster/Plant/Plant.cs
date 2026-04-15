using Godot;

public partial class Plant : MobBase
{
    private AnimatedSprite2D _sprite;
    private string _dir = "down";

    protected override void OnReady()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        PlayAnim("idle");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Velocity.Length() > 0)
            UpdateDir(Velocity);
    }

    private void UpdateDir(Vector2 v)
    {
        if (Mathf.Abs(v.X) > Mathf.Abs(v.Y))
            _dir = v.X > 0 ? "right" : "left";
        else
            _dir = v.Y > 0 ? "down" : "up";
    }

    public void PlayAnim(string anim)
    {
        string full = anim + "_" + _dir;
        if (_sprite.SpriteFrames.HasAnimation(full))
            _sprite.Play(full);
    }

    public override void TakeDamage(float amount)
    {
        base.TakeDamage(amount);
        if (CurrentHealth > 0) PlayAnim("hurt");
    }

    protected override void Die()
    {
        PlayAnim("death");
        _sprite.AnimationFinished += () => base.Die();
    }
}