using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Player : CharacterBody2D
{
    private const float SpeedWalk = 70.0f;
    private const float SpeedRun = 100.0f;

    public enum MoveState { Idle, Walk, Run }

    private MoveState _moveState = MoveState.Idle;
    private MoveState _lastMoveState = MoveState.Idle;

    private Vector2 _facingDirection = Vector2.Down;
    private bool _isAttacking = false;

    // Für "letzte Taste gewinnt"
    private string _activeDirection = "";
    private List<string> _pressedDirections = new List<string>();

    private AnimatedSprite2D _anim;

    public override void _Ready()
    {
        _anim = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        
        // In C# verbinden wir Signale meistens über Events (+=)
        _anim.AnimationFinished += OnAnimationFinished;
    }

    // HAUPTSCHLEIFE
    public override void _PhysicsProcess(double delta)
    {
        HandleDirectionInput();
        HandleAttackInput();
        HandleMovement();
        UpdateAnimation();
        MoveAndSlide();
    }

    // RICHTUNGS-INPUT (Letzte Taste gewinnt)
    private void HandleDirectionInput()
    {
        if (Input.IsActionJustPressed("up")) AddDirection("up");
        if (Input.IsActionJustPressed("down")) AddDirection("down");
        if (Input.IsActionJustPressed("left")) AddDirection("left");
        if (Input.IsActionJustPressed("right")) AddDirection("right");

        if (Input.IsActionJustReleased("up")) RemoveDirection("up");
        if (Input.IsActionJustReleased("down")) RemoveDirection("down");
        if (Input.IsActionJustReleased("left")) RemoveDirection("left");
        if (Input.IsActionJustReleased("right")) RemoveDirection("right");

        if (_pressedDirections.Count > 0)
        {
            // System.Linq erlaubt uns hier .Last() auf der Liste aufzurufen
            _activeDirection = _pressedDirections.Last();
        }
        else
        {
            _activeDirection = "";
        }
    }

    private void AddDirection(string dir)
    {
        if (!_pressedDirections.Contains(dir))
        {
            _pressedDirections.Add(dir);
        }
    }

    private void RemoveDirection(string dir)
    {
        _pressedDirections.Remove(dir);
    }

    // ATTACK-INPUT
    private void HandleAttackInput()
    {
        if (Input.IsActionJustPressed("attack"))
        {
            StartAttack();
        }
    }

    private void StartAttack()
    {
        if (_isAttacking) return;

        _isAttacking = true;

        string dirName = GetDirectionName();
        string attackAnim = GetAttackAnimationName(dirName);
        _anim.Play(attackAnim);
    }

    private string GetAttackAnimationName(string dirName)
    {
        switch (_moveState)
        {
            case MoveState.Run:
                return "run_attack_" + dirName;
            case MoveState.Walk:
                return "walk_attack_" + dirName;
            default:
                return "attack_" + dirName;
        }
    }

    // BEWEGUNG BERECHNEN
    private void HandleMovement()
    {
        Vector2 inputDir = DirectionStringToVector(_activeDirection);
        bool isRunning = Input.IsActionPressed("run");

        if (inputDir == Vector2.Zero)
        {
            _lastMoveState = _moveState;
            _moveState = MoveState.Idle;
            Velocity = Vector2.Zero;
        }
        else
        {
            _facingDirection = inputDir;

            if (isRunning)
            {
                _moveState = MoveState.Run;
                Velocity = inputDir * SpeedRun;
            }
            else
            {
                _moveState = MoveState.Walk;
                Velocity = inputDir * SpeedWalk;
            }
        }
    }

    private Vector2 DirectionStringToVector(string dir)
    {
        switch (dir)
        {
            case "up": return Vector2.Up;
            case "down": return Vector2.Down;
            case "left": return Vector2.Left;
            case "right": return Vector2.Right;
            default: return Vector2.Zero;
        }
    }

    // ANIMATION
    private void UpdateAnimation()
    {
        if (_isAttacking) return;

        string dirName = GetDirectionName();

        switch (_moveState)
        {
            case MoveState.Idle:
                PlayIdleAnimation(dirName);
                break;
            case MoveState.Walk:
                _anim.Play("walk_" + dirName);
                break;
            case MoveState.Run:
                _anim.Play("run_" + dirName);
                break;
        }
    }

    private void PlayIdleAnimation(string dirName)
    {
        if (_lastMoveState == MoveState.Run)
        {
            _anim.Play("idle_" + dirName + ".2");
        }
        else
        {
            string idleAnim = "idle_" + dirName + ".1";
            
            // In C# rufen wir SpriteFrames direkt als Eigenschaft ab
            if (!_anim.SpriteFrames.HasAnimation(idleAnim))
            {
                idleAnim = "idle_" + dirName;
            }
            _anim.Play(idleAnim);
        }
    }

    private string GetDirectionName()
    {
        if (_facingDirection == Vector2.Right) return "right";
        if (_facingDirection == Vector2.Left) return "left";
        if (_facingDirection == Vector2.Up) return "up";
        return "down";
    }

    // SIGNAL CALLBACKS
    private void OnAnimationFinished()
    {
        // .Animation gibt ein StringName Objekt zurück, .ToString() macht daraus einen C# String
        if (_anim.Animation.ToString().Contains("attack"))
        {
            _isAttacking = false;
        }
    }
}