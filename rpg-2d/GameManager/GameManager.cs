using Godot;

namespace RPG2d.GameManager;

public partial class GameManager : Node
{
	[Export] 
	private PackedScene _playerScene;
	[Export]
	private PackedScene _mainLevelScene;

	private const int Port = 8910;

	public override void _Ready()
	{
		Multiplayer.PeerConnected += AddPlayer;
		Multiplayer.PeerDisconnected += RemovePlayer;
	}

	public void StartHost()
	{
		var peer = new ENetMultiplayerPeer();
		var error = peer.CreateServer(Port, 4); 
		
		if (error != Error.Ok)
		{
			GD.PrintErr("Server konnte nicht gestartet werden: " + error);
			return;
		}
		
		GD.Print("Server wurde gestartet!");
		
		Multiplayer.MultiplayerPeer = peer;
		
		GetTree().CurrentScene.QueueFree();
		Node newLevel = _mainLevelScene.Instantiate();
		GetTree().Root.AddChild(newLevel);
		GetTree().CurrentScene = newLevel;
		
		AddPlayer(1);
	}

	public void JoinGame()
	{
		var peer = new ENetMultiplayerPeer();
		peer.CreateClient("127.0.0.1", Port); 
		
		Multiplayer.MultiplayerPeer = peer;
		GD.Print("Verbinde als Client...");
		
		GetTree().CurrentScene.QueueFree();
		Node newLevel = _mainLevelScene.Instantiate();
		GetTree().Root.AddChild(newLevel);
		GetTree().CurrentScene = newLevel;
	}

	private void AddPlayer(long id)
	{	
		if (!Multiplayer.IsServer())
			return;

		GD.Print($"Spawne Spieler mit ID: {id}");
		
		var playerInstance = _playerScene.Instantiate<Player.Player>();
		playerInstance.Name = id.ToString();
		
		var playersContainer = GetTree().CurrentScene.GetNodeOrNull("Players");
		
		if (playersContainer != null)
		{

			playersContainer.AddChild(playerInstance);
		}
		else
		{
			GD.PrintErr("Konnte Players-Container nicht finden. Stelle sicher, dass die aktuelle Szene einen Node namens 'Players' hat.");
		}
	}

	private void RemovePlayer(long id)
	{
		if (!Multiplayer.IsServer())
			return;
		
		GD.Print($"Spieler {id} hat das Spiel verlassen.");
		var playersContainer = GetTree().CurrentScene.GetNodeOrNull("Players");
		var playerNode = playersContainer?.GetNodeOrNull(id.ToString());
		playerNode?.QueueFree();
	}
}
