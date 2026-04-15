using Godot;

public partial class Orc : MobBase
{
    private AnimatedSprite2D _sprite;

    // Aktuelle Blickrichtung: "down" | "up" | "left" | "right"
    private string _dir = "down";

    protected override void OnReady()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        PlayAnim("idle");
    }

    public override void _PhysicsProcess(double delta)
    {
        // TODO: KI / Bewegung hier einfügen
        // Beispiel: Richtung aus Velocity ableiten
        if (Velocity.Length() > 0)
            UpdateDirection(Velocity);
    }

    private void UpdateDirection(Vector2 velocity)
    {
        if (Mathf.Abs(velocity.X) > Mathf.Abs(velocity.Y))
            _dir = velocity.X > 0 ? "right" : "left";
        else
            _dir = velocity.Y > 0 ? "down" : "up";
    }

    // Spielt eine Animation in der aktuellen Richtung
    public void PlayAnim(string animName)
    {
        string full = animName + "_" + _dir;
        if (_sprite.SpriteFrames.HasAnimation(full))
            _sprite.Play(full);
    }

    public override void TakeDamage(float amount)
    {
        base.TakeDamage(amount);
        if (CurrentHealth > 0)
            PlayAnim("hurt");
    }

    protected override void Die()
    {
        PlayAnim("death");
        _sprite.AnimationFinished += () => base.Die();
    }
}
