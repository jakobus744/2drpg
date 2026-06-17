using System;
using System.Collections.Generic;
using Godot;
using RPG2d.Entity;
using RPG2d.World.Items;
using RPG2d.World.Items.Inventory;
using GameMgr = RPG2d.GameManager.GameManager;

namespace RPG2d.Player;

public partial class Player : BaseEntity<PlayerState>
{
	public PlayerInventory Inventory { get; private set; }
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

	// Letzter Zustand wird gebraucht um zwischen zwei Idle-Varianten zu unterscheiden:
	private MoveState _lastMoveState = MoveState.Idle;
	private Vector2 _facingDirection = Vector2.Down;
	private string _facingDirectionName = "down";

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

	// Tick-based pickup: zuletzt betretener PickupItem in Reichweite (Input-Sensing)
	private PickupItem _nearbyPickup;

	// Tick-based damage: Treffer werden hier gesammelt und in ProcessCommand verarbeitet
	private struct PendingHit
	{
		public Player Target;
		public float Damage;
		public string Direction;
	}
	private readonly List<PendingHit> _pendingHits = new();
	private readonly HashSet<Node2D> _hitBodies = new(); // verhindert Doppeltreffer pro Angriff
	private float _pendingDamage;
	private string _pendingDamageDir = "";

	// Lag compensation: captured when StartAttack runs on the server
	private uint _attackStartServerTick;

	// Remote sync tracking: nur bei Änderung Waffen-Visual neu laden
	private string _lastSyncWeaponPath = "";
	private string _lastSyncOffhandPath = "";

	// Multiplayer: Server schreibt Position + Animation, Clients lesen und interpolieren
	[Export] public Vector2 SyncPosition = Vector2.Zero;
	[Export] public string SyncAnimation = "";
	[Export] public string SyncWeaponPath = "";
	[Export] public string SyncOffhandPath = "";

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
		Inventory = new PlayerInventory();
		
		// YSort am Player selbst deaktivieren  der Parent (Welt) übernimmt das Sorting
		YSortEnabled = false;

		// Tick 0 mit Initialwerten befüllen, damit ProcessCommand immer einen Vorgänger hat
		StateBuffer.Set(0, new PlayerState
		{
			Health = MaxHealth,
			Stamina = MaxStamina,
			Position = Position
		});

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

	// Wird von PickupItem.BodyEntered/BodyExited aufgerufen — nur Input-Sensing, keine Gameplay-Logik
	public void RegisterNearbyPickup(PickupItem item) => _nearbyPickup = item;
	public void UnregisterNearbyPickup(PickupItem item)
	{
		if (_nearbyPickup == item) _nearbyPickup = null;
	}

	// Scene-Pfad des nächstgelegenen Pickups — für PlayerCmd.InteractTargetPath
	public string NearbyPickupPath => _nearbyPickup?.SceneFilePath ?? "";

