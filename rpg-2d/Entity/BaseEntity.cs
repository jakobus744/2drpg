using Godot;

namespace RPG2d.Entity;

public partial class BaseEntity : Node2D
{
    public struct State
    {
        [NetVar(Tolerance = 1f)]
        public Vector2 Position;
    
        [NetVar(Tolerance = 1f)]
        public Vector2 Velocity;
    }

    public NetworkStateBuffer<State> StateBuffer = new();
    
    public virtual void ApplyServerState(uint tick, State serverState)
    {
        StateBuffer.Set(tick, serverState);
        Position = serverState.Position;
    }
}