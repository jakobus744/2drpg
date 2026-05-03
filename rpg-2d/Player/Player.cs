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
	private const float SprintFalloff = 30f;

	public enum MoveState { Idle, Walk, Run, Dead }

	private MoveState _moveState = MoveState.Idle;
	private MoveState _lastMoveState = MoveState.Idle;
	private float stamina = MaxStamina;

	private bool _isAttacking = false;
	private bool _isRolling = false;
	private bool _isHurt = false;
	private bool IsActionLocked => _isAttacking || _isRolling || _isHurt || _moveState == MoveState.Dead;

	private Node2D _weaponPivotNode;
	private AnimatedSprite2D _anim;
	private AnimationPlayer _weaponAnim;
	private Sprite2D _weaponPivot;
	private Sprite2D _shieldPivot;

	private PackedScene _currentWeaponScene;
	private PackedScene _currentOffhandScene;

	[Export] public Vector2 SyncPosition = Vector2.Zero;
	[Export] public string SyncAnimation = "";




	public override void _EnterTree()
	{
		if (int.TryParse(Name, out int peerId))
			SetMultiplayerAuthority(peerId);
		else
			SetMultiplayerAuthority(1);
	}

	public override void _Ready()
	{
		YSortEnabled = false;

		_anim = GetNode<AnimatedSprite2D>("Base Animation");
		_anim.AnimationFinished += OnAnimationFinished;

		GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D")?.Hide();

		_weaponAnim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

		_weaponPivotNode = GetNodeOrNull<Node2D>("WeaponPivot");
		_weaponPivot = GetNodeOrNull<Sprite2D>("WeaponPivot/WeaponSprite");
		_shieldPivot = GetNodeOrNull<Sprite2D>("OffhandPivot/OffhandSprite");
		if (_weaponPivot != null) _weaponPivot.Visible = false;
		if (_shieldPivot != null) _shieldPivot.Visible = false;

		var sync = GetNodeOrNull<MultiplayerSynchronizer>("ServerSynchronizer");
		sync?.SetMultiplayerAuthority(1);

		var camera = GetNodeOrNull<Camera2D>("Camera2D");
		if (camera != null)
			camera.Enabled = IsMultiplayerAuthority();
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
			_anim.Play(SyncAnimation);
	}

	public void Hurt(string dirName)
	{
		if (_moveState == MoveState.Dead) return;

		string animName = "hurt_" + dirName;
		if (!_anim.SpriteFrames.HasAnimation(animName)) return;

		_isHurt = true;
		_anim.Play(animName);
	}

	public void Die(string dirName)
	{
		if (_moveState == MoveState.Dead) return;
		_moveState = MoveState.Dead;
		Velocity = Vector2.Zero;

		string animName = "death_" + dirName;
		if (_anim.SpriteFrames.HasAnimation(animName))
			_anim.Play(animName);
	}

	public PlayerState ProcessCommand(PlayerState previousState, PlayerCmd cmd)
	{
		if (previousState != null)
			ApplyState(previousState);

		if (_moveState == MoveState.Dead)
			return new PlayerState { Position = Position, Velocity = Vector2.Zero, Stamina = stamina };

		if (cmd.IsAttackPressed && !IsActionLocked) StartAttack(cmd.FacingDirection);
		if (cmd.IsRollPressed && !IsActionLocked) StartRoll(cmd.FacingDirection);

		if (cmd.MovementVector == Vector2.Zero)
		{
			_lastMoveState = _moveState;
			_moveState = MoveState.Idle;
			Velocity = Vector2.Zero;
			stamina += StaminaRecovery;
		}
		else
		{
			_moveState = cmd.IsRunPressed ? MoveState.Run : MoveState.Walk;

			var wishSpeed = SpeedWalk;
			if (cmd.IsRunPressed && stamina > SprintCost)
			{
				wishSpeed = stamina <= SprintFalloff
					? SpeedWalk + (SpeedRun - SpeedWalk) * (stamina / SprintFalloff)
					: SpeedRun;
				stamina -= SprintCost;
			}
			else
			{
				stamina += MovingStaminaRecovery;
			}

			Velocity = cmd.MovementVector * wishSpeed;
		}

		stamina = Math.Clamp(stamina, 0f, MaxStamina);

		UpdateAnimation(cmd.FacingDirection);
		MoveAndSlide();

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
		PlayWeaponAnim(animName);
	}

	private void StartAttack(string dirName)
	{
		if (_weaponPivot == null || !_weaponPivot.Visible) return;
		string animName = GetAttackAnimationName(dirName);
		if (!_anim.SpriteFrames.HasAnimation(animName)) return;

		_isAttacking = true;
		_anim.Play(animName);
		PlayWeaponAnim(animName);
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
			case MoveState.Idle:
				PlayIdleAnimation(dirName);
				PlayWeaponAnim("idle_" + dirName);
				break;
			case MoveState.Walk:
				_anim.Play("walk_" + dirName);
				PlayWeaponAnim("walk_" + dirName);
				break;
			case MoveState.Run:
				_anim.Play("run_" + dirName);
				PlayWeaponAnim("run_" + dirName);
				break;
		}

		UpdateWeaponZIndex(dirName);
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

	private void OnAnimationFinished()
	{
		string name = _anim.Animation.ToString();
		if (name.Contains("attack")) _isAttacking = false;
		if (name.Contains("roll")) _isRolling = false;
		if (name.Contains("hurt")) _isHurt = false;
	}

	public void ApplyState(PlayerState state)
	{
		Position = state.Position;
		Velocity = state.Velocity;
		stamina = state.Stamina;
	}

	public void EquipWeapon(PackedScene droppedScene, Texture2D texture, Rect2 region,
		Vector2 scale, Vector2 offset, float rotation = 0f)
	{
		if (_currentWeaponScene != null)
			DropItem(_currentWeaponScene);

		_currentWeaponScene = droppedScene;

		if (_weaponPivot == null) return;
		_weaponPivot.Texture = texture;
		_weaponPivot.RegionEnabled = true;
		_weaponPivot.RegionRect = region;
		_weaponPivot.Rotation = Mathf.DegToRad(rotation);
		_weaponPivot.Scale = scale;
		_weaponPivot.Offset = offset;
		_weaponPivot.Visible = true;
	}

	public void EquipOffhand(PackedScene droppedScene, Texture2D texture, Rect2 region,
		Vector2 scale, Vector2 offset, float rotation = 0f)
	{
		if (_currentOffhandScene != null)
			DropItem(_currentOffhandScene);

		_currentOffhandScene = droppedScene;

		if (_shieldPivot == null) return;
		_shieldPivot.Texture = texture;
		_shieldPivot.RegionEnabled = true;
		_shieldPivot.RegionRect = region;
		_shieldPivot.Scale = scale;
		_shieldPivot.Offset = offset;
		_shieldPivot.Visible = true;
	}

	private void DropItem(PackedScene scene)
	{
		if (scene == null) return;
		var instance = scene.Instantiate<Node2D>();
		instance.Scale = Vector2.One;
		instance.Position = GlobalPosition;
		GetParent().AddChild(instance);
	}

	private void UpdateWeaponZIndex(string dirName)
	{
		if (_weaponPivotNode == null) return;

		bool inFront = _moveState == MoveState.Idle && dirName == "down" || dirName == "right";
		bool currentlyInFront = _weaponPivotNode.GetIndex() > _anim.GetIndex();

		if (inFront == currentlyInFront) return;

		_weaponPivotNode.GetParent().MoveChild(_weaponPivotNode, _anim.GetIndex());
		_weaponPivotNode.ZIndex = 0;
	}

	private void PlayWeaponAnim(string animName)
	{
		if (_weaponAnim == null) return;
		if (_weaponAnim.CurrentAnimation == animName) return;
		if (_weaponAnim.HasAnimation(animName))
			_weaponAnim.Play(animName);
	}
}
