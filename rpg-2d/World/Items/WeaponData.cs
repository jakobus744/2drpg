using Godot;

namespace RPG2d.World.Items;

[GlobalClass]
public partial class WeaponData : Resource
{
    [Export] public string WeaponName = "Unarmed";
    [Export] public float Damage = 10f;
    [Export] public uint AttackCooldownTicks = 30;
}