using Godot;

/// <summary>
/// Abstrakte Basisklasse für alle Mobs.
/// Leite davon ab: class Slime : MobBase
/// </summary>
public abstract partial class MobBase : CharacterBody2D
{
    [Export] public float MaxHealth { get; set; } = 100f;
    [Export] public float MoveSpeed { get; set; } = 80f;

    protected float CurrentHealth;
    protected AnimatedSprite2D Sprite;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        Sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        OnReady();
    }

    protected virtual void OnReady() { }

    public virtual void TakeDamage(float amount)
    {
        CurrentHealth -= amount;
        if (CurrentHealth <= 0)
            Die();
    }

    protected virtual void Die()
    {
        QueueFree();
    }
}
