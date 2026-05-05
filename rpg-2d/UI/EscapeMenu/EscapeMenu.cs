using Godot;

namespace RPG2d.UI.EscapeMenu;

public partial class EscapeMenu : CanvasLayer
{
    [Export] private Button _resumeButton;
    [Export] private Button _settingsButton;
    [Export] private Button _mainMenuButton;

    private Control _settingsMenu;

    public override void _Ready()
    {
        Visible = false;

        _resumeButton.Pressed += () =>
        {
            Visible = false;
            GetTree().Paused = false;
        };
        _settingsButton.Pressed += OpenSettings;
        _mainMenuButton.Pressed += () =>
            GetTree().ChangeSceneToFile("res://UI/MainMenu/MainMenu.tscn");
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Input.IsActionJustPressed("ui_cancel")) return;

        Visible = !Visible;
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
}
