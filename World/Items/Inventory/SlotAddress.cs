using RPG2d.World.Items.Data;
namespace RPG2d.World.Items.Inventory;

public enum SlotType : byte { Grid = 0, Hotbar = 1, Equipment = 2 }

public struct SlotAddress
{
    public SlotType Type;
    public int Index;
    public EquipSlot Equip;

    public static SlotAddress Grid(int i) => new() { Type = SlotType.Grid, Index = i };
    public static SlotAddress Hotbar(int i) => new() { Type = SlotType.Hotbar, Index = i };
    public static SlotAddress Equipment(EquipSlot e) => new() { Type = SlotType.Equipment, Equip = e };

    public byte ToIndexByte()
    {
        byte typeBits = (byte)((byte)Type << 6);
        byte idx = Type == SlotType.Equipment ? (byte)Equip : (byte)Index;
        return (byte)(typeBits | (idx & 0x3F));
    }

    public static SlotAddress FromIndexByte(byte b)
    {
        var type = (SlotType)(b >> 6);
        byte idx = (byte)(b & 0x3F);
        return type switch
        {
            SlotType.Grid => Grid(idx),
            SlotType.Hotbar => Hotbar(idx),
            SlotType.Equipment => Equipment((EquipSlot)idx),
            _ => default
        };
    }
}