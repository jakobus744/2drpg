using Godot;

namespace RPG2d.World;

/// <summary>
/// Globaler Hintergrundmusik-Player. Als Autoload eingehaengt, damit die Musik
/// genau einmal existiert. Vorher hatte jede Zone ihren eigenen Player mit
/// autoplay - beim Nachladen der Nachbarzonen liefen dann mehrere Kopien
/// desselben Stuecks zeitversetzt und haben sich gegenseitig ausgeloescht.
/// </summary>
public partial class MusicPlayer : Node
{
    public static MusicPlayer Instance { get; private set; }

    [Export] public AudioStream Track { get; set; }
    [Export] public float VolumeDb { get; set; } = -6f;
    [Export] public string Bus { get; set; } = "Music";

    private AudioStreamPlayer _player;

    public override void _Ready()
    {
        Instance = this;

        _player = new AudioStreamPlayer
        {
            Name = "Stream",
            VolumeDb = VolumeDb,
            Bus = Bus
        };
        AddChild(_player);
        _player.Finished += OnFinished;

        if (Track == null)
        {
            Track = GD.Load<AudioStream>("res://Assets/Music/freesound_community-clouds-29191.mp3");
        }

        Play(Track);
    }

    public void Play(AudioStream stream)
    {
        if (stream == null || _player == null) return;
        if (_player.Stream == stream && _player.Playing) return;

        _player.Stream = stream;
        _player.Play();
    }

    public void Stop() => _player?.Stop();

    // MP3 kennt keine verlaessliche Loop-Markierung, deshalb von Hand neu starten.
    private void OnFinished() => _player?.Play();

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }
}
