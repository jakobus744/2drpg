using System;
using RPG2d.World.Items.Data;

namespace RPG2d.World.Items.Inventory;

public class ItemStack
{

    public ItemData Data;
    public int Count;


    public ItemStack(ItemData data, int count = 1)
    {
        Data = data;
        Count = count;
    }

    public bool IsEmpty => Data == null || Count <= 0;
    public int MaxStack => Data?.MaxStackSize ?? 1;
    public int FreeSpace => Data == null ? 0 : MaxStack - Count;



    public bool CanMergeWith(ItemStack other)
    {
        if (IsEmpty || other == null || other.IsEmpty) return false;
        if (other.Data != Data) return false;
        return FreeSpace > 0;
    }

    public int Add(int amount)
    {
        int added = Math.Min(FreeSpace, amount);
        Count += added;
        return amount - added;   // Rest der nicht passte
    }

    public ItemStack Split(int amount)
    {
        amount = Math.Min(amount, Count);
        Count -= amount;
        return new ItemStack(Data, amount);
    }

}