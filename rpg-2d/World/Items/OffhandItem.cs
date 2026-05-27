using Godot;

namespace RPG2d.World.Items;

public partial class OffhandItem : PickupItem
{
	protected override void Equip(RPG2d.Player.Player player, PackedScene dropped) =>
		player.EquipOffhand(dropped, ItemTexture, ItemRegion, ItemScale, ItemOffset, ItemRotation);
}
