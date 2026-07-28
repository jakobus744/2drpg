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
    [Export] public int PickupAmount = 1;        // wie viele man pro Aufheben bekommt (= Nutzungen bei Tränken)
    [Export] public string DroppedScenePath = "";

    // Consumable-Effekte (nur relevant wenn Category == Consumable).
    // Pro Nutzung wird 1 vom Stapel verbraucht und diese Effekte angewendet.
    [Export] public float StaminaRestore = 0f;
    [Export] public float HealthRestore = 0f;

    // Rüstungs-Material (nur relevant wenn Slot == Helmet/Armor/Boots).
    // Bestimmt welche Style-Sheets als Layer über den Charakter gelegt werden
    // (z.B. "Lila" -> Style/Helm/Lila/Helm_<anim>.png). Teil ergibt sich aus Slot.
    [Export] public string ArmorMaterial = "";
}
