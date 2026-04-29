using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RPG2d.Player;

public partial class PlayerInput : Node
{
    private readonly List<string> _pressedDirections = [];
    private uint _currentTick = 0;
    private uint _lastTickAcknowledged = 0;
    private const int BufferSize = 128;
    private readonly PlayerCmd[] _commandBuffer = new PlayerCmd[BufferSize];
    private readonly PlayerState[] _stateBuffer = new PlayerState[BufferSize];

    private readonly Queue<PlayerCmd> _serverCommandQueue = new Queue<PlayerCmd>();

    private PredictionDebug _debugDrawer;

    public override void _Ready()
    {
        if (IsMultiplayerAuthority())
        {
            _debugDrawer = new PredictionDebug();
            AddChild(_debugDrawer);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsMultiplayerAuthority())
        {
            ++_currentTick;

            var cmd = BuildPlayerCommand();
            cmd.Tick = _currentTick;

            // Command speichern, Verarbeiten und Ergebnis speichern.
            SetCommand(cmd.Tick, cmd);
            var state = GetParent<Player>().ProcessCommand(GetState(_currentTick - 1), cmd);
            SetState(_currentTick, state);
            
            if (!Multiplayer.IsServer())
                RpcId(1, MethodName.ReceiveCommand, cmd.ToBytes());
        }
        else if (Multiplayer.IsServer())
        {
            PlayerState state = null;
            while (_serverCommandQueue.Count > 0)
            {
                var cmd = _serverCommandQueue.Dequeue();
                _currentTick = cmd.Tick;

                state = GetParent<Player>().ProcessCommand(GetState(_currentTick - 1), cmd);
                SetState(_currentTick, state);
            }

            if (state != null)
            {
                RpcId(Multiplayer.GetRemoteSenderId(), MethodName.ReceivePlayerState, _currentTick, state.ToBytes());
            }
        }
    }

    public PlayerState GetState(uint tick)
    {
        return _stateBuffer[tick % BufferSize];
    }

    public void SetState(uint tick, PlayerState state)
    {
        _stateBuffer[tick % BufferSize] = state;
    }
    
    public PlayerCmd GetCommand(uint tick)
    {
        return _commandBuffer[tick % BufferSize];
    }

    public void SetCommand(uint tick, PlayerCmd cmd)
    {
        _commandBuffer[tick % BufferSize] = cmd;
    }

    private PlayerCmd BuildPlayerCommand()
    {
        var playerCmd = new PlayerCmd();

        if (Input.IsActionJustPressed("up")) AddDirection("up");
        if (Input.IsActionJustPressed("down")) AddDirection("down");
        if (Input.IsActionJustPressed("left")) AddDirection("left");
        if (Input.IsActionJustPressed("right")) AddDirection("right");

        if (Input.IsActionJustReleased("up")) RemoveDirection("up");
        if (Input.IsActionJustReleased("down")) RemoveDirection("down");
        if (Input.IsActionJustReleased("left")) RemoveDirection("left");
        if (Input.IsActionJustReleased("right")) RemoveDirection("right");

        // Combined für diagonales laufen
        playerCmd.MovementVector = GetCombinedMovementVector();

        // wir starten down
        if (_currentTick == 1)
        {
            playerCmd.FacingDirection = "down";
        }
        else
        {
            playerCmd.FacingDirection = _pressedDirections.Count > 0
                ? _pressedDirections.Last()
                : GetCommand(_currentTick - 1).FacingDirection;
        }

        playerCmd.IsRunPressed = Input.IsActionPressed("run");
        playerCmd.IsRollPressed = Input.IsActionJustPressed("roll");
        playerCmd.IsAttackPressed = Input.IsActionJustPressed("attack");

        return playerCmd;
    }

    private void AddDirection(string dir)
    {
        if (!_pressedDirections.Contains(dir)) _pressedDirections.Add(dir);
    }

    private void RemoveDirection(string dir) => _pressedDirections.Remove(dir);

    private Vector2 GetCombinedMovementVector()
    {
        var combined = _pressedDirections.Aggregate(Vector2.Zero, (current, dir) => current + dir switch
        {
            "up" => Vector2.Up,
            "down" => Vector2.Down,
            "left" => Vector2.Left,
            "right" => Vector2.Right,
            _ => Vector2.Zero,
        });
        return combined == Vector2.Zero ? Vector2.Zero : combined.Normalized();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveCommand(byte[] cmdData)
    {
        if (Multiplayer.GetRemoteSenderId() != int.Parse(GetParent().Name))
            return;

        var cmd = PlayerCmd.FromBytes(cmdData);
        _serverCommandQueue.Enqueue(cmd);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceivePlayerState(uint tickAcknowledged, byte[] stateData)
    {
        var state = PlayerState.FromBytes(stateData);

        // Wir haben bereits einen neueren State verarbeitet
        if (tickAcknowledged < _lastTickAcknowledged)
            return;

        // Vergleichen mit dem Server
        var predictedState = GetState(tickAcknowledged);

        var unacknowledgedPath = new List<Vector2>();
        for (var i = tickAcknowledged + 1; i <= _currentTick; i++)
        {
            if (GetState(i) != null)
            {
                unacknowledgedPath.Add(GetState(i).Position);
            }
        }

        _debugDrawer?.UpdateDebugData(state.Position, state.Velocity, predictedState.Position, unacknowledgedPath);
        if (state.Equals(predictedState))
            return;

        GD.Print($"Reprediction nötig! Server State weicht von Prediction ab. Tick: {tickAcknowledged}");
        GD.Print($"Server State: Pos({state.Position}), Vel({state.Velocity})");
        GD.Print($"Predicted State: Pos({predictedState.Position}), Vel({predictedState.Velocity})");
        _lastTickAcknowledged = tickAcknowledged;

        var player = GetParent<Player>();
        player.ApplyState(state);
        SetState(tickAcknowledged, state);
        
        for (var tick = tickAcknowledged + 1; tick <= _currentTick; ++tick)
        {
            var previousState = GetState(tick - 1);
            var cmd = GetCommand(tick);
            SetState(tick, player.ProcessCommand(previousState, cmd));
        }
    }
}