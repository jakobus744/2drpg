using System.Collections.Generic;
using Godot;
using RPG2d.World.Items.Data;
using RPG2d.World.Items.Inventory;

namespace RPG2d.UI.Inventory;

// Inventar-Panel (Tab): Grid (24 Slots) + Equipment-Slots (7).
// Togglet mit der "inventar"-Action. Spiel läuft weiter (Multiplayer).
public partial class InventoryPanelUI : Control
{
    [Export] private PackedScene _slotScene;
    [Export] private GridContainer _grid;        // Eltern der 24 Grid-Slots
    [Export] private Control _equipmentRoot;     // Eltern der 7 Equipment-Slots
    [Export] private int _slotSize = 21;

    private readonly InventorySlotUI[] _gridSlots = new InventorySlotUI[PlayerInventory.GridSize];
    private readonly List<InventorySlotUI> _equipSlots = new();
    private PlayerInventory _inventory;

    public override void _Ready()
    {
        Visible = false;   // startet versteckt

        // Grid-Slots erzeugen (GridContainer ordnet sie automatisch an)
        if (_grid != null)
        {
            _grid.Columns = 6;
            for (int i = 0; i < _gridSlots.Length; i++)
            {
                var slot = _slotScene.Instantiate<InventorySlotUI>();
                _grid.AddChild(slot);
                slot.CustomMinimumSize = new Vector2(_slotSize, _slotSize);
                _gridSlots[i] = slot;
            }
        }

        // Equipment-Slots werden NICHT generiert — du platzierst sie im Editor
        // unter dem Equipment-Node und setzt je 'DesignEquipSlot'. Hier nur einsammeln.
        if (_equipmentRoot != null)
        {
            foreach (var child in _equipmentRoot.GetChildren())
                if (child is InventorySlotUI slot && slot.DesignEquipSlot != EquipSlot.None)
                    _equipSlots.Add(slot);
        }
    }

    public override void _Process(double delta)
    {
        // Lazy-Bind sobald LocalPlayer existiert
        if (_inventory != null) return;

        var player = Player.Player.LocalPlayer;
        if (player == null) return;

        _inventory = player.Inventory;
        for (int i = 0; i < _gridSlots.Length; i++)
            _gridSlots[i].Setup(_inventory, SlotAddress.Grid(i));
        foreach (var slot in _equipSlots)
            slot.Setup(_inventory, SlotAddress.Equipment(slot.DesignEquipSlot));

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
        foreach (var s in _gridSlots) s.Refresh();
        foreach (var s in _equipSlots) s.Refresh();
    }

    // _Input statt _UnhandledInput: läuft VOR der GUI-Fokus-Navigation.
    // Sonst frisst Godots "ui_focus_next" (= Tab) das Event, bevor wir es sehen.
    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("inventar"))
        {
            Visible = !Visible;
            GetViewport().GuiReleaseFocus();   // kein Control behält Fokus → Tab bleibt unser
            GetViewport().SetInputAsHandled();
        }
    }
}
