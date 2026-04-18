using Godot;

public partial class Citizen5 : CharacterBody2D
{
    private AnimatedSprite2D _sprite;
    private string _dir = "down";

    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        // Play first available animation
        if (_sprite.SpriteFrames.HasAnimation("idle_down"))
            _sprite.Play("idle_down");
        else if (_sprite.SpriteFrames.HasAnimation("idle"))
            _sprite.Play("idle");
    }

    public void PlayAnim(string anim)
    {
        string full = anim + "_" + _dir;
        if (_sprite.SpriteFrames.HasAnimation(full))
            _sprite.Play(full);
        else if (_sprite.SpriteFrames.HasAnimation(anim))
            _sprite.Play(anim);
    }
}