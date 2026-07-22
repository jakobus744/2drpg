using Godot;
using RPG2d.World.Items.Data;

namespace RPG2d.World.Items;

// Boden-Item für Rüstung (Helm/Chest/Boots). Beim Aufheben landet die ItemData
// im Inventar; das Anlegen (und damit das Layer-Visual) passiert erst wenn der
// Spieler das Item in den passenden Equipment-Slot zieht.
public partial class ArmorItem : PickupItem
{
    [Export] public ItemData Item;

    // Rüstung hat kein Pickup-Sofort-Visual — no-op.
    protected override void Equip(RPG2d.Player.Player player, PackedScene dropped) { }

    public override ItemData GetItemData() => Item;
}
