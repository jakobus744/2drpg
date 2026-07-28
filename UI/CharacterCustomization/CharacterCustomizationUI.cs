using System;
using Godot;
using RPG2d.Player;
using PlayerNode = RPG2d.Player.Player;

namespace RPG2d.UI.CharacterCustomization;

public partial class CharacterCustomizationUI : Control
{
    [Export] private OptionButton _hairOptions;
    [Export] private OptionButton _eyeOptions;
    [Export] private OptionButton _faceOptions;
    [Export] private Button _saveButton;
    [Export] private Button _resetButton;
    [Export] private Button _closeButton;
    [Export] private Label _statusLabel;

    private const string ConfigPath = "user://character_appearance.cfg";
    private PlayerNode _player;

    public override void _Ready()
    {
        Visible = false;
        Populate(_hairOptions, PlayerNode.HairStyles);
        Populate(_eyeOptions, PlayerNode.EyeStyles);
        Populate(_faceOptions, PlayerNode.FaceStyles);

        _hairOptions.ItemSelected += _ => ApplySelection();
        _eyeOptions.ItemSelected += _ => ApplySelection();
        _faceOptions.ItemSelected += _ => ApplySelection();
        _saveButton.Pressed += SaveSelection;
        _resetButton.Pressed += ResetSelection;
        _closeButton.Pressed += () => SetOpen(false);
    }

    public override void _Process(double delta)
    {
        if (_player != null && GodotObject.IsInstanceValid(_player)) return;

        _player = PlayerNode.LocalPlayer;
        if (_player == null) return;

        LoadSelection();
        ApplySelection();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Echo: true }) return;

        if (@event.IsActionPressed("character_customization"))
        {
            SetOpen(!Visible);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            SetOpen(false);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        PlayerInput.GameplayInputBlocked = false;
    }

    private void SetOpen(bool open)
    {
        Visible = open;
        PlayerInput.GameplayInputBlocked = open;
        if (open)
        {
            MoveToFront();
            _hairOptions.GrabFocus();
            SetStatus("Auswahl wird direkt am Charakter angezeigt.");
        }
        else
        {
            GetViewport().GuiReleaseFocus();
        }
    }

    private static void Populate(OptionButton options, string[] values)
    {
        options.Clear();
        foreach (string value in values)
        {
            int index = options.ItemCount;
            options.AddItem(DisplayName(value));
            options.SetItemMetadata(index, value);
        }
    }

    private static string DisplayName(string value) => value switch
    {
        "Standard" => "Standard (Original)",
        "Gruen" => "Grün",
        "Tuerkis" => "Türkis",
        "Gebraeunt" => "Gebräunt",
        "Sehr_Dunkel" => "Sehr dunkel",
        _ => value
    };

    private void ApplySelection()
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player)) return;

        _player.SetAppearanceFromUi(
            SelectedValue(_hairOptions),
            SelectedValue(_eyeOptions),
            SelectedValue(_faceOptions));
        SetStatus("Vorschau aktiv – mit „Speichern“ dauerhaft übernehmen.");
    }

    private void SaveSelection()
    {
        ApplySelection();

        var config = new ConfigFile();
        string section = AppearanceSection();
        config.Load(ConfigPath);
        config.SetValue(section, "hair", SelectedValue(_hairOptions));
        config.SetValue(section, "eyes", SelectedValue(_eyeOptions));
        config.SetValue(section, "face", SelectedValue(_faceOptions));

        Error error = config.Save(ConfigPath);
        SetStatus(error == Error.Ok
            ? "Charakteraussehen gespeichert."
            : $"Speichern fehlgeschlagen: {error}");
    }

    private void LoadSelection()
    {
        string hair = PlayerNode.DefaultHairStyle;
        string eyes = PlayerNode.DefaultEyeStyle;
        string face = PlayerNode.DefaultFaceStyle;

        var config = new ConfigFile();
        if (config.Load(ConfigPath) == Error.Ok)
        {
            string section = AppearanceSection();
            hair = (string)config.GetValue(section, "hair", hair);
            eyes = (string)config.GetValue(section, "eyes", eyes);
            face = (string)config.GetValue(section, "face", face);
        }

        SelectValue(_hairOptions, hair, PlayerNode.DefaultHairStyle);
        SelectValue(_eyeOptions, eyes, PlayerNode.DefaultEyeStyle);
        SelectValue(_faceOptions, face, PlayerNode.DefaultFaceStyle);
    }

    private void ResetSelection()
    {
        SelectValue(_hairOptions, PlayerNode.DefaultHairStyle, PlayerNode.DefaultHairStyle);
        SelectValue(_eyeOptions, PlayerNode.DefaultEyeStyle, PlayerNode.DefaultEyeStyle);
        SelectValue(_faceOptions, PlayerNode.DefaultFaceStyle, PlayerNode.DefaultFaceStyle);
        ApplySelection();
        SetStatus("Standardaussehen als Vorschau gesetzt.");
    }

    private static string SelectedValue(OptionButton options)
    {
        if (options.Selected < 0) return "";
        return options.GetItemMetadata(options.Selected).AsString();
    }

    private static void SelectValue(OptionButton options, string value, string fallback)
    {
        int fallbackIndex = 0;
        for (int i = 0; i < options.ItemCount; i++)
        {
            string candidate = options.GetItemMetadata(i).AsString();
            if (candidate == fallback) fallbackIndex = i;
            if (candidate != value) continue;
            options.Select(i);
            return;
        }

        options.Select(fallbackIndex);
    }

    private void SetStatus(string text)
    {
        if (_statusLabel != null) _statusLabel.Text = text;
    }

    private string AppearanceSection()
    {
        if (!Multiplayer.HasMultiplayerPeer()) return "appearance_local";
        return Multiplayer.IsServer()
            ? "appearance_host"
            : $"appearance_client_{Multiplayer.GetUniqueId()}";
    }
}
