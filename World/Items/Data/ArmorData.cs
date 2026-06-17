using Godot;

namespace RPG2d.World.Items.Data;

[GlobalClass]
public partial class ArmorData : Resource
{
    [Export] public ItemData Item;
    [Export] public float Defense;
}
