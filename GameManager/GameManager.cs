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

	private static readonly HashSet<string> _removedItemScenes = new();

	public static uint ServerTick { get; private set; }

	public override void _Ready()
	{
		Multiplayer.PeerConnected += AddPlayer;
		Multiplayer.PeerDisconnected += RemovePlayer;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Multiplayer.IsServer())
			ServerTick++;
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
		playerInstance.Position = RPG2d.World.WorldManager.GetZonePosition(new Vector2I(1, 1));

		GetTree().CurrentScene.AddChild(playerInstance);

		if (_removedItemScenes.Count > 0)
		{
			var paths = new string[_removedItemScenes.Count];
			_removedItemScenes.CopyTo(paths);
			RpcId(id, MethodName.SyncRemovedItems, paths);
		}
	}

	private void RemovePlayer(long id)
	{
		if (!Multiplayer.IsServer())
			return;

		GD.Print($"Spieler {id} hat das Spiel verlassen.");
		World.WorldManager.ClearPeerZones(id);
		var playerNode = GetTree().CurrentScene.GetNodeOrNull(id.ToString());
		playerNode?.QueueFree();
	}

	private void SpawnHud(Node level)
	{
		var hudInstance = _hudScene.Instantiate();
		level.AddChild(hudInstance);
	}

	public void TrackRemovedItem(string scenePath)
	{
		_removedItemScenes.Add(scenePath);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SyncRemovedItems(string[] paths)
	{
		foreach (var path in paths)
			FreeAllByScene(path);
	}

	private static void FreeAllByScene(string scenePath)
	{
		if (string.IsNullOrEmpty(scenePath)) return;
		var root = ((SceneTree)Engine.GetMainLoop()).CurrentScene;
		FreeAllRecursive(root, scenePath);
	}

	private static void FreeAllRecursive(Node node, string scenePath)
	{
		if (node is World.Items.PickupItem pi && pi.SceneFilePath == scenePath)
		{
			pi.QueueFree();
		}
		foreach (var child in node.GetChildren())
			FreeAllRecursive(child, scenePath);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RemoveItemByScene(string scenePath, Vector2 atPosition)
	{
		FreeItemByScene(scenePath, atPosition);
	}


	private static void FreeItemByScene(string scenePath, Vector2 atPosition)
	{
		if (string.IsNullOrEmpty(scenePath)) return;
		var root = ((SceneTree)Engine.GetMainLoop()).CurrentScene;
		float bestDist = float.MaxValue;
		World.Items.PickupItem best = null;
		FindClosestPickup(root, scenePath, atPosition, ref bestDist, ref best);
		if (best != null)
		{
			best.Visible = false;
			best.GetParent()?.RemoveChild(best);
			best.QueueFree();
		}
	}

	private static void FindClosestPickup(Node node, string scenePath, Vector2 pos, ref float bestDist, ref World.Items.PickupItem best)
	{
		if (node is World.Items.PickupItem pi && pi.SceneFilePath == scenePath)
		{
			float d = pi.GlobalPosition.DistanceSquaredTo(pos);
			if (d < bestDist) { bestDist = d; best = pi; }
		}
		foreach (var child in node.GetChildren())
			FindClosestPickup(child, scenePath, pos, ref bestDist, ref best);
	}
}
