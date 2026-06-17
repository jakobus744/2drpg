using System.Collections.Generic;
using Godot;
using RPG2d.World.Items.Inventory;

namespace RPG2d.UI.Inventory;

// Inventar-Panel (Tab): zeigt alle im Editor platzierten InventorySlotUI.
// Jeder Slot taggt sich selbst (DesignType+DesignIndex oder DesignEquipSlot).
public partial class InventoryPanelUI : Control
{
    private readonly List<InventorySlotUI> _slots = new();
    private PlayerInventory _inventory;

    public override void _Ready()
    {
        Visible = false;   // startet versteckt
        CollectSlots(this);
    }

    // Sammelt ALLE InventorySlotUI-Nachfahren ein (egal wie verschachtelt platziert)
    private void CollectSlots(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is InventorySlotUI slot)
                _slots.Add(slot);
            CollectSlots(child);
        }
    }

    public override void _Process(double delta)
    {
        // Lazy-Bind sobald LocalPlayer existiert
        if (_inventory != null) return;

        var player = Player.Player.LocalPlayer;
        if (player == null) return;

        _inventory = player.Inventory;
        foreach (var slot in _slots)
            slot.Setup(_inventory, slot.ResolveDesignAddress());

        _inventory.Changed += RefreshAll;
        RefreshAll();
    }

    public override void _ExitTree()
    {
        if (_inventory != null)
            _inventory.Changed -= RefreshAll;
    }

    private void RefreshAll()
    {
        foreach (var s in _slots) s.Refresh();
    }

    // _Input statt _UnhandledInput: läuft VOR der GUI-Fokus-Navigation.
    // Sonst frisst Godots "ui_focus_next" (= Tab) das Event, bevor wir es sehen.
    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("inventar"))
        {
            Visible = !Visible;
            GetViewport().GuiReleaseFocus();
            GetViewport().SetInputAsHandled();
        }
    }
}
