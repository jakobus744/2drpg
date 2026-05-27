using System;
using Godot;
using RPG2d.World.Items;

namespace RPG2d.Player;

public partial class Player : CharacterBody2D
{
	private const float SpeedWalk = 70f;
	private const float SpeedRun = 100f;

	private const float MaxHealth = 100f;
	private const float HealthRecovery = 1f;

	private const float MaxStamina = 100f;
	private const float StaminaRecovery = .8f;
	private const float MovingStaminaRecovery = .2f;
	private const float RollCost = 20f;
	private const float SprintCost = .10f;
	private const float SprintFalloff = 33f;

	public enum MoveState
	{
		Idle,
		Walk,
		Run,
		Dead
	}

	private MoveState _moveState = MoveState.Idle;

	// Letzter Zustand wird gebraucht um zwischen zwei Idle-Varianten zu unterscheiden:
	private MoveState _lastMoveState = MoveState.Idle;
	private Vector2 _facingDirection = Vector2.Down;

	// Verhindert Bewegungs-/Animationsunterbrechung während Angriff, Rolle oder Treffer
	// Dead sperrt zusätzlich alles dauerhaft bis Respawn
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

	// Aktuell ausgerüstete Szenen  gebraucht um beim Item-Tausch das alte Item fallen zu lassen
	private PackedScene _currentOffhandScene;
	private WeaponItem _currentWeapon;

	// Multiplayer: Server schreibt Position + Animation, Clients lesen und interpolieren
	[Export] public Vector2 SyncPosition = Vector2.Zero;
	[Export] public string SyncAnimation = "";

	public static Player LocalPlayer { get; private set; }
	public PlayerInput Input { get; private set; }

	public override void _EnterTree()
	{
		// Node-Name ist die PeerId des Besitzers (gesetzt vom Lobby-System)
		// SetMultiplayerAuthority bestimmt wer Input schicken darf
		if (int.TryParse(Name, out int peerId))
			SetMultiplayerAuthority(peerId);
		else
			SetMultiplayerAuthority(1); // Fallback: Server hat Kontrolle
	}

