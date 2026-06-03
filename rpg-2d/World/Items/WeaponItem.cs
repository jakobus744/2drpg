using Godot;
using RPG2d.World.Items.Data;

namespace RPG2d.World.Items;

public partial class WeaponItem : PickupItem
{
	[Export] public WeaponData Stats;
	
	public bool IsEquipped = false;
	public Area2D Hitbox { get; private set; }
	
	public override void _Ready()
	{
		base._Ready();
		Hitbox = GetNodeOrNull<Area2D>("HitboxArea");
	}
	
	public override void _Process(double delta)
	{
		if (IsEquipped) return;

		base._Process(delta);
	}
	
	protected override void Equip(RPG2d.Player.Player player, PackedScene dropped) =>
		player.EquipWeapon(this);

	public override ItemData GetItemData() => Stats?.Item;
}
