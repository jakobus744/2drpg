using System.IO;
using Godot;

namespace RPG2d.Player;

public class PlayerState
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Stamina;
    
    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Position.X);
        writer.Write(Position.Y);
        writer.Write(Velocity.X);
        writer.Write(Velocity.Y);
        writer.Write(Stamina);

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
            Stamina = reader.ReadSingle()
        };
    }
}