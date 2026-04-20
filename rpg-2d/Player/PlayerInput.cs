using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RPG2d.Player;

public partial class PlayerInput : Node
{
    private readonly List<string> _pressedDirections = [];
    private uint _currentTick = 0;
    private const int BufferSize = 128;
    private PlayerCmd[] _commandBuffer = new PlayerCmd[BufferSize];

    public override void _PhysicsProcess(double delta)
    {
        if (!IsMultiplayerAuthority())
            return;

        ++_currentTick;

        PlayerCmd cmd = BuildPlayerCommand();
        cmd.Tick = _currentTick;

        _commandBuffer[_currentTick % BufferSize] = cmd;

        GetParent<Player>().ProcessCommand(cmd);

        if (!Multiplayer.IsServer())
            RpcId(1, MethodName.ReceiveCommand, cmd.ToBytes());
    }

    public PlayerCmd GetCommand(uint tick)
    {
        return _commandBuffer[tick % BufferSize];
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

        PlayerCmd cmd = PlayerCmd.FromBytes(cmdData);
        GetParent<Player>().ProcessCommand(cmd);
    }
}