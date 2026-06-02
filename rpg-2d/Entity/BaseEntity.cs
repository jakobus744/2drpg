using Godot;

namespace RPG2d.Entity;

public abstract partial class BaseEntity<TState> : CharacterBody2D where TState : struct
{
    public NetworkStateBuffer<TState> StateBuffer = new();
    public uint CurrentTick { get; protected set; }

    public virtual void ApplyServerState(uint tick, TState serverState)
    {
        StateBuffer.Set(tick, serverState);
    }
}
