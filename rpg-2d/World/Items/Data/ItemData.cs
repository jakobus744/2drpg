using Godot;

namespace RPG2d.World.Items.Data;

[GlobalClass]
public partial class ItemData : Resource
{
    [Export] public string ItemId = "";          // z.B. "axt", "iron_helmet"
    [Export] public string DisplayName = "";
    [Export] public string Description = "";
    [Export] public Texture2D Icon;
    [Export] public Rect2 IconRegion;            // Atlas Sub-Rect
    [Export] public float IconRotation = 0f;     // Icon-Drehung in Grad (z.B. 45 für Waffen)
    [Export] public float IconScale = 1f;        // relative Icon-Größe im Slot (1 = volle Zelle)
    [Export] public ItemCategory Category;
    [Export] public EquipSlot Slot = EquipSlot.None;
    [Export] public int MaxStackSize = 1;        // 1 für Waffen, höher für Tränke
    [Export] public string DroppedScenePath = "";
}
