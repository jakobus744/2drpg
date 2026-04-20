using Godot;

namespace RPG2d.GameManager;

public partial class GameManager : Node
{
    [Export] 
    private PackedScene _playerScene;

    private const int Port = 8910;

    public override void _Ready()
    {
        // Wenn ein ANDERER Spieler unserem Server beitritt, rufen wir AddPlayer auf
        Multiplayer.PeerConnected += AddPlayer;
        
        // Wenn ein Spieler unseren Server verlässt, löschen wir ihn
        Multiplayer.PeerDisconnected += RemovePlayer;
    }

    // Diese Funktion rufen wir auf, um den Server zu starten (Host)
    public void StartHost()
    {
        var peer = new ENetMultiplayerPeer();
        // Erstellt einen Server auf Port 8910 mit maximal 4 Spielern
        var error = peer.CreateServer(Port, 4); 
        
        if (error != Error.Ok)
        {
            GD.PrintErr("Server konnte nicht gestartet werden: " + error);
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        GD.Print("Server gestartet! Ich bin der Host.");

        // Wir müssen uns selbst (ID 1) auch in die Welt spawnen!
        AddPlayer(1);
    }

    // Diese Funktion rufen wir auf, um uns als Client zu verbinden
    public void JoinGame()
    {
        var peer = new ENetMultiplayerPeer();
        // Verbindet sich mit dem eigenen PC (localhost)
        peer.CreateClient("127.0.0.1", Port); 
        
        Multiplayer.MultiplayerPeer = peer;
        GD.Print("Verbinde als Client...");
    }

    private void AddPlayer(long id)
    {
        GD.Print($"Spawne Spieler mit ID: {id}");
        
        // 1. Player-Szene instanziieren
        Player.Player playerInstance = _playerScene.Instantiate<Player.Player>();
        playerInstance.Name = id.ToString();
        
        AddChild(playerInstance);
    }

    private void RemovePlayer(long id)
    {
        GD.Print($"Spieler {id} hat das Spiel verlassen.");
        var playerNode = GetNodeOrNull(id.ToString());
        playerNode?.QueueFree();
    }
}