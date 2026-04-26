using Godot;
using RPG2d.Player;

namespace RPG2d.World.Items;

public partial class TestItem : Area2D
{
    [Export] public Texture2D WeaponTexture;
    [Export] public Rect2 WeaponRegion;

    private Player.Player _playerInRange;

    public override void _Ready()
    {
        BodyEntered += body =>
        {
            if (body is Player.Player p && p.IsMultiplayerAuthority())
                _playerInRange = p;
        };
        BodyExited += body =>
        {
            if (body is Player.Player)
                _playerInRange = null;
        };
    }

    public override void _Process(double delta)
    {
        if (_playerInRange != null && Input.IsActionJustPressed("interact"))
        {
            _playerInRange.EquipWeapon(WeaponTexture, WeaponRegion);
            QueueFree();
        }
    }
}