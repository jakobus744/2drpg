using Godot;
using RPG2d.Entity;

public struct MobState
{
    public MobState() { }

    [NetVar(Tolerance = 1f)] public Vector2 Position;
    [NetVar(Tolerance = 1f)] public Vector2 Velocity;
    [NetVar(Tolerance = 1f)] public float Health;
    [NetVar] public bool IsDead;
}
