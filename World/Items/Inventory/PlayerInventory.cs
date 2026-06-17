using System.Collections.Generic;
using RPG2d.World.Items.Data;
using Action = System.Action;
namespace RPG2d.World.Items.Inventory;

public class PlayerInventory
{
    public event Action Changed;
    private void NotifyChanged() => Changed?.Invoke();
    public const int GridSize = 16;
    public const int HotbarSize = 10;

    public ItemStack[] Grid = new ItemStack[GridSize];
    public ItemStack[] Hotbar = new ItemStack[HotbarSize];
    public Dictionary<EquipSlot, ItemStack> EquipmentSlots = new();
    public int ActiveHotbarIndex = 0;

    // Liest Stack an beliebiger Adresse (null = leer)
    public ItemStack GetSlot(SlotAddress addr)
    {
        return addr.Type switch
        {
            SlotType.Grid => Grid[addr.Index],
            SlotType.Hotbar => Hotbar[addr.Index],
            SlotType.Equipment => EquipmentSlots.GetValueOrDefault(addr.Equip),
            _ => null
        };
    }

    public void SetSlot(SlotAddress addr, ItemStack stack)
    {
        switch (addr.Type)
        {
            case SlotType.Grid: Grid[addr.Index] = stack; break;
            case SlotType.Hotbar: Hotbar[addr.Index] = stack; break;
            case SlotType.Equipment: EquipmentSlots[addr.Equip] = stack; break;
        }
        NotifyChanged();
    }




    public bool TryAddItem(ItemData data, int count = 1)
    {
        if (data == null || count <= 0) return false;

        // 1. vorhandene Stapel auffüllen
        count = FillExisting(Hotbar, data, count);
        count = FillExisting(Grid, data, count);

        // 2. leere Slots füllen
        count = FillEmpty(Hotbar, data, count);
        count = FillEmpty(Grid, data, count);

        NotifyChanged();
        return count == 0;   // alles untergebracht?
    }

    private static int FillExisting(ItemStack[] slots, ItemData data, int count)
    {
        for (int i = 0; i < slots.Length && count > 0; i++)
        {
            var s = slots[i];
            if (s != null && !s.IsEmpty && s.Data == data && s.FreeSpace > 0)
                count = s.Add(count);
        }
        return count;
    }

    private static int FillEmpty(ItemStack[] slots, ItemData data, int count)
    {
        for (int i = 0; i < slots.Length && count > 0; i++)
        {
            if (slots[i] == null || slots[i].IsEmpty)
            {
                var stack = new ItemStack(data, 0);
                count = stack.Add(count);
                slots[i] = stack;
            }
        }
        return count;
    }



    // Drag&Drop: Inhalt von 'from' nach 'to' Gleiches Item stapeln, sonst tauschen
    public void SwapSlots(SlotAddress from, SlotAddress to)
    {
        if (from.Equals(to)) return;   // auf sich selbst → nix

        var a = GetSlot(from);
        var b = GetSlot(to);

        // beide gleiches Item + Platz  zusammenführen
        if (a != null && b != null && b.CanMergeWith(a))
        {
            int leftover = b.Add(a.Count);
            a.Count = leftover;
            SetSlot(from, a.IsEmpty ? null : a);
            SetSlot(to, b);
            return;
        }

        // sonst: einfach tauschen
        SetSlot(from, b);
        SetSlot(to, a);
    }

    // Items aus Slot entfernen 
    public void RemoveFromSlot(SlotAddress addr, int count = 1)
    {
        var s = GetSlot(addr);
        if (s == null || s.IsEmpty) return;
        s.Count -= count;
        if (s.IsEmpty) SetSlot(addr, null);
    }

}