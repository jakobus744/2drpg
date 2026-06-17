using RPG2d.World.Items.Data;
namespace RPG2d.World.Items.Inventory;

public enum SlotType { Grid, Hotbar, Equipment }

public struct SlotAddress
{
    public SlotType Type;
    public int Index; // Grid 0-23 , Hotbar 0-7
    public EquipSlot Equip;

    public static SlotAddress Grid(int i) => new() { Type = SlotType.Grid, Index = i };
    public static SlotAddress Hotbar(int i) => new() { Type = SlotType.Hotbar, Index = i };
    public static SlotAddress Equipment(EquipSlot e) => new() { Type = SlotType.Equipment, Equip = e };

}