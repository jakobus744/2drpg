using Godot;

namespace RPG2d.World.Items;

[GlobalClass]
public partial class WeaponData : Resource
{
    [Export] public string WeaponName = "Unarmed";
    [Export] public float Damage = 10f;
    [Export] public uint AttackCooldownTicks = 30;

    // Stamina-Kosten pro Angriff (Basis = stehend)
    [Export] public float AttackStaminaCost = 10f;

    // Multiplikatoren je nach Bewegungszustand
    [Export] public float WalkAttackMultiplier = 1.2f;
    [Export] public float RunAttackMultiplier = 1.5f;
}