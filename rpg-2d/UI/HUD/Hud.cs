using Godot;

namespace RPG2d.UI.HUD;

public partial class Hud : CanvasLayer
{
    private TextureProgressBar _healthBar;
    private TextureProgressBar _staminaBar;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _healthBar = GetNode<TextureProgressBar>("MarginContainer/VBoxContainer/HealthBar");
        _staminaBar = GetNode<TextureProgressBar>("MarginContainer/VBoxContainer/StaminaBar");

        _healthBar.MaxValue = 100f;
        _staminaBar.MaxValue = 100f;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if (Player.Player.LocalPlayer == null)
            return;

        var state = Player.Player.LocalPlayer.Input.GetState(Player.Player.LocalPlayer.Input.CurrentTick);
        if (state == null) return;
        _healthBar.Value = state.Health;
        _staminaBar.Value = state.Stamina;
    }
}