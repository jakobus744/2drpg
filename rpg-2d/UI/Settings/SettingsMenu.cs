using Godot;

namespace RPG2d.UI.Settings;

public partial class SettingsMenu : Control
{
    [Export] private HSlider _musicSlider;
    [Export] private HSlider _sfxSlider;
    [Export] private CheckButton _fullscreenToggle;
    [Export] private Button _saveButton;
    [Export] private Button _declineButton;

    [Export] private Button _closeButton;

    private int _musicBus;
    private int _sfxBus;

    // Werte beim Öffnen merken — bei Decline wiederherstellen
    private float _savedMusicVolume;
    private float _savedSfxVolume;
    private bool _savedFullscreen;

    public override void _Ready()
    {
        _musicBus = AudioServer.GetBusIndex("Music");
        _sfxBus = AudioServer.GetBusIndex("Master");

        // Aktuelle Werte merken
        _savedMusicVolume = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(_musicBus));
        _savedSfxVolume = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(_sfxBus));
        _savedFullscreen = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;

        // Slider auf aktuelle Werte setzen
        _musicSlider.Value = _savedMusicVolume;
        _sfxSlider.Value = _savedSfxVolume;
        _fullscreenToggle.ButtonPressed = _savedFullscreen;



        if (_closeButton != null)
            _closeButton.Pressed += () => Hide();

        // Live-Preview beim Schieben
        _musicSlider.ValueChanged += v =>
            AudioServer.SetBusVolumeDb(_musicBus, Mathf.LinearToDb((float)v));
        _sfxSlider.ValueChanged += v =>
            AudioServer.SetBusVolumeDb(_sfxBus, Mathf.LinearToDb((float)v));
        _fullscreenToggle.Toggled += on => DisplayServer.WindowSetMode(
            on ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);

        _saveButton.Pressed += Save;
        _declineButton.Pressed += Decline;
    }

    private void Save()
    {
        // TODO: Werte in Config-Datei speichern (ConfigFile)
        QueueFree();
    }

    private void Decline()
    {
        // Alte Werte wiederherstellen
        AudioServer.SetBusVolumeDb(_musicBus, Mathf.LinearToDb(_savedMusicVolume));
        AudioServer.SetBusVolumeDb(_sfxBus, Mathf.LinearToDb(_savedSfxVolume));
        DisplayServer.WindowSetMode(_savedFullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
        QueueFree();
    }
}
