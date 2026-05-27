using Godot;

namespace RPG2d.UI.EscapeMenu;

public partial class EscapeMenu : CanvasLayer
{
    [Export] private Button _resumeButton;
    [Export] private Button _backButton;
    [Export] private Button _settingsButton;
    [Export] private Button _mainMenuButton;

    private Control _settingsMenu;

    public override void _Ready()
    {
        Visible = false;

        void Resume()
        {
            CloseSettings();
            Visible = false;
            GetTree().Paused = false;
        }
        _resumeButton.Pressed += Resume;
        _backButton.Pressed += Resume;


        _settingsButton.Pressed += OpenSettings;
        _mainMenuButton.Pressed += () =>
        {
            CloseSettings();
            GetTree().Paused = false;
            if (Multiplayer.MultiplayerPeer != null)
            {
                Multiplayer.MultiplayerPeer.Close();
                Multiplayer.MultiplayerPeer = null;
            }
            GetTree().ChangeSceneToFile("res://UI/MainMenu/MainMenu.tscn");
        };
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Input.IsActionJustPressed("ui_cancel")) return;

        Visible = !Visible;
        if (!Visible) CloseSettings();
        GetTree().Paused = Visible;
        GetViewport().SetInputAsHandled();
    }

    private void OpenSettings()
    {
        if (_settingsMenu != null) return;
        var scene = GD.Load<PackedScene>("res://UI/Settings/Settings.tscn");
        _settingsMenu = scene.Instantiate<Control>();
        _settingsMenu.TreeExited += () => _settingsMenu = null;
        AddChild(_settingsMenu);
    }

    private void CloseSettings()
    {
        _settingsMenu?.QueueFree();
        _settingsMenu = null;
    }
}
