using Godot;

namespace RPG2d.World.Items.Data;

[GlobalClass]
public partial class ConsumableData : Resource
{
    [Export] public ItemData Item;
    [Export] public float HealthRestore;
    [Export] public float StaminaRestore;
}
