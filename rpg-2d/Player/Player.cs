using System;
using Godot;

namespace RPG2d.Player;

public partial class Player : CharacterBody2D
{
    private const float SpeedWalk = 70f;
    private const float SpeedRun = 100f;

    private const float MaxHealth = 100f;
    private const float HealthRecovery = 1f;

    private const float MaxStamina = 100f;
    private const float StaminaRecovery = .5f;
    private const float MovingStaminaRecovery = .25f;
    private const float RollCost = 30f;
    private const float SprintCost = .25f;

    // Ab wann Sprint Speed weniger wird, evtl renamen?
    private const float SprintFalloff = 30f;

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

    public static Player LocalPlayer { get; private set; }
    
    public PlayerInput Input { get; private set; }

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
        {
            camera.Enabled = IsMultiplayerAuthority();
        }
        
        Input = GetNodeOrNull<PlayerInput>("Input");
        if (IsMultiplayerAuthority())
        {
            LocalPlayer = this;
        }
    }

    public override void _ExitTree()
    {
        if (LocalPlayer == this)
        {
            LocalPlayer = null;
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

    // Player Input verarbeiten, sowohl auf dem Server als auch auf dem Client
    public PlayerState ProcessCommand(PlayerState previousState, PlayerCmd cmd)
    {
        // Initial state holen setzen falls nicht vorhanden, sonst clonen
        var state = previousState?.Clone() ?? new PlayerState
        {
            Position = Position,
            Velocity = Velocity,
            Stamina = MaxStamina,
            Health = MaxHealth,
            LastHurtTime = 0f
        };

        if (_moveState == MoveState.Dead)
        {
            state.Velocity = Vector2.Zero;
            return state;
        }

        // 1. Aktionen verarbeiten
        if (cmd.IsAttackPressed && !IsActionLocked) StartAttack(cmd.FacingDirection);
        if (cmd.IsRollPressed && !IsActionLocked)
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
                // Speed increase ist stamina basiert
                if (state.Stamina <= SprintFalloff)
                {
                    wishSpeed += (SpeedRun - SpeedWalk) * (state.Stamina / SprintFalloff);
                }
                else
                {
                    wishSpeed = SpeedRun;
                }

                state.Stamina -= SprintCost;
            }
            else
            {
                state.Stamina += MovingStaminaRecovery;
            }

            Velocity = cmd.MovementVector * wishSpeed;
        }

        // @todo: Tod verarbeiten
        if (state.Health <= 0)
        {
            Die(cmd.FacingDirection);
        }
        else
        {
            state.Health += HealthRecovery;
        }

        state.Stamina = Math.Clamp(state.Stamina, 0f, MaxStamina);
        state.Health = Math.Clamp(state.Health, 0f, MaxHealth);

        // 3. Animation updaten und bewegen
        UpdateAnimation(cmd.FacingDirection);
        MoveAndSlide();

        state.Position = Position;
        state.Velocity = Velocity;

        // 4. State zurück geben
        return state;
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
        if (finishedAnimation.Contains("hurt")) _isHurt = false;
    }

    public void ApplyState(PlayerState state)
    {
        Position = state.Position;
        Velocity = state.Velocity;
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