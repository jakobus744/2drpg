using Godot;

public partial class Calt : MobBase
{
    private AnimatedSprite2D _sprite;
    private string _dir = "down";

    protected override void OnReady()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        PlayAnim("idle");
    }

    private void PlayAnim(string anim)
    {
        string full = anim + "_" + _dir;
        if (_sprite.SpriteFrames.HasAnimation(full))
            _sprite.Play(full);
        else if (_sprite.SpriteFrames.HasAnimation(anim + "_down"))
            _sprite.Play(anim + "_down");
    }

    public override void TakeDamage(float amount) { base.TakeDamage(amount); }
    protected override void Die() { base.Die(); }
}