	// Tick-basierter Schaden: wird vom Angreifer aufgerufen, im nächsten ProcessCommand verarbeitet
	public void QueueDamage(float amount, string direction)
	{
		_pendingDamage += amount;
		_pendingDamageDir = direction;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Server schreibt laufend Position + Animation + Waffen-State in die Sync-Exports
		// MultiplayerSynchronizer überträgt diese an alle Clients
		if (Multiplayer.IsServer())
		{
			SyncPosition = Position;
			SyncAnimation = _anim.Animation;
			SyncWeaponPath = _currentWeapon?.SceneFilePath ?? "";
			SyncOffhandPath = _currentOffhandScene?.ResourcePath ?? "";
		}

		// Lokaler Spieler wird durch ProcessCommand gesteuert, nicht hier
		if (IsMultiplayerAuthority()) return;

		// Remote-Spieler: Position interpolieren (verhindert Rucken bei Netzwerklatenz)
		Position = Position.Lerp(SyncPosition, (float)delta * 15f);

		// Animation des remote Spielers synchron halten
		if (!string.IsNullOrEmpty(SyncAnimation))
		{
			_anim.Play(SyncAnimation);
			PlayWeaponAnim(SyncAnimation);
		}

		// Weapon-Visuals nur bei Änderung neu laden
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

	// Von außen aufrufbar (z.B. durch Health-System bei Treffern)
	// Kurze Unterbrechung: Hurt-Animation läuft durch, danach normal weiter
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void Hurt(string dirName)
	{
		if (_moveState == MoveState.Dead) return; // Keine Hurt-Anim wenn bereits tot

		string animName = "hurt_" + dirName;
		CancelAttack();
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
		CancelAttack();

		string animName = "death_" + dirName;
		if (_anim.SpriteFrames.HasAnimation(animName))
			_anim.Play(animName);
	}

	// Server-Authority RPC für externe Todes-Auslöser (Fallen, Umgebungsschaden, etc.)
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void DieRpc(string dirName)
	{
		Die(dirName);
	}

	// Hauptschnittstelle: wird vom PlayerInput-System aufgerufen (lokal + Server)
	// Liest vorherigen State aus StateBuffer, verarbeitet den Command und schreibt Ergebnis zurück
	public void ProcessCommand(PlayerCmd cmd)
	{
		var state = StateBuffer.Get(cmd.Tick - 1);
		CurrentTick = cmd.Tick; // zuerst lesen dann schreiben

		// 0. Eingehenden Schaden verarbeiten (tick-basiert, vom Server autoritativ)
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

		// 0b. Pickup verarbeiten — Item NUR ins Inventar (kein Auto-Equip mehr).
		// Equip läuft jetzt über die Equipment-Slots (siehe 0d).
		if (cmd.IsInteractPressed && _nearbyPickup != null)
		{
			var pickup = _nearbyPickup;
			_nearbyPickup = null; // verbraucht — kein Re-Pickup während Reprediction

			// Nur lokaler Besitzer füllt sein (privates) Inventar
			if (IsMultiplayerAuthority())
			{
				var itemData = pickup.GetItemData();
				if (itemData != null)
					Inventory.TryAddItem(itemData);
			}

			// Server entfernt das Item authoritativ
			// TODO: bei vollem Inventar Item liegen lassen (Server kennt Client-Inventar nicht)
			if (Multiplayer.HasMultiplayerPeer())
			{
				if (Multiplayer.IsServer())
					pickup.Rpc("RemoveItem");
			}
			else
			{
				pickup.QueueFree(); // Single-Player
			}
		}

		// 0d. Equip aus Equipment-Slots (Pfade kommen via cmd, getrieben vom Inventar).
		// Ändert sich der gewünschte Pfad → Visual + synced State aktualisieren.
		if (cmd.EquippedWeaponPath != state.EquippedWeaponPath)
		{
			if (!string.IsNullOrEmpty(cmd.EquippedWeaponPath))
				ApplyWeaponAttachment(cmd.EquippedWeaponPath);
			else
				HideWeaponVisual();
			state.EquippedWeaponPath = cmd.EquippedWeaponPath;
		}

		if (cmd.EquippedOffhandPath != state.EquippedOffhandPath)
		{
			if (!string.IsNullOrEmpty(cmd.EquippedOffhandPath))
			{
				_currentOffhandScene = GD.Load<PackedScene>(cmd.EquippedOffhandPath);
				ApplyOffhandVisual(cmd.EquippedOffhandPath);
			}
			else
			{
				HideOffhandVisual();
			}
			state.EquippedOffhandPath = cmd.EquippedOffhandPath;
		}

		// 0c. Ausgehende Treffer an Ziele weiterleiten (tick-basiert)
		foreach (var hit in _pendingHits)
			hit.Target.QueueDamage(hit.Damage, hit.Direction);
		_pendingHits.Clear();

		if (_moveState == MoveState.Dead)
		{
			state.Velocity = Vector2.Zero;
			StateBuffer.Set(cmd.Tick, state);
			return;
		}

		// 1. Aktionen verarbeiten
		if (cmd.IsAttackPressed && !IsActionLocked && _currentWeapon != null)
		{
			float attackCost = GetAttackStaminaCost();
			if (cmd.Tick >= state.NextAttackTick && state.Stamina >= attackCost)
			{
				_attackStartServerTick = GameMgr.ServerTick;
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

		// 3. Animation + Physik
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

			_hitBodies.Clear();
			// Turn OFF the attack hitbox
			if (_currentWeapon != null && _currentWeapon.Hitbox != null)
				_currentWeapon.Hitbox.SetDeferred("monitoring", false);
		}

		if (name.Contains("roll")) _isRolling = false;
		if (name.Contains("hurt")) _isHurt = false;
		// "death" bewusst nicht hier — Dead-State bleibt bis Respawn von außen
	}

	// Wird vom Server bei Reconciliation aufgerufen — setzt Position/Velocity hart zurück
	public override void ApplyServerState(uint tick, PlayerState serverState)
	{
		base.ApplyServerState(tick, serverState);
		Position = serverState.Position;
		Velocity = serverState.Velocity;

		// Action-Locks zurücksetzen, damit Reprediction nicht mit veralteten States startet
		_isAttacking = false;
		_isRolling = false;
		_isHurt = false;

		// Weapon-Reconciliation: falls Server eine andere Waffe hat als angezeigt
		string currentWeaponPath = _currentWeapon?.SceneFilePath ?? "";
		if (serverState.EquippedWeaponPath != currentWeaponPath)
		{
			if (!string.IsNullOrEmpty(serverState.EquippedWeaponPath))
				ApplyWeaponAttachment(serverState.EquippedWeaponPath);
			else
				HideWeaponVisual();
		}

		// Offhand-Reconciliation
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

		// Verhindert Doppeltreffer durch mehrere Collision Shapes oder erneuten BodyEntered
		if (!_hitBodies.Add(body)) return;

		if (body is Player enemy)
		{
			GD.Print($"Hit enemy for {_currentWeapon.Stats.Damage} damage!");

			// Queue damage for tick-based processing (statt direkt Hurt aufzurufen)
			_pendingHits.Add(new PendingHit
			{
				Target = enemy,
				Damage = _currentWeapon.Stats.Damage,
				Direction = _facingDirectionName
			});
		}
		else if (body is MobBase mob)
		{
			// Lag-compensated hit: rewind mob to attack tick, validate with
			// actual collision shapes via OverlapsBody, then restore.
			uint lookupTick = _attackStartServerTick > 0 ? _attackStartServerTick - 1 : 0;
			var saved = new MobState
			{
				Position = mob.Position,
				Velocity = mob.Velocity,
				Health = mob.CurrentHealth,
				IsDead = mob.IsDead
			};
			mob.ApplyState(mob.GetStateAtTick(lookupTick));

			bool valid = _currentWeapon.Hitbox != null
				&& _currentWeapon.Hitbox.OverlapsBody(mob);

			mob.ApplyState(saved);

			if (!valid)
				return;

			GD.Print($"Hit mob for {_currentWeapon.Stats.Damage} damage!");
			mob.TakeDamage(_currentWeapon.Stats.Damage);
		}
	}

	// Wird von ProcessCommand (tick-basiert) aufgerufen — Visual + State werden dort gesetzt
	public void EquipWeapon(WeaponItem groundItem)
	{
		if (_currentWeapon != null && IsMultiplayerAuthority())
			DropItem(GD.Load<PackedScene>(_currentWeapon.SceneFilePath));

		ApplyWeaponAttachment(groundItem.SceneFilePath);
	}

	private void ApplyWeaponAttachment(string scenePath)
	{
		// 1) Alte Waffe aufräumen — Hitbox-Event abmelden, Node entfernen
		if (_currentWeapon != null)
		{
			if (_currentWeapon.Hitbox != null)
				_currentWeapon.Hitbox.BodyEntered -= OnHitboxBodyEntered;

			_currentWeapon.QueueFree();
		}

		// 2) Neue Waffe instantiieren (Scene-Pfad kommt aus State oder Sync-Export)
		var weaponScene = GD.Load<PackedScene>(scenePath);
		if (weaponScene == null)
		{
			GD.PrintErr($"Failed to load weapon scene: {scenePath}");
			return;
		}
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
		if (_weaponAnim != null && !string.IsNullOrEmpty(_weaponAnim.CurrentAnimation))
			_weaponAnim.Seek(_weaponAnim.CurrentAnimationPosition, true);

		// 5) Altes WeaponSprite (Sprite2D) ausblenden — wird nicht mehr gebraucht,
		//    da die Weapon-Scene ihr eigenes Sprite mitbringt
		if (_weaponPivot != null)
			_weaponPivot.Visible = false;

		// 6) Weapon-Scene als Kind von WeaponPivot einhängen
		if (_weaponPivotNode != null)
		{
			_weaponPivotNode.AddChild(_currentWeapon);

			// 7) Lokale Transform der Waffe resetten — Scale aus der Scene lesen
			_currentWeapon.Position = Vector2.Zero;
			_currentWeapon.Rotation = Mathf.DegToRad(_currentWeapon.ItemRotation);

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

	// Wird von ProcessCommand (tick-basiert) aufgerufen — Visual + State werden dort gesetzt
	public void EquipOffhand(PackedScene droppedScene, Texture2D texture, Rect2 region,
		Vector2 scale, Vector2 offset, float rotation = 0f)
	{
		if (_currentOffhandScene != null && IsMultiplayerAuthority())
			DropItem(_currentOffhandScene);

		_currentOffhandScene = droppedScene;
		ApplyOffhandVisual(texture, region, scale, offset, rotation);
	}

	// Overload für State-basierte Syncs (Scene-Pfad statt einzelner Properties)
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

		// Pivot-Transform zurücksetzen — AnimationPlayer hinterlässt Position/Rotation
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

	// Instanziiert das Item als Node2D an der aktuellen Spielerposition in der Welt
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
