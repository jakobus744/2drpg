using Godot;

namespace RPG2d.Player;

public partial class Player : CharacterBody2D
{
	private const float SpeedWalk = 70f;
	private const float SpeedRun = 100f;

	public enum MoveState { Idle, Walk, Run }

	private MoveState _moveState = MoveState.Idle;
	private MoveState _lastMoveState = MoveState.Idle;
	private Vector2 _facingDirection = Vector2.Down;
	private bool _isAttacking = false;
	private bool _isRolling = false;

	private bool IsActionLocked => _isAttacking || _isRolling;

	private AnimatedSprite2D _anim;
	private AnimationPlayer _weaponAnim;
	private Sprite2D _weaponPivot;
	private bool _hasWeapon = false;

	[Export] public Vector2 SyncPosition = Vector2.Zero;
	[Export] public string SyncAnimation = "";

	public override void _EnterTree()
	{
		// Sollte immer der Fall sein, außer wir testen etwas spezifisches wo wir die Node manuell hinzufügen
		if (int.TryParse(Name, out int peerId))
		{
			SetMultiplayerAuthority(peerId);
		}
		else
		{
			SetMultiplayerAuthority(1);
		}
	}

	public override void _Ready()
	{
		YSortEnabled = false;
		_anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_anim.AnimationFinished += OnAnimationFinished;

		_weaponAnim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		_weaponPivot = GetNodeOrNull<Sprite2D>("WeaponPivot");
		if (_weaponPivot != null) _weaponPivot.Visible = false;

		_anim.FrameChanged += SyncWeaponAnim;
		_anim.AnimationChanged += () => SyncWeaponAnim();

		var sync = GetNodeOrNull<MultiplayerSynchronizer>("ServerSynchronizer");
		sync?.SetMultiplayerAuthority(1);

		var camera = GetNodeOrNull<Camera2D>("Camera2D");
		if (camera != null)
		{
			camera.Enabled = IsMultiplayerAuthority();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Multiplayer.IsServer())
		{
			SyncPosition = Position;
			SyncAnimation = _anim.Animation;
		}

		if (IsMultiplayerAuthority()) return;

		Position = Position.Lerp(SyncPosition, (float)delta * 15f);

		if (!string.IsNullOrEmpty(SyncAnimation))
		{
			_anim.Play(SyncAnimation);
		}
	}

	// Player Input verarbeiten, sowohl auf dem Server als auch auf dem Client
	public PlayerState ProcessCommand(PlayerCmd cmd)
	{
		// 1. Aktionen verarbeiten
		if (cmd.IsAttackPressed && !IsActionLocked) StartAttack(cmd.FacingDirection);
		if (cmd.IsRollPressed && !IsActionLocked) StartRoll(cmd.FacingDirection);

		// 2. Bewegung verarbeiten
		if (cmd.MovementVector == Vector2.Zero)
		{
			_lastMoveState = _moveState;
			_moveState = MoveState.Idle;
			Velocity = Vector2.Zero;
		}
		else
		{
			_facingDirection = DirectionStringToVector(cmd.FacingDirection);
			_moveState = cmd.IsRunPressed ? MoveState.Run : MoveState.Walk;
			Velocity = cmd.MovementVector * (cmd.IsRunPressed ? SpeedRun : SpeedWalk);
		}

		// 3. Animation updaten und bewegen
		UpdateAnimation(cmd.FacingDirection);
		MoveAndSlide();

		// 4. State zurück geben
		return new PlayerState
		{
			Position = Position,
			Velocity = Velocity
		};
	}

	private void StartRoll(string dirName)
	{
		string animName = "roll_" + dirName;
		if (!_anim.SpriteFrames.HasAnimation(animName)) return;

		_isRolling = true;
		_anim.Play(animName);
	}

	private void StartAttack(string dirName)
	{
		string animName = GetAttackAnimationName(dirName);
		if (!_anim.SpriteFrames.HasAnimation(animName)) return;

		_isAttacking = true;
		_anim.Play(animName);
	}

	private string GetAttackAnimationName(string dir) => _moveState switch
	{
		MoveState.Run => "run_attack_" + dir,
		MoveState.Walk => "walk_attack_" + dir,
		_ => "attack_" + dir,
	};

	private void UpdateAnimation(string dirName)
	{
		if (IsActionLocked) return;

		UpdateWeaponZIndex(dirName);

		switch (_moveState)
		{
			case MoveState.Idle: PlayIdleAnimation(dirName); break;
			case MoveState.Walk: _anim.Play("walk_" + dirName); break;
			case MoveState.Run: _anim.Play("run_" + dirName); break;
		}
	}

	private void UpdateWeaponZIndex(string dir)
	{
		if (_weaponPivot == null) return;
		_weaponPivot.ZIndex = (dir == "up" || dir == "left") ? -1 : 1;
	}

	private void PlayIdleAnimation(string dir)
	{
		string anim = _lastMoveState == MoveState.Run
			? "idle_" + dir + ".2"
			: "idle_" + dir + ".1";

		if (!_anim.SpriteFrames.HasAnimation(anim))
			anim = "idle_" + dir;

		_anim.Play(anim);
	}

	private Vector2 DirectionStringToVector(string dir) => dir switch
	{
		"up" => Vector2.Up,
		"down" => Vector2.Down,
		"left" => Vector2.Left,
		"right" => Vector2.Right,
		_ => Vector2.Zero,
	};

	private void OnAnimationFinished()
	{
		string finishedAnimation = _anim.Animation.ToString();

		if (finishedAnimation.Contains("attack")) _isAttacking = false;
		if (finishedAnimation.Contains("roll")) _isRolling = false;
	}

	public void ApplyState(PlayerState state)
	{
		Position = state.Position;
		Velocity = state.Velocity;
	}

	public void EquipWeapon(Texture2D texture, Rect2 region)
	{
		_hasWeapon = true;
		if (_weaponPivot == null) return;
		_weaponPivot.Texture = texture;
		_weaponPivot.RegionEnabled = true;
		_weaponPivot.RegionRect = region;
		_weaponPivot.Visible = true;
	}

	private void SyncWeaponAnim()
	{
		if (!_hasWeapon || _weaponAnim == null) return;
		string animName = _anim.Animation;
		if (_weaponAnim.HasAnimation(animName))
		{
			double fps = _anim.SpriteFrames.GetAnimationSpeed(animName);
			double time = _anim.Frame / fps;
			_weaponAnim.Play(animName);
			_weaponAnim.Seek(time, true);
		}
	}
}
