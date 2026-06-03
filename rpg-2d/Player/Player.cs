using System;
using System.Collections.Generic;
using Godot;
using RPG2d.Entity;
using RPG2d.World.Items;

namespace RPG2d.Player;

public partial class Player : BaseEntity<PlayerState>
{
	private const float SpeedWalk = 70f;
	private const float SpeedRun = 100f;

	private const float MaxHealth = 100f;
	private const float HealthRecovery = 1f;

	private const float MaxStamina = 100f;
	private const float StaminaRecovery = .80f;
	private const float MovingStaminaRecovery = .25f;
	private const float RollCost = 5f;
	private const float SprintCost = .2f;
	private const float SprintFalloff = 35f;

	private const float stoprun = 40f;

	public enum MoveState
	{
		Idle,
		Walk,
		Run,
		Dead
	}

	private MoveState _moveState = MoveState.Idle;

	private MoveState _lastMoveState = MoveState.Idle;
	private Vector2 _facingDirection = Vector2.Down;
	private string _facingDirectionName = "down";

	private bool _isAttacking = false;
	private bool _isRolling = false;
	private bool _isHurt = false;
	private bool IsActionLocked => _isAttacking || _isRolling || _isHurt || _moveState == MoveState.Dead;

	private Node2D _weaponPivotNode;
	private Node2D _offHandPivotNode;

	private AnimatedSprite2D _anim;
	private AnimationPlayer _weaponAnim;
	private Sprite2D _weaponPivot;
	private Sprite2D _shieldPivot;

	private PackedScene _currentOffhandScene;
	private WeaponItem _currentWeapon;

	private PickupItem _nearbyPickup;

	private struct PendingHit
	{
		public Player Target;
		public float Damage;
		public string Direction;
	}
	private readonly List<PendingHit> _pendingHits = new();
	private readonly HashSet<Node2D> _hitBodies = new();
	private float _pendingDamage;
	private string _pendingDamageDir = "";

	private string _lastSyncWeaponPath = "";
	private string _lastSyncOffhandPath = "";

	[Export] public Vector2 SyncPosition = Vector2.Zero;
	[Export] public string SyncAnimation = "";
	[Export] public string SyncWeaponPath = "";
	[Export] public string SyncOffhandPath = "";

	public static Player LocalPlayer { get; private set; }
	public PlayerInput Input { get; private set; }

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

		StateBuffer.Set(0, new PlayerState
		{
			Health = MaxHealth,
			Stamina = MaxStamina,
			Position = Position
		});

		_anim = GetNode<AnimatedSprite2D>("Base Animation");
		_anim.AnimationFinished += OnAnimationFinished;

		GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D")?.Hide();

		_weaponAnim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

		_weaponPivotNode = GetNodeOrNull<Node2D>("WeaponPivot");
		_offHandPivotNode = GetNodeOrNull<Node2D>("OffHandPivot");
		_weaponPivot = GetNodeOrNull<Sprite2D>("WeaponPivot/WeaponSprite");
		_shieldPivot = GetNodeOrNull<Sprite2D>("OffHandPivot/OffHandSprite");
		if (_weaponPivot != null) _weaponPivot.Visible = false;
		if (_shieldPivot != null) _shieldPivot.Visible = false;

		var sync = GetNodeOrNull<MultiplayerSynchronizer>("ServerSynchronizer");
		sync?.SetMultiplayerAuthority(1);

		var camera = GetNodeOrNull<Camera2D>("Camera2D");
		if (camera != null)
			camera.Enabled = IsMultiplayerAuthority();