	public override void _Ready()
	{
		// YSort am Player selbst deaktivieren  der Parent (Welt) übernimmt das Sorting
		YSortEnabled = false;

		// Primärer Sprite: neue Sprites ohne eingebautes Schwert
		_anim = GetNode<AnimatedSprite2D>("Base Animation");
		_anim.AnimationFinished += OnAnimationFinished;

		// Altes Sprite (Swordsman mit eingebautem Schwert) ausblenden  nicht mehr aktiv
		GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D")?.Hide();

		_weaponAnim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

		// WeaponSprite und ShieldSprite starten unsichtbar werden sichtbar wenn Item equipped
		_weaponPivotNode = GetNodeOrNull<Node2D>("WeaponPivot");
		_offHandPivotNode = GetNodeOrNull<Node2D>("OffHandPivot");
		_weaponPivot = GetNodeOrNull<Sprite2D>("WeaponPivot/WeaponSprite");
		_shieldPivot = GetNodeOrNull<Sprite2D>("OffHandPivot/OffHandSprite");
		if (_weaponPivot != null) _weaponPivot.Visible = false;
		if (_shieldPivot != null) _shieldPivot.Visible = false;

		// ServerSynchronizer gehört dem Server er liest SyncPosition/SyncAnimation und verteilt sie
		var sync = GetNodeOrNull<MultiplayerSynchronizer>("ServerSynchronizer");
		sync?.SetMultiplayerAuthority(1);

		// Kamera nur beim lokalen Spieler aktivieren
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

	public override void _PhysicsProcess(double delta)
	{
		// Server schreibt laufend Position + Animation in die Sync-Exports
		// MultiplayerSynchronizer überträgt diese an alle Clients
		if (Multiplayer.IsServer())
		{
			SyncPosition = Position;
			SyncAnimation = _anim.Animation;
		}

		// Lokaler Spieler wird durch ProcessCommand gesteuert, nicht hier
		if (IsMultiplayerAuthority()) return;

		// Remote-Spieler: Position interpolieren (verhindert Rucken bei Netzwerklatenz)
		Position = Position.Lerp(SyncPosition, (float)delta * 15f);

		// Animation des remote Spielers synchron halten
		if (!string.IsNullOrEmpty(SyncAnimation))
			_anim.Play(SyncAnimation);
	}

	// Von außen aufrufbar (z.B. durch Health-System bei Treffern)
	// Kurze Unterbrechung: Hurt-Animation läuft durch, danach normal weiter
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void Hurt(string dirName)
	{
		if (_moveState == MoveState.Dead) return; // Keine Hurt-Anim wenn bereits tot

		string animName = "hurt_" + dirName;
		if (!_anim.SpriteFrames.HasAnimation(animName)) return;

		_isHurt = true;
		_anim.Play(animName);
	}

	// Von außen aufrufbar (z.B. durch Health-System wenn HP auf 0 fällt)
	// Setzt State auf Dead, spielt Death-Animation, blockiert danach allen Input
	public void Die(string dirName)
	{
		if (_moveState == MoveState.Dead) return; // Nicht doppelt sterben
		_moveState = MoveState.Dead;
		Velocity = Vector2.Zero;

		string animName = "death_" + dirName;
		if (_anim.SpriteFrames.HasAnimation(animName))
			_anim.Play(animName);
	}

	// Hauptschnittstelle: wird vom PlayerInput-System aufgerufen (lokal + Server)
	// Verarbeitet einen Eingabe-Snapshot und gibt den resultierenden State zurück
	public PlayerState ProcessCommand(PlayerState previousState, PlayerCmd cmd)
	{
		var state = previousState?.Clone() ?? new PlayerState
		{
			Position = Position,
			Velocity = Velocity,
			Stamina = MaxStamina,
			Health = MaxHealth,
		};

		if (_moveState == MoveState.Dead)
		{
			state.Velocity = Vector2.Zero;
			return state;
		}

		// 1. Aktionen verarbeiten
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

		// 2. Bewegung verarbeiten
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
			_moveState = cmd.IsRunPressed ? MoveState.Run : MoveState.Walk;

			var wishSpeed = SpeedWalk;
			if (cmd.IsRunPressed && state.Stamina > SprintCost)
			{
				wishSpeed = state.Stamina <= SprintFalloff
					? SpeedWalk + (SpeedRun - SpeedWalk) * (state.Stamina / SprintFalloff)
					: SpeedRun;
				state.Stamina -= SprintCost;
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

		// 3. Animation + Physik
		UpdateAnimation(cmd.FacingDirection);
		MoveAndSlide();

		state.Position = Position;
		state.Velocity = Velocity;

		return state;
	}

	private void StartRoll(string dirName)
	{
		string animName = "roll_" + dirName;
		if (!_anim.SpriteFrames.HasAnimation(animName)) return;

		// Laufenden Angriff abbrechen — sonst bleibt _isAttacking true nach Roll
		if (_isAttacking)
		{
			_isAttacking = false;
			if (_currentWeapon?.Hitbox != null)
				_currentWeapon.Hitbox.SetDeferred("monitoring", false);
		}
		_isHurt = false;

		_isRolling = true;
		_anim.Play(animName);
		PlayWeaponAnim(animName);
	}

	private void StartAttack(string dirName)
	{
		if (_currentWeapon == null) return; // Kann nicht angreifen ohne Waffe
		string animName = GetAttackAnimationName(dirName);
		if (!_anim.SpriteFrames.HasAnimation(animName)) return;

		_isAttacking = true;
		if (_currentWeapon.Hitbox != null)
			_currentWeapon.Hitbox.SetDeferred("monitoring", true);

		_anim.Play(animName);
		PlayWeaponAnim(animName);
	}

	// Stamina-Kosten pro Angriff: Basis aus WeaponData × Bewegungs-Multiplikator
	private float GetAttackStaminaCost()
	{
		var stats = _currentWeapon.Stats;
		return _moveState switch
		{
			MoveState.Run => stats.AttackStaminaCost * stats.RunAttackMultiplier,
			MoveState.Walk => stats.AttackStaminaCost * stats.WalkAttackMultiplier,
			_ => stats.AttackStaminaCost, // Idle = Basis-Kosten
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
		// Während Angriff/Rolle/Tod läuft die Animation bereits nicht unterbrechen
		if (IsActionLocked) return;

		switch (_moveState)
		{
			case MoveState.Idle:
				// Zwei Idle-Varianten: .1 nach Walk, .2 nach Sprint
				// Fällt auf "idle_*" zurück falls Variante nicht existiert
				PlayIdleAnimation(dirName);
				PlayWeaponAnim("idle_" + dirName);
				break;
			case MoveState.Walk:
				_anim.Play("walk_" + dirName);
				PlayWeaponAnim("walk_" + dirName);
				break;
			case MoveState.Run:
				_anim.Play("run_" + dirName);
				// Falls AnimationPlayer keine run_*-Animation hat: Waffe bleibt auf letzter Position
				PlayWeaponAnim("run_" + dirName);
				break;
		}

		UpdateWeaponZIndex(dirName);
	}

	private void PlayIdleAnimation(string dir)
	{
		// Nach Sprint andere Idle-Variante als nach normalem Laufen
		string anim = _lastMoveState == MoveState.Run
			? "idle_" + dir + ".2"
			: "idle_" + dir + ".1";

		// Fallback falls Variante nicht in SpriteFrames definiert
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

	// Wird aufgerufen wenn eine nicht-loopende Animation (Angriff, Rolle, Treffer) fertig ist
	private void OnAnimationFinished()
	{
		var name = _anim.Animation.ToString();

		if (name.Contains("attack"))
		{
			_isAttacking = false;

			// Turn OFF the attack hitbox
			if (_currentWeapon != null && _currentWeapon.Hitbox != null)
				_currentWeapon.Hitbox.SetDeferred("monitoring", false);
		}

		if (name.Contains("attack")) _isAttacking = false;
		if (name.Contains("roll")) _isRolling = false;
		if (name.Contains("hurt")) _isHurt = false;
		// "death" bewusst nicht hier — Dead-State bleibt bis Respawn von außen
	}

	// Wird vom Server bei Reconciliation aufgerufen — setzt Position/Velocity hart zurück
	public void ApplyState(PlayerState state)
	{
		Position = state.Position;
		Velocity = state.Velocity;
	}

	private void OnHitboxBodyEntered(Node2D body)
	{
		// Don't hit yourself!
		if (body == this) return;

		if (body is Player enemy && Multiplayer.IsServer())
		{
			// Since we are holding the actual weapon, we can read its damage!
			GD.Print($"Hit enemy for {_currentWeapon.Stats.Damage} damage!");

			enemy.Hurt(_facingDirection.ToString());
			enemy.Rpc("Hurt", _facingDirection.ToString());
		}
	}

	public void EquipWeapon(WeaponItem groundItem)
	{
		string scenePath = groundItem.SceneFilePath;
		Vector2 itemScale = groundItem.Scale;

		// Alte Waffe fallen lassen (45° gedreht) bevor neue equipped wird
		if (_currentWeapon != null && IsMultiplayerAuthority())
		{
			var dropScene = GD.Load<PackedScene>(_currentWeapon.SceneFilePath);
			DropItem(dropScene);
		}

		ApplyWeaponAttachment(scenePath, itemScale);

		if (Multiplayer.HasMultiplayerPeer())
		{
			Rpc(MethodName.SyncWeaponEquip, scenePath, itemScale);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SyncWeaponEquip(string scenePath, Vector2 scale)
	{
		ApplyWeaponAttachment(scenePath, scale);
	}

	private void ApplyWeaponAttachment(string scenePath, Vector2 scale)
	{
		// 1) Alte Waffe aufräumen — Hitbox-Event abmelden, Node entfernen
		if (_currentWeapon != null)
		{
			if (_currentWeapon.Hitbox != null)
				_currentWeapon.Hitbox.BodyEntered -= OnHitboxBodyEntered;

			_currentWeapon.QueueFree();
		}

		// 2) Neue Waffe instantiieren (Scene-Pfad kommt ggf. übers Netzwerk)
		var weaponScene = GD.Load<PackedScene>(scenePath);
		_currentWeapon = weaponScene.Instantiate<WeaponItem>();

		// 3) Pickup-Logik deaktivieren — Waffe ist jetzt equipped, nicht mehr am Boden
		_currentWeapon.IsEquipped = true;
		_currentWeapon.Monitoring = false;
		_currentWeapon.Monitorable = false;

		// 4) Pivot-Transform zurücksetzen — AnimationPlayer hinterlässt Position/Rotation
		//    vom letzten Frame der vorherigen Animation. Ohne Reset sitzt die neue Waffe schief.
		if (_weaponPivotNode != null)
		{
			_weaponPivotNode.Position = Vector2.Zero;
			_weaponPivotNode.Rotation = 0;
			_weaponPivotNode.Scale = Vector2.One;
		}
		_weaponAnim?.Seek(_weaponAnim.CurrentAnimationPosition, true);

		// 5) Altes WeaponSprite (Sprite2D) ausblenden — wird nicht mehr gebraucht,
		//    da die Weapon-Scene ihr eigenes Sprite mitbringt
		if (_weaponPivot != null)
			_weaponPivot.Visible = false;

		// 6) Weapon-Scene als Kind von WeaponPivot einhängen
		if (_weaponPivotNode != null)
		{
			_weaponPivotNode.AddChild(_currentWeapon);

			// 7) Lokale Transform der Waffe resetten — auf dem Boden hatte sie
			//    eine Welt-Position/Rotation, die hier nicht mehr gilt
			_currentWeapon.Position = Vector2.Zero;
			_currentWeapon.Rotation = Mathf.DegToRad(_currentWeapon.ItemRotation);
			_currentWeapon.Scale = scale;

			// 8) Z-Index resetten — Weapon-Scenes haben z_index=1 für Boden-Darstellung,
			//    aber am Spieler wird Z-Order von UpdateWeaponZIndex gesteuert
			_currentWeapon.ZIndex = 0;

			// 9) Hitbox vorbereiten — startet deaktiviert, wird bei Attack-State eingeschaltet
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
		if (_currentOffhandScene != null)
			DropItem(_currentOffhandScene);

		_currentOffhandScene = droppedScene;

		if (_shieldPivot == null) return;

		// Pivot-Transform zurücksetzen — AnimationPlayer hinterlässt Position/Rotation
		if (_offHandPivotNode != null)
		{
			_offHandPivotNode.Position = Vector2.Zero;
			_offHandPivotNode.Rotation = 0;
			_offHandPivotNode.Scale = Vector2.One;
		}
		_weaponAnim?.Seek(_weaponAnim.CurrentAnimationPosition, true);

		_shieldPivot.Texture = texture;
		_shieldPivot.RegionEnabled = true;
		_shieldPivot.RegionRect = region;
		_shieldPivot.Rotation = Mathf.DegToRad(rotation);
		_shieldPivot.Scale = scale;
		_shieldPivot.Offset = offset;
		_shieldPivot.Visible = true;
	}

	// Instanziiert das Item als Node2D an der aktuellen Spielerposition in der Welt
	private void DropItem(PackedScene scene)
	{
		if (scene == null) return;
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

	// AnimationPlayer für WeaponPivot abspielen — nur wenn Animation wechselt
	// Sprite (_anim) und AnimationPlayer laufen gleiche FPS → bleiben synchron ohne Frame-Sync
	private void PlayWeaponAnim(string animName)
	{
		if (_weaponAnim == null) return;
		if (_weaponAnim.CurrentAnimation == animName) return; // Läuft bereits
		if (_weaponAnim.HasAnimation(animName))
			_weaponAnim.Play(animName);
	}
}
