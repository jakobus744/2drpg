using Godot;

namespace RPG2d.World.Items;

public partial class WeaponItem : PickupItem
{
    protected override void Equip(RPG2d.Player.Player player, PackedScene dropped) =>
        player.EquipWeapon(dropped, ItemTexture, ItemRegion, ItemScale, ItemOffset, ItemRotation);
}