		Input = GetNodeOrNull<PlayerInput>("Input");
		if (IsMultiplayerAuthority())
			LocalPlayer = this;
	}

	public override void _ExitTree()
	{
		if (LocalPlayer == this)
			LocalPlayer = null;
	}

	public void RegisterNearbyPickup(PickupItem item) => _nearbyPickup = item;
	public void UnregisterNearbyPickup(PickupItem item)
	{
		if (_nearbyPickup == item) _nearbyPickup = null;
	}

	public string NearbyPickupPath => _nearbyPickup?.SceneFilePath ?? "";

	public void QueueDamage(float amount, string direction)
	{
		_pendingDamage += amount;
		_pendingDamageDir = direction;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Multiplayer.IsServer())
		{
			SyncPosition = Position;
			SyncAnimation = _anim.Animation;
			SyncWeaponPath = _currentWeapon?.SceneFilePath ?? "";
			SyncOffhandPath = _currentOffhandScene?.ResourcePath ?? "";
		}

		if (IsMultiplayerAuthority()) return;

		Position = Position.Lerp(SyncPosition, (float)delta * 15f);

		if (!string.IsNullOrEmpty(SyncAnimation))
		{
			_anim.Play(SyncAnimation);
			PlayWeaponAnim(SyncAnimation);
		}

		if (SyncWeaponPath != _lastSyncWeaponPath)
		{
			if (!string.IsNullOrEmpty(SyncWeaponPath))
				ApplyWeaponAttachment(SyncWeaponPath);
			else
				HideWeaponVisual();
			_lastSyncWeaponPath = SyncWeaponPath;
		}

		if (SyncOffhandPath != _lastSyncOffhandPath)
		{
			if (!string.IsNullOrEmpty(SyncOffhandPath))
				ApplyOffhandVisual(SyncOffhandPath);
			else
				HideOffhandVisual();
			_lastSyncOffhandPath = SyncOffhandPath;
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void Hurt(string dirName)
	{
		if (_moveState == MoveState.Dead) return;

		string animName = "hurt_" + dirName;
		CancelAttack();
		if (!_anim.SpriteFrames.HasAnimation(animName)) return;

		_isHurt = true;
		_anim.Play(animName);
	}

	public void Die(string dirName)
	{
		if (_moveState == MoveState.Dead) return;
		_moveState = MoveState.Dead;
		Velocity = Vector2.Zero;
		CancelAttack();

		string animName = "death_" + dirName;
		if (_anim.SpriteFrames.HasAnimation(animName))
			_anim.Play(animName);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void DieRpc(string dirName)
	{
		Die(dirName);
	}

	public void ProcessCommand(PlayerCmd cmd)
	{
		var state = StateBuffer.Get(cmd.Tick - 1);
		CurrentTick = cmd.Tick;

		if (_pendingDamage > 0)
		{
			state.Health -= _pendingDamage;
			state.LastHurtTick = cmd.Tick;
			if (!_isHurt)
			{
				CancelAttack();
				string hurtAnim = "hurt_" + _pendingDamageDir;
				if (_anim.SpriteFrames.HasAnimation(hurtAnim))
				{
					_isHurt = true;
					_anim.Play(hurtAnim);
				}
			}
			string hurtDir = _pendingDamageDir;
			_pendingDamage = 0f;
			_pendingDamageDir = "";
			if (Multiplayer.IsServer())
				Rpc("Hurt", hurtDir);
		}

		// 0b. Equip verarbeiten
		if (cmd.IsInteractPressed && _nearbyPickup != null)
		{
			var pickup = _nearbyPickup;
			_nearbyPickup = null;

			if (pickup is WeaponItem weapon)
			{
				if (_currentWeapon != null && IsMultiplayerAuthority())
					DropItem(GD.Load<PackedScene>(_currentWeapon.SceneFilePath));

				ApplyWeaponAttachment(weapon.SceneFilePath);
				state.EquippedWeaponPath = weapon.SceneFilePath;
			}
			else if (pickup is OffhandItem)
			{
				if (_currentOffhandScene != null && IsMultiplayerAuthority())
					DropItem(_currentOffhandScene);

				_currentOffhandScene = GD.Load<PackedScene>(pickup.SceneFilePath);
				ApplyOffhandVisual(pickup.SceneFilePath);
				state.EquippedOffhandPath = pickup.SceneFilePath;
			}

			// Nur der Server entfernt das Item authoritativ
			if (Multiplayer.HasMultiplayerPeer())
			{
				if (Multiplayer.IsServer())
					pickup.Rpc("RemoveItem");
			}
			else
			{
				pickup.QueueFree();
			}
		}

	foreach (var hit in _pendingHits)
			hit.Target.QueueDamage(hit.Damage, hit.Direction);
		_pendingHits.Clear();

		if (_moveState == MoveState.Dead)
		{
			state.Velocity = Vector2.Zero;
			StateBuffer.Set(cmd.Tick, state);
			return;
		}

		if (cmd.IsAttackPressed && !IsActionLocked && _currentWeapon != null)
		{
			float attackCost = GetAttackStaminaCost();
			if (cmd.Tick >= state.NextAttackTick && state.Stamina >= attackCost)
			{
				StartAttack(cmd.FacingDirection);
				state.NextAttackTick += _currentWeapon.Stats.AttackCooldownTicks;
				state.Stamina -= attackCost;
			}
		}

		if (cmd.IsRollPressed && !_isRolling && !_isHurt && _moveState != MoveState.Dead)
		{
			if (state.Stamina >= RollCost)
			{
				StartRoll(cmd.FacingDirection);
				state.Stamina = Math.Max(state.Stamina - RollCost, 0f);
			}
		}

	if (cmd.MovementVector == Vector2.Zero)
		{
			_lastMoveState = _moveState;
			_moveState = MoveState.Idle;
			Velocity = Vector2.Zero;
			state.Stamina += StaminaRecovery;
		}
		else
		{
			_facingDirection = DirectionStringToVector(cmd.FacingDirection);
			_facingDirectionName = cmd.FacingDirection;
			if (cmd.IsRunPressed && !state.IsExhausted && state.Stamina > SprintCost)
			{
				_moveState = MoveState.Run;
			}
			else
			{
				_moveState = MoveState.Walk;
			}

			var wishSpeed = SpeedWalk;
			if (_moveState == MoveState.Run)
			{
				wishSpeed = state.Stamina <= SprintFalloff
					? SpeedWalk + (SpeedRun - SpeedWalk) * (state.Stamina / SprintFalloff)
					: SpeedRun;
				state.Stamina -= SprintCost;

				if (state.Stamina <= SprintCost)
					state.IsExhausted = true;
			}
			else
			{
				state.Stamina += MovingStaminaRecovery;
			}

			Velocity = cmd.MovementVector * wishSpeed;
		}

		if (state.Health <= 0)
			Die(cmd.FacingDirection);
		else
			state.Health += HealthRecovery;

		state.Stamina = Math.Clamp(state.Stamina, 0f, MaxStamina);
		state.Health = Math.Clamp(state.Health, 0f, MaxHealth);

		if (state.IsExhausted && state.Stamina >= stoprun)
			state.IsExhausted = false;

	UpdateAnimation(cmd.FacingDirection);
		MoveAndSlide();

		state.Position = Position;
		state.Velocity = Velocity;

		StateBuffer.Set(cmd.Tick, state);
	}

	private void CancelAttack()
	{
		if (!_isAttacking) return;
		_isAttacking = false;
		_hitBodies.Clear();
		if (_currentWeapon?.Hitbox != null)
			_currentWeapon.Hitbox.SetDeferred("monitoring", false);
	}

	private void StartRoll(string dirName)
	{
		string animName = "roll_" + dirName;
		if (!_anim.SpriteFrames.HasAnimation(animName)) return;
		CancelAttack();
		_isHurt = false;

		_isRolling = true;
		_anim.Play(animName);
		PlayWeaponAnim(animName);
	}

	private void StartAttack(string dirName)
	{
		if (_currentWeapon == null) return;
		string animName = GetAttackAnimationName(dirName);
		if (!_anim.SpriteFrames.HasAnimation(animName)) return;

		_isAttacking = true;
		if (_currentWeapon.Hitbox != null)
			_currentWeapon.Hitbox.SetDeferred("monitoring", true);

		_anim.Play(animName);
		PlayWeaponAnim(animName);
	}

	private float GetAttackStaminaCost()
	{
		var stats = _currentWeapon.Stats;
		return _moveState switch
		{
			MoveState.Run => stats.AttackStaminaCost * stats.RunAttackMultiplier,
			MoveState.Walk => stats.AttackStaminaCost * stats.WalkAttackMultiplier,
			_ => stats.AttackStaminaCost,
		};
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
		var name = _anim.Animation.ToString();

		if (name.Contains("attack"))
		{
			_isAttacking = false;

			_hitBodies.Clear();
			if (_currentWeapon != null && _currentWeapon.Hitbox != null)
				_currentWeapon.Hitbox.SetDeferred("monitoring", false);
		}

		if (name.Contains("roll")) _isRolling = false;
		if (name.Contains("hurt")) _isHurt = false;
	}

	public override void ApplyServerState(uint tick, PlayerState serverState)
	{
		base.ApplyServerState(tick, serverState);
		Position = serverState.Position;
		Velocity = serverState.Velocity;

		_isAttacking = false;
		_isRolling = false;
		_isHurt = false;

		string currentWeaponPath = _currentWeapon?.SceneFilePath ?? "";
		if (serverState.EquippedWeaponPath != currentWeaponPath)
		{
			if (!string.IsNullOrEmpty(serverState.EquippedWeaponPath))
				ApplyWeaponAttachment(serverState.EquippedWeaponPath);
			else
				HideWeaponVisual();
		}

		string currentOffhandPath = _currentOffhandScene?.ResourcePath ?? "";
		if (serverState.EquippedOffhandPath != currentOffhandPath)
		{
			if (!string.IsNullOrEmpty(serverState.EquippedOffhandPath))
			{
				_currentOffhandScene = GD.Load<PackedScene>(serverState.EquippedOffhandPath);
				ApplyOffhandVisual(serverState.EquippedOffhandPath);
			}
			else
				HideOffhandVisual();
		}
	}

	private void HideWeaponVisual()
	{
		if (_currentWeapon == null) return;
		if (_currentWeapon.Hitbox != null)
			_currentWeapon.Hitbox.BodyEntered -= OnHitboxBodyEntered;
		_currentWeapon.QueueFree();
		_currentWeapon = null;
	}

	private void HideOffhandVisual()
	{
		if (_shieldPivot != null) _shieldPivot.Visible = false;
		_currentOffhandScene = null;
	}

	private void OnHitboxBodyEntered(Node2D body)
	{
		// Don't hit yourself!
		if (body == this) return;
		if (!Multiplayer.IsServer() || _currentWeapon == null) return;

		if (!_hitBodies.Add(body)) return;

		if (body is Player enemy)
		{
			GD.Print($"Hit enemy for {_currentWeapon.Stats.Damage} damage!");

			_pendingHits.Add(new PendingHit
			{
				Target = enemy,
				Damage = _currentWeapon.Stats.Damage,
				Direction = _facingDirectionName
			});
		}
		else if (body is MobBase mob)
		{
			GD.Print($"Hit mob for {_currentWeapon.Stats.Damage} damage!");
			mob.TakeDamage(_currentWeapon.Stats.Damage);
		}
	}

	public void EquipWeapon(WeaponItem groundItem)
	{
		if (_currentWeapon != null && IsMultiplayerAuthority())
			DropItem(GD.Load<PackedScene>(_currentWeapon.SceneFilePath));

		ApplyWeaponAttachment(groundItem.SceneFilePath);
	}

	private void ApplyWeaponAttachment(string scenePath)
	{
		if (_currentWeapon != null)
		{
			if (_currentWeapon.Hitbox != null)
				_currentWeapon.Hitbox.BodyEntered -= OnHitboxBodyEntered;

			_currentWeapon.QueueFree();
		}

		var weaponScene = GD.Load<PackedScene>(scenePath);
		if (weaponScene == null)
		{
			GD.PrintErr($"Failed to load weapon scene: {scenePath}");
			return;
		}
		_currentWeapon = weaponScene.Instantiate<WeaponItem>();

		_currentWeapon.IsEquipped = true;
		_currentWeapon.Monitoring = false;
		_currentWeapon.Monitorable = false;

		if (_weaponPivotNode != null)
		{
			_weaponPivotNode.Position = Vector2.Zero;
			_weaponPivotNode.Rotation = 0;
			_weaponPivotNode.Scale = Vector2.One;
		}
		if (_weaponAnim != null && !string.IsNullOrEmpty(_weaponAnim.CurrentAnimation))
			_weaponAnim.Seek(_weaponAnim.CurrentAnimationPosition, true);

		if (_weaponPivot != null)
			_weaponPivot.Visible = false;

		if (_weaponPivotNode != null)
		{
			_weaponPivotNode.AddChild(_currentWeapon);

			_currentWeapon.Position = Vector2.Zero;
			_currentWeapon.Rotation = Mathf.DegToRad(_currentWeapon.ItemRotation);

			_currentWeapon.ZIndex = 0;

			if (_currentWeapon.Hitbox != null)
			{
				_currentWeapon.Hitbox.Monitoring = false;
				_currentWeapon.Hitbox.BodyEntered += OnHitboxBodyEntered;
			}
		}
	}

	public void EquipOffhand(PackedScene droppedScene, Texture2D texture, Rect2 region,
		Vector2 scale, Vector2 offset, float rotation = 0f)
	{
		if (_currentOffhandScene != null && IsMultiplayerAuthority())
			DropItem(_currentOffhandScene);

		_currentOffhandScene = droppedScene;
		ApplyOffhandVisual(texture, region, scale, offset, rotation);
	}

	private void ApplyOffhandVisual(string scenePath)
	{
		var scene = GD.Load<PackedScene>(scenePath);
		var item = scene.Instantiate<OffhandItem>();
		ApplyOffhandVisual(item.ItemTexture, item.ItemRegion,
			item.ItemScale, item.ItemOffset, item.ItemRotation);
		item.QueueFree();
	}

	private void ApplyOffhandVisual(Texture2D texture, Rect2 region,
		Vector2 scale, Vector2 offset, float rotation)
	{
		if (_shieldPivot == null) return;

		if (_offHandPivotNode != null)
		{
			_offHandPivotNode.Position = Vector2.Zero;
			_offHandPivotNode.Rotation = 0;
			_offHandPivotNode.Scale = Vector2.One;
		}
		if (_weaponAnim != null && !string.IsNullOrEmpty(_weaponAnim.CurrentAnimation))
			_weaponAnim.Seek(_weaponAnim.CurrentAnimationPosition, true);

		_shieldPivot.Texture = texture;
		_shieldPivot.RegionEnabled = true;
		_shieldPivot.RegionRect = region;
		_shieldPivot.Rotation = Mathf.DegToRad(rotation);
		_shieldPivot.Scale = scale;
		_shieldPivot.Offset = offset;
		_shieldPivot.Visible = true;
	}

	private void DropItem(PackedScene scene)
	{
		if (scene == null) return;
		Rpc(MethodName.DropItemRpc, scene.ResourcePath);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void DropItemRpc(string scenePath)
	{
		var scene = GD.Load<PackedScene>(scenePath);
		var instance = scene.Instantiate<Node2D>();
		instance.Position = GlobalPosition;
		instance.RotationDegrees = 45f;
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
