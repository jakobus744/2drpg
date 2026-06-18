using Godot;

namespace RPG2d.World;

// Eine Zone + ihre Position im Welt-Raster (z.B. (0,0), (1,0), (0,1)).
[GlobalClass]
public partial class ZoneEntry : Resource
{
    [Export] public Vector2I Coord;     // Raster-Koordinate
    [Export] public PackedScene Scene;  // die Zonen-Szene (Forest, Desert, ...)
}
