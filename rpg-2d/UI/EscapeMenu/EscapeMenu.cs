using Godot;

namespace RPG2d.UI.EscapeMenu;

public partial class EscapeMenu : Control
{
    [Export] private Button _resumeButton;
    [Export] private Button _settingsButton;
    [Export] private Button _mainMenuButton;

    private Control _settingsMenu;

    public override void _Ready()
    {
        Visible = false;

        _resumeButton.Pressed += () => Visible = false;
        _settingsButton.Pressed += OpenSettings;
        _mainMenuButton.Pressed += () =>
            GetTree().ChangeSceneToFile("res://UI/MainMenu/MainMenu.tscn");
    }

    // public override void _UnhandledInput(InputEvent e)
    // {
    //     if (!e.IsActionJustPressed("ui_cancel")) return;

    //     Visible = !Visible;
    //     GetViewport().SetInputAsHandled();
    // }

    private void OpenSettings()
    {
        if (_settingsMenu != null) return;
        var scene = GD.Load<PackedScene>("res://UI/Settings/SettingsMenu.tscn");
        _settingsMenu = scene.Instantiate<Control>();
        _settingsMenu.TreeExited += () => _settingsMenu = null;
        AddChild(_settingsMenu);
    }
}
