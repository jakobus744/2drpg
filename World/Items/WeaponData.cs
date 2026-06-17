using Godot;
using RPG2d.World.Items.Data;

namespace RPG2d.World.Items;

[GlobalClass]
public partial class WeaponData : Resource
{
    [Export] public ItemData Item;
    [Export] public string WeaponName = "Unarmed";
    [Export] public float Damage = 10f;
    [Export] public uint AttackCooldownTicks = 30;

    [Export] public float AttackStaminaCost = 10f;

    [Export] public float WalkAttackMultiplier = 1.2f;
    [Export] public float RunAttackMultiplier = 1.5f;
}