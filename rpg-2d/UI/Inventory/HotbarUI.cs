using Godot;
using RPG2d.World.Items.Inventory;

namespace RPG2d.UI.Inventory;

// Hotbar: 8 Slots nebeneinander. Spiegelt PlayerInventory.Hotbar.
// Tasten 1-8 wählen den aktiven Slot.
public partial class HotbarUI : Control
{
    [Export] private PackedScene _slotScene;
    [Export] private int _slotSize = 21;
    [Export] private Vector2 _startOffset = Vector2.Zero; // wo Slot 0 beginnt (in die erste Zelle schieben)

    private readonly InventorySlotUI[] _slots = new InventorySlotUI[PlayerInventory.HotbarSize];
    private PlayerInventory _inventory;

    public override void _Ready()
    {
        // Slots erzeugen + nebeneinander platzieren
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slotScene.Instantiate<InventorySlotUI>();
            AddChild(slot);
            slot.Position = _startOffset + new Vector2(i * _slotSize, 0);
            slot.CustomMinimumSize = new Vector2(_slotSize, _slotSize);
            slot.Size = new Vector2(_slotSize, _slotSize);   // Icon füllt Slot → _slotSize steuert Größe
            _slots[i] = slot;
        }
    }

    public override void _Process(double delta)
    {
        // Lazy-Bind: LocalPlayer existiert erst nach Spawn (asynchron)
        if (_inventory != null) return;

        var player = Player.Player.LocalPlayer;
        if (player == null) return;

        _inventory = player.Inventory;
        for (int i = 0; i < _slots.Length; i++)
            _slots[i].Setup(_inventory, SlotAddress.Hotbar(i));

        _inventory.Changed += RefreshAll;   // bei jeder Inventar-Änderung neu zeichnen
        RefreshAll();
    }

    public override void _ExitTree()
    {
        if (_inventory != null)
            _inventory.Changed -= RefreshAll;   // abmelden = kein Leak
    }

    private void RefreshAll()
    {
        foreach (var slot in _slots)
            slot.Refresh();
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        for (int i = 0; i < _slots.Length; i++)
            _slots[i].SetHighlight(i == _inventory.ActiveHotbarIndex);
    }

    private void Cycle(int dir)
    {
        int n = _slots.Length;
        _inventory.ActiveHotbarIndex = (_inventory.ActiveHotbarIndex + dir + n) % n;
        UpdateHighlight();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_inventory == null) return;

        // Mausrad → aktiven Slot durchblättern (wrap)
        if (@event is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelDown) { Cycle(1); return; }
            if (mb.ButtonIndex == MouseButton.WheelUp) { Cycle(-1); return; }
        }

        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        int n = key.Keycode switch
        {
            Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3,
            Key.Key5 => 4, Key.Key6 => 5, Key.Key7 => 6, Key.Key8 => 7,
            Key.Key9 => 8, Key.Key0 => 9,
            _ => -1,
        };
        if (n < 0) return;

        _inventory.ActiveHotbarIndex = n;
        UpdateHighlight();
    }
}
