using System;
using System.Collections.Generic;
using Godot;
using RPG2d.Entity;
using RPG2d.World.Items;
using RPG2d.World.Items.Data;
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

	// Rüstungs-Layer (mit-animierte Overlays über "Base Animation")
	private AnimatedSprite2D _bootsLayer;
	private AnimatedSprite2D _armorLayer;
	private AnimatedSprite2D _helmLayer;
	private AnimatedSprite2D _faceLayer;
	private AnimatedSprite2D _eyesLayer;
	private AnimatedSprite2D _hairLayer;
	// Aktuell angelegte Rüstungs-ItemData-Pfade (für Sync-Export an Remotes)
	private string _currentHelmetPath = "";
	private string _currentArmorPath = "";
	private string _currentBootsPath = "";
	// Cache: "Teil|Material" -> gebaute SpriteFrames (nicht bei jedem Equip neu bauen)
	private readonly Dictionary<string, SpriteFrames> _styleFramesCache = new();

	public static readonly string[] HairStyles =
		{ "Standard", "Blau", "Blond", "Braun", "Gruen", "Lila", "Pink", "Rot", "Schwarz", "Tuerkis", "Weiss" };
	public static readonly string[] EyeStyles =
		{ "Standard", "Bernstein", "Braun", "Cyan", "Grau", "Gruen", "Rot", "Violett" };
	public static readonly string[] FaceStyles =
		{ "Standard", "Blass", "Dunkel", "Gebraeunt", "Hell", "Sehr_Dunkel" };

	public const string DefaultHairStyle = "Standard";
	public const string DefaultEyeStyle = "Standard";
	public const string DefaultFaceStyle = "Standard";

	private string _currentHairStyle = DefaultHairStyle;
	private string _currentEyeStyle = DefaultEyeStyle;
	private string _currentFaceStyle = DefaultFaceStyle;

	public string CurrentHairStyle => _currentHairStyle;
	public string CurrentEyeStyle => _currentEyeStyle;
	public string CurrentFaceStyle => _currentFaceStyle;

	// Tick-based pickup: zuletzt betretener PickupItem in Reichweite (Input-Sensing)
	private PickupItem _nearbyPickup;

	public bool InventoryDirty { get; set; }

	public PickupItem GetNearbyPickupItem() => _nearbyPickup;

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
	private string _lastSyncHelmetPath = "";
	private string _lastSyncArmorPath = "";
	private string _lastSyncBootsPath = "";
	private string _lastSyncHairStyle = "";
	private string _lastSyncEyeStyle = "";
	private string _lastSyncFaceStyle = "";
	private string _lastSyncedAnimation = "";

	// Multiplayer: Server schreibt Position + Animation, Clients lesen und interpolieren
	[Export] public Vector2 SyncPosition = Vector2.Zero;
	[Export] public string SyncAnimation = "";
	[Export] public string SyncWeaponPath = "";
	[Export] public string SyncOffhandPath = "";
	[Export] public string SyncHelmetPath = "";
	[Export] public string SyncArmorPath = "";
	[Export] public string SyncBootsPath = "";
	[Export] public string SyncHairStyle = DefaultHairStyle;
	[Export] public string SyncEyeStyle = DefaultEyeStyle;
	[Export] public string SyncFaceStyle = DefaultFaceStyle;

	public static Player LocalPlayer { get; private set; }
	public static readonly List<Player> AllPlayers = new();
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

		// Rüstungs-Overlay-Layer  starten leer/unsichtbar, werden beim Equip gefüllt
		_bootsLayer = GetNodeOrNull<AnimatedSprite2D>("BootsLayer");
		_armorLayer = GetNodeOrNull<AnimatedSprite2D>("ArmorLayer");
		_helmLayer = GetNodeOrNull<AnimatedSprite2D>("HelmLayer");
		_faceLayer = GetNodeOrNull<AnimatedSprite2D>("FaceLayer");
		_eyesLayer = GetNodeOrNull<AnimatedSprite2D>("EyesLayer");
		_hairLayer = GetNodeOrNull<AnimatedSprite2D>("HairLayer");
		if (_bootsLayer != null) _bootsLayer.Visible = false;
		if (_armorLayer != null) _armorLayer.Visible = false;
		if (_helmLayer != null) _helmLayer.Visible = false;
		ApplyAppearanceInternal(DefaultHairStyle, DefaultEyeStyle, DefaultFaceStyle);

		// ServerSynchronizer gehört dem Server er liest SyncPosition/SyncAnimation und verteilt sie
		var sync = GetNodeOrNull<MultiplayerSynchronizer>("ServerSynchronizer");
		if (sync != null)
		{
			sync.SetMultiplayerAuthority(1);
			sync.ReplicationInterval = 0.1f;
		}

		// Kamera nur beim lokalen Spieler aktivieren
		var camera = GetNodeOrNull<Camera2D>("Camera2D");
		if (camera != null)
			camera.Enabled = IsMultiplayerAuthority();

		Input = GetNodeOrNull<PlayerInput>("Input");
		if (IsMultiplayerAuthority())
			LocalPlayer = this;

		if (!AllPlayers.Contains(this))
			AllPlayers.Add(this);
	}

	public override void _ExitTree()
	{
		AllPlayers.Remove(this);
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
			SyncHelmetPath = _currentHelmetPath;
			SyncArmorPath = _currentArmorPath;
			SyncBootsPath = _currentBootsPath;
			SyncHairStyle = _currentHairStyle;
			SyncEyeStyle = _currentEyeStyle;
			SyncFaceStyle = _currentFaceStyle;
		}

		// Rüstungs-Layer jeden Frame an die Base-Animation koppeln (Owner + Remote)
		SyncVisualLayers();

		// Lokaler Spieler wird durch ProcessCommand gesteuert, nicht hier
		if (IsMultiplayerAuthority()) return;

		// Remote-Spieler: Position interpolieren (verhindert Rucken bei Netzwerklatenz)
		Position = Position.Lerp(SyncPosition, (float)delta * 15f);

		// Animation des remote Spielers synchron halten (nur bei Änderung)
		if (!string.IsNullOrEmpty(SyncAnimation) && SyncAnimation != _lastSyncedAnimation)
		{
			_anim.Play(SyncAnimation);
			PlayWeaponAnim(SyncAnimation);
			_lastSyncedAnimation = SyncAnimation;
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

		// Rüstungs-Layer der Remote-Spieler
		if (SyncHelmetPath != _lastSyncHelmetPath)
		{
			ApplyOrHideArmor(_helmLayer, EquipSlot.Helmet, SyncHelmetPath);
			_lastSyncHelmetPath = SyncHelmetPath;
		}
		if (SyncArmorPath != _lastSyncArmorPath)
		{
			ApplyOrHideArmor(_armorLayer, EquipSlot.Armor, SyncArmorPath);
			_lastSyncArmorPath = SyncArmorPath;
		}
		if (SyncBootsPath != _lastSyncBootsPath)
		{
			ApplyOrHideArmor(_bootsLayer, EquipSlot.Boots, SyncBootsPath);
			_lastSyncBootsPath = SyncBootsPath;
		}

		if (SyncHairStyle != _lastSyncHairStyle
			|| SyncEyeStyle != _lastSyncEyeStyle
			|| SyncFaceStyle != _lastSyncFaceStyle)
		{
			ApplyAppearanceInternal(SyncHairStyle, SyncEyeStyle, SyncFaceStyle);
			_lastSyncHairStyle = SyncHairStyle;
			_lastSyncEyeStyle = SyncEyeStyle;
			_lastSyncFaceStyle = SyncFaceStyle;
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

		// 0b. Pickup verarbeiten — Client predicted, Server validiert gegen autoritatives Inventar.
		if (cmd.IsInteractPressed && _nearbyPickup != null)
		{
			var pickup = _nearbyPickup;
			_nearbyPickup = null;

			if (IsMultiplayerAuthority() && !(Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer()))
			{
				var itemData = pickup.GetItemData();
				if (itemData != null)
				{
					int amount = pickup.AmountOverride > 0 ? pickup.AmountOverride : itemData.PickupAmount;
					Inventory.TryAddItem(itemData, amount);
				}
			}

			if (Multiplayer.HasMultiplayerPeer())
			{
				if (Multiplayer.IsServer())
				{
					var itemData = pickup.GetItemData();
					int amount = pickup.AmountOverride > 0 ? pickup.AmountOverride : itemData.PickupAmount;
					if (itemData != null && Inventory.TryAddItem(itemData, amount))
					{
						pickup.Visible = false;
						pickup.SetDeferred("monitoring", false);
						pickup.QueueFree();
						InventoryDirty = true;
						var gm = GetNodeOrNull<RPG2d.GameManager.GameManager>("/root/GameManager");
						gm?.TrackRemovedItem(pickup.SceneFilePath);
						gm?.Rpc("RemoveItemByScene", pickup.SceneFilePath, pickup.GlobalPosition);

					}
				}
			}
			else
			{
						pickup.Visible = false;
						pickup.SetDeferred("monitoring", false);
				pickup.QueueFree();
			}
		}

		// 0b2. Server: Inventar-Aktion aus cmd verarbeiten, Equipment + Consumable
		// aus autoritativem Inventar ableiten (überschreibt Client-Werte).
		if (Multiplayer.IsServer())
		{
			ProcessServerInventoryAction(cmd);

			var wep = Inventory.EquipmentSlots.GetValueOrDefault(EquipSlot.Weapon);
			cmd.EquippedWeaponPath = wep?.Data?.DroppedScenePath ?? "";
			var off = Inventory.EquipmentSlots.GetValueOrDefault(EquipSlot.Offhand);
			cmd.EquippedOffhandPath = off?.Data?.DroppedScenePath ?? "";
			var helm = Inventory.EquipmentSlots.GetValueOrDefault(EquipSlot.Helmet);
			cmd.EquippedHelmetPath = helm?.Data?.ResourcePath ?? "";
			var chest = Inventory.EquipmentSlots.GetValueOrDefault(EquipSlot.Armor);
			cmd.EquippedArmorPath = chest?.Data?.ResourcePath ?? "";
			var feet = Inventory.EquipmentSlots.GetValueOrDefault(EquipSlot.Boots);
			cmd.EquippedBootsPath = feet?.Data?.ResourcePath ?? "";

			if (cmd.IsUseItemPressed)
			{
				var addr = SlotAddress.Hotbar(cmd.ActiveHotbarIndex);
				var stack = Inventory.GetSlot(addr);
				if (stack != null && !stack.IsEmpty
					&& stack.Data.Category == ItemCategory.Consumable
					&& stack.Data.ItemId == cmd.InvItemId)
				{
					Inventory.RemoveFromSlot(addr, 1);
					InventoryDirty = true;
					cmd.UseStaminaRestore = stack.Data.StaminaRestore;
					cmd.UseHealthRestore = stack.Data.HealthRestore;
				}
				else
				{
					cmd.IsUseItemPressed = false;
					cmd.UseStaminaRestore = 0;
					cmd.UseHealthRestore = 0;
				}
			}

			Inventory.ActiveHotbarIndex = cmd.ActiveHotbarIndex;
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

		// 0d3. Rüstungs-Layer (Helm/Chest/Boots)  Pfade = ItemData-Ressourcen der Slots.
		if (cmd.EquippedHelmetPath != state.EquippedHelmetPath)
		{
			ApplyOrHideArmor(_helmLayer, EquipSlot.Helmet, cmd.EquippedHelmetPath);
			_currentHelmetPath = cmd.EquippedHelmetPath;
			state.EquippedHelmetPath = cmd.EquippedHelmetPath;
		}
		if (cmd.EquippedArmorPath != state.EquippedArmorPath)
		{
			ApplyOrHideArmor(_armorLayer, EquipSlot.Armor, cmd.EquippedArmorPath);
			_currentArmorPath = cmd.EquippedArmorPath;
			state.EquippedArmorPath = cmd.EquippedArmorPath;
		}
		if (cmd.EquippedBootsPath != state.EquippedBootsPath)
		{
			ApplyOrHideArmor(_bootsLayer, EquipSlot.Boots, cmd.EquippedBootsPath);
			_currentBootsPath = cmd.EquippedBootsPath;
			state.EquippedBootsPath = cmd.EquippedBootsPath;
		}

		// 0d2. Consumable-Effekt (Rechtsklick) — Server validiert + entfernt Item aus autoritativem
		// Inventar (siehe 0b2). Werte im cmd sind jetzt server-bestätigt.
		if (cmd.IsUseItemPressed)
		{
			state.Stamina += cmd.UseStaminaRestore;
			state.Health += cmd.UseHealthRestore;
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
		// Safety: wenn _isAttacking noch true ist aber die Animation keine
		// Attack-Animation ist, Lock aufheben (verhindert stuck nach Reconciliation)
		if (_isAttacking && !_anim.Animation.ToString().Contains("attack"))
			CancelAttack();

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

	// ---- Rüstungs-Layer -------------------------------------------------

	// Koppelt alle sichtbaren Rüstungs-Layer an Animation + Frame der Base.
	private void SyncVisualLayers()
	{
		if (_anim == null) return;
		MirrorLayer(_faceLayer);
		MirrorLayer(_eyesLayer);
		MirrorLayer(_hairLayer);
		MirrorLayer(_helmLayer);
		MirrorLayer(_armorLayer);
		MirrorLayer(_bootsLayer);
	}

	private void MirrorLayer(AnimatedSprite2D layer)
	{
		if (layer == null || !layer.Visible || layer.SpriteFrames == null) return;
		string a = _anim.Animation;
		if (!layer.SpriteFrames.HasAnimation(a)) return;
		if (layer.Animation != a) layer.Animation = a;
		int f = _anim.Frame;
		if (layer.Frame != f) layer.Frame = f;
	}

	// Legt einen Rüstungs-Layer an (baut/holt SpriteFrames aus ItemData) oder blendet ihn aus.
	public void SetAppearanceFromUi(string hairStyle, string eyeStyle, string faceStyle)
	{
		if (!IsValidAppearance(hairStyle, eyeStyle, faceStyle)) return;

		// Die lokale Vorschau reagiert sofort; der Server validiert und repliziert anschließend.
		ApplyAppearanceInternal(hairStyle, eyeStyle, faceStyle);

		if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
		{
			SyncHairStyle = _currentHairStyle;
			SyncEyeStyle = _currentEyeStyle;
			SyncFaceStyle = _currentFaceStyle;
			return;
		}

		RpcId(1, MethodName.RequestAppearanceRpc, hairStyle, eyeStyle, faceStyle);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
		TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RequestAppearanceRpc(string hairStyle, string eyeStyle, string faceStyle)
	{
		if (!Multiplayer.IsServer()) return;
		if (Multiplayer.GetRemoteSenderId() != GetMultiplayerAuthority()) return;
		if (!IsValidAppearance(hairStyle, eyeStyle, faceStyle)) return;

		ApplyAppearanceInternal(hairStyle, eyeStyle, faceStyle);
		SyncHairStyle = _currentHairStyle;
		SyncEyeStyle = _currentEyeStyle;
		SyncFaceStyle = _currentFaceStyle;
	}

	private static bool IsValidAppearance(string hairStyle, string eyeStyle, string faceStyle)
	{
		return Array.IndexOf(HairStyles, hairStyle) >= 0
			&& Array.IndexOf(EyeStyles, eyeStyle) >= 0
			&& Array.IndexOf(FaceStyles, faceStyle) >= 0;
	}

	private void ApplyAppearanceInternal(string hairStyle, string eyeStyle, string faceStyle)
	{
		if (!IsValidAppearance(hairStyle, eyeStyle, faceStyle)) return;

		_currentHairStyle = hairStyle;
		_currentEyeStyle = eyeStyle;
		_currentFaceStyle = faceStyle;

		ApplyStyleLayer(_faceLayer, "Face", faceStyle);
		ApplyStyleLayer(_eyesLayer, "Eyes", eyeStyle);
		ApplyStyleLayer(_hairLayer, "Hair", hairStyle);
	}

	private void ApplyStyleLayer(AnimatedSprite2D layer, string part, string variant)
	{
		if (layer == null) return;
		if (variant == "Standard")
		{
			layer.Visible = false;
			return;
		}

		var frames = GetStyleFrames(part, variant);
		if (frames == null)
		{
			layer.Visible = false;
			return;
		}

		layer.SpriteFrames = frames;
		layer.Visible = true;
		MirrorLayer(layer);
	}

	private void ApplyOrHideArmor(AnimatedSprite2D layer, EquipSlot slot, string itemPath)
	{
		if (layer == null) return;
		if (string.IsNullOrEmpty(itemPath))
		{
			layer.Visible = false;
			layer.SpriteFrames = null;
			return;
		}

		var data = GD.Load<ItemData>(itemPath);
		if (data == null || string.IsNullOrEmpty(data.ArmorMaterial))
		{
			layer.Visible = false;
			return;
		}

		var frames = GetStyleFrames(ArmorPartForSlot(slot), data.ArmorMaterial);
		if (frames == null) { layer.Visible = false; return; }

		layer.SpriteFrames = frames;
		layer.Visible = true;
		MirrorLayer(layer);
	}

	private static string ArmorPartForSlot(EquipSlot slot) => slot switch
	{
		EquipSlot.Helmet => "Helm",
		EquipSlot.Armor => "Chest",
		EquipSlot.Boots => "Boots",
		_ => ""
	};

	// Baut die Rüstungs-SpriteFrames aus der Base-SpriteFrames: gleiche Animationen +
	// Frame-Regionen, aber Textur = die Style-Sheets des Materials. Gecacht pro "Teil|Material".
	private SpriteFrames GetStyleFrames(string part, string variant)
	{
		string key = part + "|" + variant;
		if (_styleFramesCache.TryGetValue(key, out var cached)) return cached;

		var baseSf = _anim?.SpriteFrames;
		if (baseSf == null || string.IsNullOrEmpty(part)) return null;

		var sf = new SpriteFrames();
		foreach (string anim in baseSf.GetAnimationNames())
		{
			if (!sf.HasAnimation(anim)) sf.AddAnimation(anim);
			sf.SetAnimationLoop(anim, baseSf.GetAnimationLoop(anim));
			sf.SetAnimationSpeed(anim, baseSf.GetAnimationSpeed(anim));

			int count = baseSf.GetFrameCount(anim);
			for (int i = 0; i < count; i++)
			{
				if (baseSf.GetFrameTexture(anim, i) is not AtlasTexture baseTex) continue;
				string stylePath = StyleSheetPath(part, variant, baseTex.Atlas?.ResourcePath);
				if (string.IsNullOrEmpty(stylePath) || !ResourceLoader.Exists(stylePath)) continue;
				var atlas = new AtlasTexture
				{
					Atlas = GD.Load<Texture2D>(stylePath),
					Region = baseTex.Region
				};
				sf.AddFrame(anim, atlas, baseSf.GetFrameDuration(anim, i));
			}
		}
		if (sf.HasAnimation("default")) sf.RemoveAnimation("default");

		_styleFramesCache[key] = sf;
		return sf;
	}

	// .../new/man_lvl2_<Action>_with_shadow.png  ->  Style/<Part>/<Material>/<Part>_<Action>[_with_shadow].png
	private static string StyleSheetPath(string part, string variant, string baseAtlasPath)
	{
		if (string.IsNullOrEmpty(baseAtlasPath)) return "";
		string file = baseAtlasPath.GetFile();                              // man_lvl2_attack_with_shadow.png
		string action = file.Replace("man_lvl2_", "").Replace("_with_shadow.png", "");
		string dir = $"res://Assets/Charakter/Player/Style/{part}/{variant}";
		return part == "Boots"
			? $"{dir}/Boots_{action}_with_shadow.png"
			: $"{dir}/{part}_{action}.png";
	}

	private void ProcessServerInventoryAction(PlayerCmd cmd)
	{
		var action = (InvActionType)cmd.InvAction;
		switch (action)
		{
			case InvActionType.Swap:
			{
				var from = SlotAddress.FromIndexByte(cmd.InvSlotA);
				var to = SlotAddress.FromIndexByte(cmd.InvSlotB);
				var srcStack = Inventory.GetSlot(from);
				if (srcStack == null || srcStack.IsEmpty) break;
				if (srcStack.Data.ItemId != cmd.InvItemId) break;
				if (to.Type == SlotType.Equipment && !IsValidEquipForSlot(srcStack.Data, to.Equip))
					break;
				Inventory.SwapSlots(from, to);
				InventoryDirty = true;
				break;
			}
			case InvActionType.Drop:
			{
				var from = SlotAddress.FromIndexByte(cmd.InvSlotA);
				var stack = Inventory.GetSlot(from);
				if (stack == null || stack.IsEmpty || stack.Data.ItemId != cmd.InvItemId) break;
				int count = Math.Min(cmd.InvCount > 0 ? cmd.InvCount : stack.Count, stack.Count);
				var data = stack.Data;
				Inventory.RemoveFromSlot(from, count);
				DropToGround(data, count);
				InventoryDirty = true;
				break;
			}
		}
	}

	private static bool IsValidEquipForSlot(ItemData item, EquipSlot slot)
	{
		if (item.Slot == slot) return true;
		if (item.Category == ItemCategory.Ring && (slot == EquipSlot.Ring1 || slot == EquipSlot.Ring2))
			return true;
		return false;
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
            Vector2 rewindPos = mob.Position;

            bool valid = _currentWeapon.Hitbox != null
                         && _currentWeapon.Hitbox.OverlapsBody(mob);

            mob.ApplyState(saved);

            PredictionDebug.Instance?.ShowMobRollback(saved.Position, rewindPos, valid);

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
        if (scene == null)
        {
            GD.PrintErr($"Offhand-Szene nicht ladbar: {scenePath}");
            return;
        }
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
    // Öffentlich: wirft ein Item (per ItemData) auf den Boden — spawnt die DroppedScene für alle.
    public void DropToGround(ItemData data, int count = 1)
    {
        if (data == null || string.IsNullOrEmpty(data.DroppedScenePath)) return;
        DropItem(GD.Load<PackedScene>(data.DroppedScenePath), count);
    }

    private void DropItem(PackedScene scene, int count = 1)
    {
        if (scene == null) return;
        Rpc(MethodName.DropItemRpc, scene.ResourcePath, count);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DropItemRpc(string scenePath, int amount)
    {
        var scene = GD.Load<PackedScene>(scenePath);
        var instance = scene.Instantiate<Node2D>();
        instance.Position = GlobalPosition;
        instance.RotationDegrees = 45f;
        if (instance is PickupItem pi) pi.AmountOverride = amount;   // Rest-Anzahl mitführen
        GetParent().AddChild(instance);
    }
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void SyncInventoryState(byte[] inventoryData)
	{
		if (!IsMultiplayerAuthority()) return;
		var serverInv = new PlayerInventory();
		serverInv.Deserialize(inventoryData);
		Inventory.CopyFrom(serverInv);
	}

    private void UpdateWeaponZIndex(string dirName)
    {
        if (_weaponPivotNode == null) return;

        // z_index bleibt 0 (Spieler-Ebene) -> korrekte Tiefe gegenueber der Welt (Baeume etc.).
        // Vorne/Hinten wird ueber die Baum-Reihenfolge gesteuert, damit die Waffe VOR den
        // Ruestungs-Layern (Boots/Chest/Helm) liegt, ohne ueber Welt-Objekte zu rutschen.
        _weaponPivotNode.ZIndex = 0;

        bool inFront = _moveState == MoveState.Idle && dirName == "down" || dirName == "right";
        var parent = _weaponPivotNode.GetParent();

        if (inFront)
        {
            // hinter den letzten Koerper-/Ruestungs-Layer setzen -> Waffe liegt davor
            int target = _anim.GetIndex();
            foreach (Node l in new Node[] { _bootsLayer, _armorLayer, _helmLayer, _hairLayer, _faceLayer, _eyesLayer })
                if (l != null && l.GetIndex() > target) target = l.GetIndex();
            if (_weaponPivotNode.GetIndex() < target)
                parent.MoveChild(_weaponPivotNode, target);
        }
        else
        {
            // vor die Base Animation -> hinter dem Koerper
            if (_weaponPivotNode.GetIndex() > _anim.GetIndex())
                parent.MoveChild(_weaponPivotNode, System.Math.Max(0, _anim.GetIndex()));
        }
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
