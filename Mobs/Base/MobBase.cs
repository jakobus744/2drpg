using System.Linq;
using Godot;
using RPG2d.Entity;
using RPG2d.GameManager;
using RPG2d.Player;
using RPG2d.World;

public abstract partial class MobBase : BaseEntity<MobState>
{
    private static int _lastPeerCount;
    private static ulong _peerChangeTick;
    private const ulong PeerStabilizeFrames = 3;

    private bool PeersStable()
    {
        int current = Multiplayer.GetPeers().Length;
        if (current != _lastPeerCount)
        {
            _lastPeerCount = current;
            _peerChangeTick = Engine.GetPhysicsFrames();
            return false;
        }

        return Engine.GetPhysicsFrames() - _peerChangeTick >= PeerStabilizeFrames;
    }

    [Export] public float MaxHealth { get; set; } = 100f;
    [Export] public float MoveSpeed { get; set; } = 60f;
    [Export] public float AttackDamage { get; set; } = 10f;

    [Export] public bool UsePathfinding { get; set; } = false;
    [Export] public float PathRecalcInterval { get; set; } = 0.5f;
    [Export] public float TargetReachedDistance { get; set; } = 10f;
    [Export] public float AvoidanceRadius { get; set; } = 0f;
    [Export] public float StuckTimeout { get; set; } = 1.0f;

    private Vector2 _syncPosition = Vector2.Zero;
    private string _syncAnimation = "";

    protected AnimatedSprite2D Sprite { get; private set; }
    protected NavigationAgent2D NavAgent { get; private set; }
    protected string FacingDirection { get; private set; } = "down";
    public float CurrentHealth { get; protected set; }
    public bool IsDead { get; protected set; }

    private bool _deathAnimPlaying;

    protected Vector2[] CurrentPath = System.Array.Empty<Vector2>();
    protected int CurrentPathIndex = 0;
    protected Vector2 Destination = Vector2.Zero;
    private float _pathRecalcTimer;
    private Vector2 _lastStuckPosition;
    private float _stuckTimer;

    public override void _Ready()
    {
        YSortEnabled = false;
        CurrentHealth = MaxHealth;
        _syncPosition = Position;

        Sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        NavAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");
        if (NavAgent == null)
        {
            NavAgent = new NavigationAgent2D();
            NavAgent.Name = "NavigationAgent2D";
            AddChild(NavAgent);
        }

        NavAgent.TargetDesiredDistance = TargetReachedDistance;
        NavAgent.PathDesiredDistance = 12f;
        if (AvoidanceRadius > 0f)
        {
            NavAgent.AvoidanceEnabled = true;
            NavAgent.Radius = AvoidanceRadius;
            NavAgent.VelocityComputed += OnVelocityComputed;
        }

        // Seed tick 0 so ProcessCommand-style reads don't fail if ever used
        StateBuffer.Set(0, new MobState
        {
            Position = Position,
            Health = MaxHealth,
            IsDead = false
        });

        PlayAnim("idle");
        Sprite.AnimationFinished += OnSpriteAnimFinished;
        OnReady();
    }

    protected virtual void OnReady()
    {
    }

    private void OnSpriteAnimFinished()
    {
        if (_deathAnimPlaying) return;
        if (!IsDead)
            PlayAnim("idle");
    }

    protected virtual void ProcessAI(double delta)
    {
    }

    protected virtual void OnHit(float amount)
    {
    }

    protected virtual void OnDeathAnimationFinished()
    {
        QueueFree();
    }

    public virtual void TakeDamage(float amount)
    {
        if (IsDead) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnHit(amount);

        if (CurrentHealth <= 0f)
            Die();
        else
            PlayAnim("hurt");
    }

