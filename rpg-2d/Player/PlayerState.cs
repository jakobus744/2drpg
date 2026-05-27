using System;
using System.IO;
using Godot;

namespace RPG2d.Player;

public class PlayerState
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Stamina;
    public float Health;
    public uint LastHurtTick;
    public uint NextAttackTick;

    private const float MaxError = 1f;

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Position.X);
        writer.Write(Position.Y);
        writer.Write(Velocity.X);
        writer.Write(Velocity.Y);
        writer.Write(Stamina);
        writer.Write(Health);
        writer.Write(LastHurtTick);
        writer.Write(NextAttackTick);

        return stream.ToArray();
    }

    public static PlayerState FromBytes(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        return new PlayerState
        {
            Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
            Velocity = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
            Stamina = reader.ReadSingle(),
            Health = reader.ReadSingle(),
            LastHurtTick = reader.ReadUInt32(),
            NextAttackTick =   reader.ReadUInt32()
        };
    }

    public bool Equals(PlayerState other)
    {
        return Position.DistanceSquaredTo(other.Position) <= MaxError * MaxError &&
               Velocity.DistanceSquaredTo(other.Velocity) <= MaxError * MaxError &&
               Math.Abs(Stamina - other.Stamina) < MaxError && Math.Abs(Health - other.Health) < MaxError &&
               LastHurtTick == other.LastHurtTick &&
               NextAttackTick == other.NextAttackTick;
    }

    public PlayerState Clone()
    {

        return new PlayerState
        {
            Position = Position,
            Velocity = Velocity,
            Stamina = Stamina,
            Health = Health,
            LastHurtTick = LastHurtTick,
            NextAttackTick = NextAttackTick
        };
    }
}