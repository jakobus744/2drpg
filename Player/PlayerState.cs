using Godot;
using RPG2d.Entity;

namespace RPG2d.Player;

public struct PlayerState
{
    public PlayerState()
    {
    }

    [NetVar(Tolerance = 1f)] public Vector2 Position;
    [NetVar(Tolerance = 1f)] public Vector2 Velocity;
    [NetVar(Tolerance = 1f)] public float Stamina;
    [NetVar(Tolerance = 1f)] public float Health;
    [NetVar] public uint LastHurtTick;
    [NetVar] public uint NextAttackTick;
    [NetVar] public bool IsExhausted;
    [NetVar] public string EquippedWeaponPath = "";
    [NetVar] public string EquippedOffhandPath = "";
}