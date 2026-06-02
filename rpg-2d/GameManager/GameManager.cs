using System.Collections.Generic;
using Godot;

namespace RPG2d.GameManager;

public partial class GameManager : Node
{
	[Export]
	private PackedScene _playerScene;
	[Export]
	private PackedScene _mainLevelScene;
	[Export]
	private PackedScene _hudScene;

	private const int Port = 8910;

	private readonly HashSet<string> _removedItemPaths = new();

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

		SpawnHud(newLevel);

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

		SpawnHud(newLevel);
	}

	private void AddPlayer(long id)
	{
		if (!Multiplayer.IsServer())
			return;

		GD.Print($"Spawne Spieler mit ID: {id}");

		var playerInstance = _playerScene.Instantiate<Player.Player>();
		playerInstance.Name = id.ToString();

		GetTree().CurrentScene.AddChild(playerInstance);

		// Sync bereits eingesammelte Items an den neuen Peer
		if (_removedItemPaths.Count > 0)
		{
			var paths = new string[_removedItemPaths.Count];
			_removedItemPaths.CopyTo(paths);
			RpcId(id, MethodName.SyncRemovedItems, paths);
		}
	}

	private void RemovePlayer(long id)
	{
		if (!Multiplayer.IsServer())
			return;

		GD.Print($"Spieler {id} hat das Spiel verlassen.");
		var playerNode = GetTree().CurrentScene.GetNodeOrNull(id.ToString());
		playerNode?.QueueFree();
	}

	private void SpawnHud(Node level)
	{
		var hudInstance = _hudScene.Instantiate();
		level.AddChild(hudInstance);
	}

	// Wird von PickupItem.RemoveItem aufgerufen wenn der Server das Item entfernt
	public void TrackRemovedItem(NodePath path)
	{
		_removedItemPaths.Add(path);
	}

	// Läuft auf dem neuen Client: entfernt alle bereits eingesammelten Items
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
	     TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SyncRemovedItems(string[] paths)
	{
		foreach (var path in paths)
		{
			var item = GetTree().CurrentScene.GetNodeOrNull(path);
			item?.QueueFree();
		}
	}
}
