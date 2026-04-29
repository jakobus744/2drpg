using System;
using Godot;

namespace RPG2d.Player;

public partial class Player : CharacterBody2D
{
	private const float SpeedWalk = 70f;
	private const float SpeedRun = 100f;
	private const float MaxStamina = 100f;
	private const float StaminaRecovery = .5f;
	private const float MovingStaminaRecovery = .25f;
	private const float RollCost = 25f;
	private const float SprintCost = .25f;
	
	// Ab wann Sprint Speed weniger wird, evtl renamen?
	private const float SprintFalloff = 30f;

	public enum MoveState { Idle, Walk, Run }
	
	private MoveState _moveState = MoveState.Idle;
	private MoveState _lastMoveState = MoveState.Idle;
	private Vector2 _facingDirection = Vector2.Down;
	private bool _isAttacking = false;
	private bool _isRolling = false;
	
	private bool IsActionLocked => _isAttacking || _isRolling;

	private AnimatedSprite2D _anim;
	
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
	public PlayerState ProcessCommand(PlayerState from, PlayerCmd cmd)
	{
		// Initial state setzen
		from ??= new PlayerState
		{
			Position = Position,
			Velocity = Velocity,
			Stamina = MaxStamina
		};
		
		var stamina = from.Stamina;
		
		// 1. Aktionen verarbeiten
		if (cmd.IsAttackPressed && !IsActionLocked) StartAttack(cmd.FacingDirection);
		if (cmd.IsRollPressed && !IsActionLocked)
		{
			if (stamina >= RollCost)
			{
				StartRoll(cmd.FacingDirection);
				stamina = Math.Max(stamina - RollCost, 0f);
			}
		}
		
		// 2. Bewegung verarbeiten
		if (cmd.MovementVector == Vector2.Zero)
		{
			_lastMoveState = _moveState;
			_moveState = MoveState.Idle;
			Velocity = Vector2.Zero;
			stamina += StaminaRecovery;
		}
		else
		{
			_facingDirection = DirectionStringToVector(cmd.FacingDirection);
			_moveState = cmd.IsRunPressed ? MoveState.Run : MoveState.Walk;

			var wishSpeed = SpeedWalk;
			if (cmd.IsRunPressed && stamina > SprintCost)
			{
				// Speed increase ist stamina basiert
				if (stamina <= SprintFalloff)
				{
					wishSpeed += (SpeedRun - SpeedWalk) * (stamina / SprintFalloff);
				}
				else
				{
					wishSpeed = SpeedRun;
				}

				stamina -= SprintCost;
			}
			else
			{
				stamina += MovingStaminaRecovery;
			}
			
			Velocity = cmd.MovementVector * wishSpeed;
		}
		
		
		stamina = Math.Clamp(stamina, 0f, MaxStamina);

		// 3. Animation updaten und bewegen
		UpdateAnimation(cmd.FacingDirection);
		MoveAndSlide();

		// 4. State zurück geben
		return new PlayerState
		{
			Position = Position,
			Velocity = Velocity,
			Stamina = stamina
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

		switch (_moveState)
		{
			case MoveState.Idle: PlayIdleAnimation(dirName); break;
			case MoveState.Walk: _anim.Play("walk_" + dirName); break;
			case MoveState.Run: _anim.Play("run_" + dirName); break;
		}
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
}
