using Godot;

/// <summary>
/// Base class for all mobs (monsters, animals, NPCs).
/// Provides health, damage, death, and basic state.
/// </summary>
public abstract partial class MobBase : CharacterBody2D
{
    [Export] public float MaxHealth   { get; set; } = 100f;
    [Export] public float MoveSpeed   { get; set; } = 60f;
    [Export] public float AttackDamage{ get; set; } = 10f;

    public float CurrentHealth { get; protected set; }
    public bool  IsDead        { get; protected set; } = false;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        OnReady();
    }

    /// <summary>Override in subclass for additional _Ready logic.</summary>
    protected virtual void OnReady() { }

    public virtual void TakeDamage(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        if (CurrentHealth <= 0f)
            Die();
    }

    protected virtual void Die()
    {
        IsDead = true;
        // Subclass handles death animation; call QueueFree when done.
        // Default: remove immediately if no override.
        QueueFree();
    }

    public virtual void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
    }
}
