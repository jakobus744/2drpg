using Godot;
using RPG2d.World.Items.Data;

namespace RPG2d.World.Items;

public abstract partial class PickupItem : Area2D
{
    [Export] public string DroppedScenePath = "";
    [Export] public Texture2D ItemTexture;

    // >0 = dieses Boden-Item wurde gedroppt und trägt eine Rest-Anzahl (statt PickupAmount).
    public int AmountOverride = 0;
    [Export] public Rect2 ItemRegion;
    [Export] public Vector2 ItemScale = Vector2.One;
    [Export] public Vector2 ItemOffset = Vector2.Zero;
    [Export] public float ItemRotation = 0f;

    protected RPG2d.Player.Player PlayerInRange;
    private PackedScene _droppedScene;

    public override void _Ready()
    {
        _droppedScene = !string.IsNullOrEmpty(DroppedScenePath)
            ? GD.Load<PackedScene>(DroppedScenePath)
            : null;

        BodyEntered += body =>
        {
            if (body is RPG2d.Player.Player p)
            {
                PlayerInRange = p;
                p.RegisterNearbyPickup(this);
            }
        };
        BodyExited += body =>
        {
            if (body is RPG2d.Player.Player p)
            {
                PlayerInRange = null;
                p.UnregisterNearbyPickup(this);
            }
        };
    }

    public override void _Process(double delta)
    {
    }

    protected abstract void Equip(RPG2d.Player.Player player, PackedScene dropped);

    // Subklasse liefert die ItemData-Vorlage für dieses Item (fürs Inventar)
    public abstract ItemData GetItemData();

    // RPC stellt sicher dass Item bei allen Clients verschwindet
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true,
         TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RemoveItem()
    {
        if (Multiplayer.IsServer())
        {
            var gm = GetNodeOrNull<RPG2d.GameManager.GameManager>("/root/GameManager");
            gm?.TrackRemovedItem(SceneFilePath);
			gm?.Rpc("RemoveItemByScene", SceneFilePath, GlobalPosition);
        }
        QueueFree();
    }
}
