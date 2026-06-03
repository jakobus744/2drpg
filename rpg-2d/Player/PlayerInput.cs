using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RPG2d.World.Items.Data;

namespace RPG2d.Player;

public partial class PlayerInput : Node
{
    private readonly List<string> _pressedDirections = [];
    private const int BufferSize = 128;
    private readonly PlayerCmd[] _commandBuffer = new PlayerCmd[BufferSize];

    private readonly Queue<PlayerCmd> _serverCommandQueue = new Queue<PlayerCmd>();

    private PredictionDebug _debugDrawer;

    public uint CurrentTick = 0;
    public uint LastTickAcknowledged = 0;

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
            ++CurrentTick;

            var cmd = BuildPlayerCommand();
            cmd.Tick = CurrentTick;

            SetCommand(cmd.Tick, cmd);
            GetParent<Player>().ProcessCommand(cmd);

            if (!Multiplayer.IsServer())
                RpcId(1, MethodName.ReceiveCommand, cmd.ToBytes());
        }
        else if (Multiplayer.IsServer())
        {
            bool processed = false;
            while (_serverCommandQueue.Count > 0)
            {
                var cmd = _serverCommandQueue.Dequeue();
                CurrentTick = cmd.Tick;
                GetParent<Player>().ProcessCommand(cmd);
                processed = true;
            }

            if (processed)
            {
                var player = GetParent<Player>();
                var state = player.StateBuffer.Get(CurrentTick);
                RpcId(Multiplayer.GetRemoteSenderId(), MethodName.ReceivePlayerState,
                    CurrentTick, player.StateBuffer.ToBytes(state));
            }
        }
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

        playerCmd.MovementVector = GetCombinedMovementVector();

        if (CurrentTick == 1)
        {
            playerCmd.FacingDirection = "down";
        }
        else
        {
            playerCmd.FacingDirection = _pressedDirections.Count > 0
                ? _pressedDirections.Last()
                : GetCommand(CurrentTick - 1).FacingDirection;
        }

        playerCmd.IsRunPressed = Input.IsActionPressed("run");
        playerCmd.IsRollPressed = Input.IsActionJustPressed("roll");
        playerCmd.IsAttackPressed = Input.IsActionJustPressed("attack");
        playerCmd.IsInteractPressed = Input.IsActionJustPressed("interact");
        if (playerCmd.IsInteractPressed)
            playerCmd.InteractTargetPath = GetParent<Player>().NearbyPickupPath;

        // Ausrüstung aus den Equipment-Slots des Inventars (treibt die Hand-Optik)
        playerCmd.EquippedWeaponPath = GetEquipPath(EquipSlot.Weapon);
        playerCmd.EquippedOffhandPath = GetEquipPath(EquipSlot.Offhand);

        return playerCmd;
    }

    // Scene-Pfad des Items im Equipment-Slot (leer = nichts angelegt)
    private string GetEquipPath(EquipSlot slot)
    {
        var inv = GetParent<Player>().Inventory;
        var stack = inv?.EquipmentSlots.GetValueOrDefault(slot);
        return stack?.Data?.DroppedScenePath ?? "";
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
        var player = GetParent<Player>();
        var serverState = player.StateBuffer.FromBytes(stateData);

        if (tickAcknowledged < LastTickAcknowledged)
            return;

        var predictedState = player.StateBuffer.Get(tickAcknowledged);

        var unacknowledgedPath = new List<Vector2>();
        for (var i = tickAcknowledged + 1; i <= CurrentTick; i++)
        {
            unacknowledgedPath.Add(player.StateBuffer.Get(i).Position);
        }

        _debugDrawer?.UpdateDebugData(serverState.Position, serverState.Velocity,
            predictedState.Position, predictedState.Velocity, unacknowledgedPath);

        if (!player.StateBuffer.IsDesynced(serverState, predictedState))
            return;

        GD.Print($"Reprediction nötig! Server State weicht von Prediction ab. Tick: {tickAcknowledged}");
        GD.Print($"Server State: Pos({serverState.Position}), Vel({serverState.Velocity})");
        GD.Print($"Predicted State: Pos({predictedState.Position}), Vel({predictedState.Velocity})");
        LastTickAcknowledged = tickAcknowledged;

        player.ApplyServerState(tickAcknowledged, serverState);

        for (var tick = tickAcknowledged + 1; tick <= CurrentTick; ++tick)
        {
            var cmd = GetCommand(tick);
            player.ProcessCommand(cmd);
        }
    }
}
