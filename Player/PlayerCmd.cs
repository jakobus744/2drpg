using System.IO;
using Godot;

namespace RPG2d.Player;

public enum InvActionType : byte { None, Pickup, Swap, Drop, Consume }

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

    // Consumable-Nutzung (Rechtsklick auf aktivem Hotbar-Slot).
    public bool IsUseItemPressed;
    public float UseStaminaRestore;
    public float UseHealthRestore;

    // Inventar-Aktion (0=none, 1=Pickup, 2=Swap, 3=Drop, 4=Consume)
    public byte InvAction;
    public byte InvSlotA;       // SlotAddress.ToIndexByte() des primären Slots
    public byte InvSlotB;       // SlotAddress.ToIndexByte() des zweiten Slots (Swap, sonst 0)
    public string InvItemId;    // ItemData.ItemId zur Validierung
    public byte InvCount;       // Anzahl (Drop, sonst 0)
    public byte ActiveHotbarIndex;

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
        writer.Write(IsUseItemPressed);
        writer.Write(UseStaminaRestore);
        writer.Write(UseHealthRestore);

        writer.Write(InvAction);
        if (InvAction != 0)
        {
            writer.Write(InvSlotA);
            writer.Write(InvSlotB);
            writer.Write(InvItemId ?? "");
            writer.Write(InvCount);
        }
        writer.Write(ActiveHotbarIndex);

        return stream.ToArray();
    }

    public static PlayerCmd FromBytes(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        var cmd = new PlayerCmd
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
            EquippedOffhandPath = reader.ReadString(),
            IsUseItemPressed = reader.ReadBoolean(),
            UseStaminaRestore = reader.ReadSingle(),
            UseHealthRestore = reader.ReadSingle(),
        };

        cmd.InvAction = reader.ReadByte();
        if (cmd.InvAction != 0)
        {
            cmd.InvSlotA = reader.ReadByte();
            cmd.InvSlotB = reader.ReadByte();
            cmd.InvItemId = reader.ReadString();
            cmd.InvCount = reader.ReadByte();
        }
        cmd.ActiveHotbarIndex = reader.ReadByte();

        return cmd;
    }
}