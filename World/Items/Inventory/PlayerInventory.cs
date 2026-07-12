using System.Collections.Generic;
using System.IO;
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
        if (s.IsEmpty) SetSlot(addr, null);   // feuert Changed
        else NotifyChanged();                  // Menge sank, Slot nicht leer → UI trotzdem updaten
    }

    private static readonly EquipSlot[] EquipOrder =
        { EquipSlot.Weapon, EquipSlot.Offhand, EquipSlot.Helmet, EquipSlot.Armor, EquipSlot.Boots, EquipSlot.Ring1, EquipSlot.Ring2 };

    public byte[] Serialize()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        SerializeSlots(writer);
        return stream.ToArray();
    }

    public void Deserialize(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        DeserializeSlots(reader);
        NotifyChanged();
    }

    private void SerializeSlots(BinaryWriter w)
    {
        for (int i = 0; i < GridSize; i++) WriteSlot(w, Grid[i]);
        for (int i = 0; i < HotbarSize; i++) WriteSlot(w, Hotbar[i]);
        foreach (var es in EquipOrder)
            WriteSlot(w, EquipmentSlots.GetValueOrDefault(es));
        w.Write(ActiveHotbarIndex);
    }

    private static void WriteSlot(BinaryWriter w, ItemStack stack)
    {
        if (stack == null || stack.IsEmpty)
            w.Write(false);
        else
        {
            w.Write(true);
            w.Write(stack.Data.ItemId ?? "");
            w.Write(stack.Count);
        }
    }

    private void DeserializeSlots(BinaryReader r)
    {
        for (int i = 0; i < GridSize; i++)
            Grid[i] = ReadSlot(r);
        for (int i = 0; i < HotbarSize; i++)
            Hotbar[i] = ReadSlot(r);
        EquipmentSlots.Clear();
        foreach (var es in EquipOrder)
        {
            var stack = ReadSlot(r);
            if (stack != null)
                EquipmentSlots[es] = stack;
        }
        ActiveHotbarIndex = r.ReadInt32();
    }

    private static ItemStack ReadSlot(BinaryReader r)
    {
        if (!r.ReadBoolean()) return null;
        var itemId = r.ReadString();
        var count = r.ReadInt32();
        var data = ItemDatabase.Get(itemId);
        return data != null ? new ItemStack(data, count) : null;
    }

    public void CopyFrom(PlayerInventory other)
    {
        for (int i = 0; i < GridSize; i++)
            Grid[i] = other.Grid[i]?.Clone();
        for (int i = 0; i < HotbarSize; i++)
            Hotbar[i] = other.Hotbar[i]?.Clone();
        EquipmentSlots.Clear();
        foreach (var kvp in other.EquipmentSlots)
            EquipmentSlots[kvp.Key] = kvp.Value?.Clone();
        ActiveHotbarIndex = other.ActiveHotbarIndex;
        NotifyChanged();
    }
}