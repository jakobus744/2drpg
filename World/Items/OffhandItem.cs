using Godot;
using RPG2d.World.Items.Data;

namespace RPG2d.World.Items;

public partial class OffhandItem : PickupItem
{
	[Export] public ItemData Item;

	protected override void Equip(RPG2d.Player.Player player, PackedScene dropped) =>
		player.EquipOffhand(dropped, ItemTexture, ItemRegion, ItemScale, ItemOffset, ItemRotation);

	public override ItemData GetItemData() => Item;
}
