using Godot;

namespace RPG2d.UI.Settings;

public partial class SettingsMenu : Control
{
    [Export] private Range _musicSlider;
    [Export] private Range _sfxSlider;
    [Export] private TextureButton _fullscreenToggle;
    [Export] private Button _saveButton;
    [Export] private Button _backButton;
    [Export] private Button _declineButton;
    [Export] private Button _closeButton;

    private const string ConfigPath = "user://settings.cfg";

    private int _musicBus;
    private int _sfxBus;
    private float _savedMusicVolume;
    private float _savedSfxVolume;
    private bool _savedFullscreen;

    public override void _Ready()
    {
        _musicBus = AudioServer.GetBusIndex("Music");
        _sfxBus = AudioServer.GetBusIndex("SFX");
        
        // Gespeicherte Config laden und auf AudioServer anwenden
        LoadConfig();

        // Aktuelle AudioServer-Werte in Slider übernehmen
        if (_musicBus >= 0 && _musicSlider != null)
        {
            _savedMusicVolume = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(_musicBus));
            _musicSlider.Value = _savedMusicVolume;
        }

        if (_sfxBus >= 0 && _sfxSlider != null)
        {
            _savedSfxVolume = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(_sfxBus));
            _sfxSlider.Value = _savedSfxVolume;
        }

        // Fullscreen-Status aus Config lesen (nicht aus WindowMode, da Embedded immer Windowed meldet)
        _savedFullscreen = LoadFullscreenFromConfig();
        if (_fullscreenToggle != null)
            _fullscreenToggle.ButtonPressed = _savedFullscreen;

        // Buttons
        if (_closeButton != null)
            _closeButton.Pressed += () => QueueFree();
        if (_backButton != null)
            _backButton.Pressed += () => QueueFree();
        if (_saveButton != null)
            _saveButton.Pressed += Save;
        if (_declineButton != null)
            _declineButton.Pressed += Decline;

        // Live-Preview beim Schieben
        if (_musicBus >= 0 && _musicSlider != null)
            _musicSlider.ValueChanged += v =>
                AudioServer.SetBusVolumeDb(_musicBus, Mathf.LinearToDb((float)v));
        if (_sfxBus >= 0 && _sfxSlider != null)
            _sfxSlider.ValueChanged += v =>
                AudioServer.SetBusVolumeDb(_sfxBus, Mathf.LinearToDb((float)v));
        if (_fullscreenToggle != null)
            _fullscreenToggle.Toggled += on => SetFullscreen(on);
    }

    private void Save()
    {
        var config = new ConfigFile();

        // Aktuelle Slider-Werte speichern
        if (_musicSlider != null)
            config.SetValue("audio", "music_volume", (float)_musicSlider.Value);
        if (_sfxSlider != null)
            config.SetValue("audio", "sfx_volume", (float)_sfxSlider.Value);
        if (_fullscreenToggle != null)
            config.SetValue("video", "fullscreen", _fullscreenToggle.ButtonPressed);

        config.Save(ConfigPath);
        GD.Print("[Settings] Saved to " + ConfigPath);
        QueueFree();
    }

    private void Decline()
    {
        // Alte Werte wiederherstellen
        if (_musicBus >= 0)
            AudioServer.SetBusVolumeDb(_musicBus, Mathf.LinearToDb(_savedMusicVolume));
        if (_sfxBus >= 0)
            AudioServer.SetBusVolumeDb(_sfxBus, Mathf.LinearToDb(_savedSfxVolume));
        SetFullscreen(_savedFullscreen);
        QueueFree();
    }

    public static void LoadConfig()
    {
        var config = new ConfigFile();
        if (config.Load(ConfigPath) != Error.Ok)
            return;

        // Music-Bus
        int musicBus = AudioServer.GetBusIndex("Music");
        if (musicBus >= 0 && config.HasSectionKey("audio", "music_volume"))
        {
            float vol = (float)config.GetValue("audio", "music_volume");
            AudioServer.SetBusVolumeDb(musicBus, Mathf.LinearToDb(vol));
        }

        // SFX-Bus (Fallback auf Master)
        int sfxBus = AudioServer.GetBusIndex("SFX");
        if (sfxBus >= 0 && config.HasSectionKey("audio", "sfx_volume"))
        {
            float vol = (float)config.GetValue("audio", "sfx_volume");
            AudioServer.SetBusVolumeDb(sfxBus, Mathf.LinearToDb(vol));
        }

        // Fullscreen
        if (config.HasSectionKey("video", "fullscreen"))
        {
            bool fs = (bool)config.GetValue("video", "fullscreen");
            SetFullscreen(fs);
        }

        GD.Print("[Settings] Config loaded");
    }


    // Borderless-Fullscreen als Workaround für Godot Embedded-Window-Limitierung.
    private static bool LoadFullscreenFromConfig()
    {
        var config = new ConfigFile();
        if (config.Load(ConfigPath) == Error.Ok && config.HasSectionKey("video", "fullscreen"))
            return (bool)config.GetValue("video", "fullscreen");
        return false;
    }

    private static void SetFullscreen(bool fullscreen)
    {
        DisplayServer.WindowSetMode(fullscreen
            ? DisplayServer.WindowMode.ExclusiveFullscreen
            : DisplayServer.WindowMode.Windowed);
    }
}


