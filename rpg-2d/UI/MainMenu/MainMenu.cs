using Godot;

namespace RPG2d.UI.MainMenu;

public partial class MainMenu : Control
{
	[Export]
	private Button _hostButton;
	
	[Export]
	private Button _joinButton;
	
	private GameManager.GameManager _gameManager;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_gameManager = GetNode<GameManager.GameManager>("/root/GameManager");
		_hostButton.Pressed += OnHostButtonPressed;
		_joinButton.Pressed += OnJoinButtonPressed;
	}

	public void OnHostButtonPressed()
	{
		_gameManager.StartHost();
		//StartGame();
	}
	
	public void OnJoinButtonPressed()
	{
		_gameManager.JoinGame();
		//StartGame();
	}
	
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
