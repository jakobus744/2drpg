using Godot;

namespace RPG2d.UI.HUD;

public partial class Hud : CanvasLayer
{
    [Export]
    private TextureProgressBar _healthBar;
    [Export]
    private TextureProgressBar _staminaBar;
    [Export]
    private TextureProgressBar _expBar;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _healthBar.MaxValue = 100f;
        _staminaBar.MaxValue = 100f;
        _expBar.MaxValue = 100f;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if (Player.Player.LocalPlayer == null)
            return;

        var player = Player.Player.LocalPlayer;
        var state = player.StateBuffer.Get(player.CurrentTick);
        _healthBar.Value = state.Health;
        _staminaBar.Value = state.Stamina;
    }
}