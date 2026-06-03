using System.IO;
using Godot;

namespace RPG2d.Player;

public struct PlayerCmd
{
	public uint Tick;
	public Vector2 MovementVector;
	public string FacingDirection;
	public bool IsRunPressed;
	public bool IsAttackPressed;
	public bool IsRollPressed;
	public bool IsInteractPressed;
	public string InteractTargetPath;

	// Gewünschte Ausrüstung (aus den Equipment-Slots des lokalen Inventars).
	// "" = nichts angelegt. Treibt state.EquippedWeaponPath/Offhand in ProcessCommand.
	public string EquippedWeaponPath;
	public string EquippedOffhandPath;

	// Serialization für Godot Networking
	public byte[] ToBytes()
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);

		writer.Write(Tick);
		writer.Write(MovementVector.X);
		writer.Write(MovementVector.Y);
		writer.Write(FacingDirection);
		writer.Write(IsRunPressed);
		writer.Write(IsAttackPressed);
		writer.Write(IsRollPressed);
		writer.Write(IsInteractPressed);
		writer.Write(InteractTargetPath ?? "");
		writer.Write(EquippedWeaponPath ?? "");
		writer.Write(EquippedOffhandPath ?? "");

		return stream.ToArray();
	}

	public static PlayerCmd FromBytes(byte[] data)
	{
		using var stream = new MemoryStream(data);
		using var reader = new BinaryReader(stream);

		return new PlayerCmd
		{
			Tick = reader.ReadUInt32(),
			MovementVector = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
			FacingDirection = reader.ReadString(),
			IsRunPressed = reader.ReadBoolean(),
			IsAttackPressed = reader.ReadBoolean(),
			IsRollPressed = reader.ReadBoolean(),
			IsInteractPressed = reader.ReadBoolean(),
			InteractTargetPath = reader.ReadString(),
			EquippedWeaponPath = reader.ReadString(),
			EquippedOffhandPath = reader.ReadString()
		};
	}
}
