using Godot;

namespace RPG2d.World.Items;

// Basisklasse für alle aufhebbare Items (Waffe, Offhand, später: Tränke, etc.)
// Enthält die gesamte Pickup-Logik —Subklassen implementieren nur was beim Equip passiert
public abstract partial class PickupItem : Area2D
{
    // Pfad zur Scene die gespawnt wird wenn Spieler dieses Item gegen ein anderes tauscht
    [Export] public string DroppedScenePath = "";
    [Export] public Texture2D ItemTexture;
    [Export] public Rect2 ItemRegion;
    [Export] public Vector2 ItemScale = Vector2.One;
    [Export] public Vector2 ItemOffset = Vector2.Zero;
    [Export] public float ItemRotation = 0f;

    // Lokaler Spieler der gerade in der Area steht (null wenn niemand)
    protected RPG2d.Player.Player PlayerInRange;
    private PackedScene _droppedScene;

    public override void _Ready()
    {
        // Vorladen verhindert Frame-Hitch durch GD.Load beim Aufnehmen
        _droppedScene = !string.IsNullOrEmpty(DroppedScenePath)
            ? GD.Load<PackedScene>(DroppedScenePath)
            : null;

        // Nur lokalen Spieler speichern — andere Multiplayer-Clients ignorieren
        BodyEntered += body =>
        {
            if (body is RPG2d.Player.Player p && p.IsMultiplayerAuthority())
                PlayerInRange = p;
        };
        BodyExited += body =>
        {
            if (body is RPG2d.Player.Player)
                PlayerInRange = null;
        };
    }

    public override void _Process(double delta)
    {
        if (PlayerInRange == null || !Input.IsActionJustPressed("interact")) return;
        Equip(PlayerInRange, _droppedScene);
        if (Multiplayer.HasMultiplayerPeer())
            Rpc(MethodName.RemoveItem);
        else
            QueueFree();
    }

    // Subklasse bestimmt was beim Aufnehmen passiert (Waffe / Offhand / etc.)
    protected abstract void Equip(RPG2d.Player.Player player, PackedScene dropped);

    // RPC stellt sicher dass Item bei allen Clients verschwindet
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RemoveItem() => QueueFree();
}
