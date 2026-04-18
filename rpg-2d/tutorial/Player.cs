using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Player : CharacterBody2D
{
    // ── Geschwindigkeit ───────────────────────────────────────
    private const float SpeedWalk  = 70f;
    private const float SpeedRun   = 100f;

    public enum MoveState { Idle, Walk, Run }

    // ── Zustand ───────────────────────────────────────────────
    private MoveState _moveState     = MoveState.Idle;
    private MoveState _lastMoveState = MoveState.Idle;
    private Vector2   _facingDirection = Vector2.Down;
    private bool      _isAttacking  = false;
    private bool      _isRolling   = false;

    // Letzte-Taste-gewinnt Input
    private string       _activeDirection   = "";
    private List<string> _pressedDirections = new();

    // ── Nodes ─────────────────────────────────────────────────
    private AnimatedSprite2D _anim;

    // ═════════════════════════════════════════════════════════
    // _Ready
    // ═════════════════════════════════════════════════════════
    public override void _Ready()
    {
        _anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _anim.AnimationFinished += OnAnimationFinished;
    }

    // ═════════════════════════════════════════════════════════
    // Hauptschleife
    // ═════════════════════════════════════════════════════════
    public override void _PhysicsProcess(double delta)
    {
        HandleDirectionInput();
        HandleAttackInput();
        HandleRollInput();
        HandleMovement();
        UpdateAnimation();
        MoveAndSlide();
    }

    // ═════════════════════════════════════════════════════════
    // Input / Bewegung
    // ═════════════════════════════════════════════════════════
    private void HandleDirectionInput()
    {
        if (Input.IsActionJustPressed("up"))    AddDirection("up");
        if (Input.IsActionJustPressed("down"))  AddDirection("down");
        if (Input.IsActionJustPressed("left"))  AddDirection("left");
        if (Input.IsActionJustPressed("right")) AddDirection("right");

        if (Input.IsActionJustReleased("up"))    RemoveDirection("up");
        if (Input.IsActionJustReleased("down"))  RemoveDirection("down");
        if (Input.IsActionJustReleased("left"))  RemoveDirection("left");
        if (Input.IsActionJustReleased("right")) RemoveDirection("right");

        _activeDirection = _pressedDirections.Count > 0
            ? _pressedDirections.Last()
            : "";
    }

    private void AddDirection(string dir)
    {
        if (!_pressedDirections.Contains(dir))
            _pressedDirections.Add(dir);
    }

    private void RemoveDirection(string dir) => _pressedDirections.Remove(dir);

    private void HandleAttackInput()
    {
        if (Input.IsActionJustPressed("attack"))
            StartAttack();
    }

    private void HandleRollInput()
    {
        if (Input.IsActionJustPressed("roll"))
            StartRoll();
    }

    private void StartRoll()
    {
        if (_isRolling || _isAttacking) return;
        string animName = "roll_" + GetDirectionName();
        if (!_anim.SpriteFrames.HasAnimation(animName))
        {
            GD.Print($"[Roll] Animation '{animName}' nicht gefunden!");
            return;
        }
        _isRolling = true;
        _anim.Play(animName);
    }

    private void StartAttack()
    {
        if (_isAttacking) return;
        _isAttacking = true;
        _anim.Play(GetAttackAnimationName(GetDirectionName()));
    }

    private string GetAttackAnimationName(string dir) => _moveState switch
    {
        MoveState.Run  => "run_attack_"  + dir,
        MoveState.Walk => "walk_attack_" + dir,
        _              => "attack_"      + dir,
    };

    private void HandleMovement()
    {
        Vector2 inputDir  = GetCombinedMovementVector();
        bool    isRunning = Input.IsActionPressed("run");

        if (inputDir == Vector2.Zero)
        {
            _lastMoveState = _moveState;
            _moveState     = MoveState.Idle;
            Velocity       = Vector2.Zero;
        }
        else
        {
            // Facing bleibt auf letzter gedrückter Taste (für Animation)
            _facingDirection = DirectionStringToVector(_activeDirection);
            _moveState       = isRunning ? MoveState.Run  : MoveState.Walk;
            Velocity         = inputDir  * (isRunning ? SpeedRun : SpeedWalk);
        }
    }

    private Vector2 GetCombinedMovementVector()
    {
        Vector2 combined = Vector2.Zero;
        foreach (string dir in _pressedDirections)
            combined += DirectionStringToVector(dir);
        return combined == Vector2.Zero ? Vector2.Zero : combined.Normalized();
    }

    private Vector2 DirectionStringToVector(string dir) => dir switch
    {
        "up"    => Vector2.Up,
        "down"  => Vector2.Down,
        "left"  => Vector2.Left,
        "right" => Vector2.Right,
        _       => Vector2.Zero,
    };

    // ═════════════════════════════════════════════════════════
    // Animation
    // ═════════════════════════════════════════════════════════
    private void UpdateAnimation()
    {
        if (_isAttacking || _isRolling) return;

        string dir = GetDirectionName();
        switch (_moveState)
        {
            case MoveState.Idle: PlayIdleAnimation(dir); break;
            case MoveState.Walk: _anim.Play("walk_" + dir); break;
            case MoveState.Run:  _anim.Play("run_"  + dir); break;
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

    private string GetDirectionName()
    {
        if (_facingDirection == Vector2.Right) return "right";
        if (_facingDirection == Vector2.Left)  return "left";
        if (_facingDirection == Vector2.Up)    return "up";
        return "down";
    }

    private void OnAnimationFinished()
    {
        if (_anim.Animation.ToString().Contains("attack"))
            _isAttacking = false;
        if (_anim.Animation.ToString().Contains("roll"))
            _isRolling = false;
    }
}