    public virtual void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
    }

    public virtual void PlayAnim(string anim)
    {
        if (Sprite == null) return;

        string full = anim + "_" + FacingDirection;
        if (Sprite.SpriteFrames.HasAnimation(full))
            Sprite.Play(full);
        else if (Sprite.SpriteFrames.HasAnimation(anim))
            Sprite.Play(anim);
    }

    protected void UpdateFacingDirection(Vector2 velocity)
    {
        if (velocity.LengthSquared() < 0.01f) return;

        if (Mathf.Abs(velocity.X) > Mathf.Abs(velocity.Y))
            FacingDirection = velocity.X > 0 ? "right" : "left";
        else
            FacingDirection = velocity.Y > 0 ? "down" : "up";
    }

    protected static Vector2 DirToVec(string dir) => dir switch
    {
        "up" => Vector2.Up,
        "down" => Vector2.Down,
        "left" => Vector2.Left,
        "right" => Vector2.Right,
        _ => Vector2.Zero,
    };

    protected virtual void Die()
    {
        if (IsDead) return;
        IsDead = true;
        Velocity = Vector2.Zero;

        if (Multiplayer.HasMultiplayerPeer())
            Rpc(MethodName.DieRpc);
        else
            DieRpc();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DieRpc()
    {
        IsDead = true;
        _deathAnimPlaying = true;
        PlayAnim("death");
        Sprite.AnimationFinished += OnDeathAnimComplete;
    }

    private void OnDeathAnimComplete()
    {
        Sprite.AnimationFinished -= OnDeathAnimComplete;
        _deathAnimPlaying = false;
        OnDeathAnimationFinished();
    }

    public MobState GetStateAtTick(uint tick)
    {
        return StateBuffer.Get(tick);
    }

    public void ApplyState(MobState state)
    {
        Position = state.Position;
        Velocity = state.Velocity;
        CurrentHealth = state.Health;
        IsDead = state.IsDead;
    }

    public override void _PhysicsProcess(double delta)
    {
        bool isAuthority = !Multiplayer.HasMultiplayerPeer() || IsMultiplayerAuthority();

        if (isAuthority)
        {
            if (!IsDead)
            {
                CurrentTick++;
                ProcessAI(delta);
                MoveAndSlide();

                if (Velocity.LengthSquared() > 0.01f)
                    UpdateFacingDirection(Velocity);
            }

            // State history for lag compensation — indexed by global server tick
            StateBuffer.Set(GameManager.ServerTick, new MobState
            {
                Position = Position,
                Velocity = Velocity,
                Health = CurrentHealth,
                IsDead = IsDead
            });

            if (!Multiplayer.HasMultiplayerPeer() || !PeersStable()) return;
            var mobCell = WorldManager.WorldToZoneCell(GlobalPosition);

            var players = RPG2d.Player.Player.AllPlayers;
            foreach (var peerId in from p in players
                     where p != null && IsInstanceValid(p)
                     select p.GetMultiplayerAuthority()
                     into peerId
                     where (long)peerId > 1
                     where WorldManager.IsZoneLoadedForPeer(peerId, mobCell)
                     select peerId)
            {
                RpcId(peerId, MethodName.SyncStateRpc, Position, Sprite?.Animation ?? "");
            }
        }
        else
        {
            Position = Position.Lerp(_syncPosition, (float)delta * 15f);
            if (!string.IsNullOrEmpty(_syncAnimation) && Sprite != null)
                Sprite.Play(_syncAnimation);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false,
        TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void SyncStateRpc(Vector2 pos, string anim)
    {
        _syncPosition = pos;
        _syncAnimation = anim;
    }

    // Pathfinding helpers — call these from subclass ProcessAI() overrides.

    protected void SetDestination(Vector2 worldTarget)
    {
        Destination = worldTarget;
        if (NavAgent != null)
            NavAgent.TargetPosition = worldTarget;
    }

    protected void ClearDestination()
    {
        Destination = Vector2.Zero;
        Velocity = Vector2.Zero;
        if (NavAgent != null)
            NavAgent.TargetPosition = GlobalPosition;
    }

    protected bool MoveAlongPath(double delta)
    {
        if (Destination == Vector2.Zero)
        {
            Velocity = Vector2.Zero;
            return false;
        }

        float distToDestSq = GlobalPosition.DistanceSquaredTo(Destination);
        float stopDist = TargetReachedDistance > 0f ? TargetReachedDistance : 10f;

        if (distToDestSq <= stopDist * stopDist)
        {
            Velocity = Vector2.Zero;
            OnDestinationReached();
            return false;
        }

        _pathRecalcTimer += (float)delta;
        if (_pathRecalcTimer >= PathRecalcInterval || CurrentPath.Length == 0)
        {
            _pathRecalcTimer = 0f;
            CurrentPath = FindPathTo(Destination);
            CurrentPathIndex = 0;
        }

        if (CurrentPath.Length > 0 && CurrentPathIndex < CurrentPath.Length)
        {
            Vector2 targetWaypoint = CurrentPath[CurrentPathIndex];
            Vector2 toWaypoint = targetWaypoint - GlobalPosition;

            if (toWaypoint.LengthSquared() < 16f * 16f)
            {
                CurrentPathIndex++;
                if (CurrentPathIndex < CurrentPath.Length)
                {
                    targetWaypoint = CurrentPath[CurrentPathIndex];
                    toWaypoint = targetWaypoint - GlobalPosition;
                }
            }

            Vector2 direction = toWaypoint.Normalized();
            Vector2 targetVelocity = direction * MoveSpeed;

            if (NavAgent != null && NavAgent.AvoidanceEnabled)
            {
                NavAgent.Velocity = targetVelocity;
            }
            else
            {
                Velocity = targetVelocity;
            }

            return true;
        }

        Velocity = Vector2.Zero;
        return false;
    }

    private void OnVelocityComputed(Vector2 safeVelocity)
    {
        Velocity = safeVelocity;
    }

    protected Vector2[] FindPathTo(Vector2 worldTarget)
    {
        var nav = GetNodeOrNull<RPG2d.World.NavigationManager>("/root/NavigationManager");
        return nav?.FindPath(GlobalPosition, worldTarget) ?? System.Array.Empty<Vector2>();
    }

    protected bool HasReachedDestination()
    {
        if (Destination == Vector2.Zero || NavAgent == null) return true;
        return NavAgent.IsTargetReached();
    }

    protected Vector2 GetRandomWalkablePosition(Vector2 center, float radius)
    {
        var nav = GetNodeOrNull<RPG2d.World.NavigationManager>("/root/NavigationManager");
        if (nav == null) return center;
        var positions = nav.GetRandomWalkablePositions(center, radius, 1);
        return positions.Length > 0 ? positions[0] : center;
    }

    protected virtual void OnDestinationReached()
    {
    }

    protected virtual void OnPathfindingFailed()
    {
    }

    protected virtual void OnPathStuck()
    {
        if (Destination == Vector2.Zero || NavAgent == null) return;
        Vector2 jitter = new((float)GD.RandRange(-20f, 20f), (float)GD.RandRange(-20f, 20f));
        NavAgent.TargetPosition = Destination + jitter;
    }

    protected void CheckStuck()
    {
        if (!UsePathfinding || Destination == Vector2.Zero) return;

        if (GlobalPosition.DistanceSquaredTo(_lastStuckPosition) < 1f && Velocity.LengthSquared() > 0.1f)
        {
            _stuckTimer += (float)GetPhysicsProcessDeltaTime();
            if (_stuckTimer >= StuckTimeout)
            {
                OnPathStuck();
                _stuckTimer = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f;
            _lastStuckPosition = GlobalPosition;
        }
    }

    private Vector2 ComputeSeparationForce()
    {
        Vector2 separation = Vector2.Zero;
        int count = 0;

        var spaceState = GetWorld2D().DirectSpaceState;
        if (spaceState == null) return Vector2.Zero;

        var query = new PhysicsShapeQueryParameters2D();
        var circle = new CircleShape2D { Radius = AvoidanceRadius };
        query.Shape = circle;
        query.Transform = new Transform2D(0, GlobalPosition);
        query.CollideWithBodies = true;
        query.CollisionMask = 1;

        var results = spaceState.IntersectShape(query);
        foreach (var result in results)
        {
            var collider = result["collider"].AsGodotObject();
            if (collider == this) continue;
            if (collider is MobBase other && other.IsDead) continue;

            Vector2 otherPos = collider is Node2D n ? n.GlobalPosition : GlobalPosition;
            Vector2 away = GlobalPosition - otherPos;
            float dist = away.Length();
            if (dist > 0.01f && dist < AvoidanceRadius)
            {
                separation += away.Normalized() * (1f - dist / AvoidanceRadius);
                count++;
            }
        }

        return count > 0 ? separation / count * 0.3f : Vector2.Zero;
    }
}