using Godot;
using RPG2d.World.Items.Data;
using RPG2d.World.Items.Inventory;

namespace RPG2d.UI.Inventory;

// Ein wiederverwendbarer Slot — zeigt Icon + Anzahl eines ItemStacks.
// Wird von Hotbar UND Grid genutzt. Kennt seine eigene Adresse im Inventar.
public partial class InventorySlotUI : Control
{
    [Export] private TextureRect _icon;
    [Export] private Label _count;
    [Export] private Control _highlight; // optional: aktiver Hotbar-Slot (z.B. ColorRect)

    // Nur für im Editor platzierte Equipment-Slots: welcher Slot ist das?
    // (None = wird vom Eltern-UI per Setup() gesetzt, z.B. Grid/Hotbar)
    [Export] public EquipSlot DesignEquipSlot = EquipSlot.None;

    private PlayerInventory _inventory;
    public SlotAddress Address { get; private set; }

    public override void _Ready()
    {
        // Slot soll keinen Tastatur-Fokus greifen — sonst klaut er Tab (ui_focus_next)
        FocusMode = FocusModeEnum.None;

        // Icon soll die Zelle füllen (Aspekt erhalten) — im Code erzwingen,
        // da tscn-Werte zur Laufzeit nicht zuverlässig greifen
        if (_icon != null)
        {
            _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _icon.SetAnchorsPreset(LayoutPreset.FullRect);
            _icon.OffsetLeft = _icon.OffsetTop = _icon.OffsetRight = _icon.OffsetBottom = 0;
        }
    }

    // Vom Eltern-UI (Hotbar/Grid) aufgerufen: sagt dem Slot wer er ist
    public void Setup(PlayerInventory inventory, SlotAddress address)
    {
        _inventory = inventory;
        Address = address;
        Refresh();
    }

    // Liest den aktuellen Stack und aktualisiert Icon + Anzahl
    public void Refresh()
    {
        var stack = _inventory?.GetSlot(Address);

        if (stack == null || stack.IsEmpty)
        {
            if (_icon != null) _icon.Visible = false;
            if (_count != null) _count.Text = "";
            return;
        }

        if (_icon != null)
        {
            _icon.Visible = true;
            _icon.Texture = BuildIcon(stack);

            // Drehung + Scale um die Zellmitte (sonst dreht's um die Ecke)
            _icon.PivotOffset = _icon.Size / 2f;
            _icon.RotationDegrees = stack.Data.IconRotation;
            _icon.Scale = Vector2.One * stack.Data.IconScale;
        }
        if (_count != null)
            _count.Text = stack.Count > 1 ? stack.Count.ToString() : "";
    }

    // Baut die Icon-Textur — bei gesetzter IconRegion ein Atlas-Ausschnitt, sonst die ganze Textur
    private static Texture2D BuildIcon(ItemStack stack)
    {
        var data = stack.Data;
        if (data.Icon == null) return null;

        if (data.IconRegion.Size != Vector2.Zero)
            return new AtlasTexture { Atlas = data.Icon, Region = data.IconRegion };

        return data.Icon;
    }

    public void SetHighlight(bool on)
    {
        if (_highlight != null) _highlight.Visible = on;
    }

    // Aktueller Stack dieses Slots (null wenn leer)
    public ItemStack GetStack() => _inventory?.GetSlot(Address);

    // ---- Drag & Drop (Godot eingebaut) ----

    // Start des Ziehens: Vorschau erstellen, Quell-Slot als Payload zurückgeben
    public override Variant _GetDragData(Vector2 atPosition)
    {
        var stack = GetStack();
        if (stack == null || stack.IsEmpty) return default;

        var preview = new TextureRect
        {
            Texture = _icon?.Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = Size,
            Size = Size,
            Modulate = new Color(1, 1, 1, 0.8f),
        };
        SetDragPreview(preview);

        return Variant.From(this); // Quelle = dieser Slot
    }

    // Darf hier abgelegt werden? Equipment-Slots filtern nach passendem EquipSlot
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        var src = data.As<InventorySlotUI>();
        if (src == null || src == this) return false;

        if (Address.Type == SlotType.Equipment)
        {
            var stack = src.GetStack();
            if (stack == null || stack.IsEmpty) return false;
            if (!FitsEquip(stack.Data, Address.Equip)) return false;
        }
        return true;
    }

    // Drop ausführen: Inhalt tauschen/stapeln (Inventory feuert Changed → UI refresht)
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var src = data.As<InventorySlotUI>();
        if (src == null) return;
        _inventory.SwapSlots(src.Address, Address);
    }

    // Passt das Item in den Equipment-Slot? Ringe passen in beide Ring-Slots
    private static bool FitsEquip(ItemData item, EquipSlot slot)
    {
        if (item.Slot == slot) return true;
        if (item.Category == ItemCategory.Ring && (slot == EquipSlot.Ring1 || slot == EquipSlot.Ring2))
            return true;
        return false;
    }
}
