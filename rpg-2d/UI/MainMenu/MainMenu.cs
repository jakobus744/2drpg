using Godot;

namespace RPG2d.UI.MainMenu;

public partial class MainMenu : Control
{
    [Export] private Button _hostButton;
    [Export] private Button _joinButton;
    [Export] private Button _settingsButton;
    [Export] private Button _quitButton;

    // SettingsMenu wird als eigene Scene über CanvasLayer geladen
    private Control _settingsMenu;

    private GameManager.GameManager _gameManager;

    public override void _Ready()
    {
        _gameManager = GetNode<GameManager.GameManager>("/root/GameManager");

        _hostButton.Pressed += () => _gameManager.StartHost();
        _joinButton.Pressed += () => _gameManager.JoinGame();
        _settingsButton.Pressed += OpenSettings;
        _quitButton.Pressed += () => GetTree().Quit();
    }

    private void OpenSettings()
    {
        if (_settingsMenu != null) return;
        var scene = GD.Load<PackedScene>("res://UI/Settings/Settings.tscn");
        _settingsMenu = scene.Instantiate<Control>();
        _settingsMenu.TreeExited += () => _settingsMenu = null;
        AddChild(_settingsMenu);
    }
}
