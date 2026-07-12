using Godot;
using RPG2d.Entity;
using RPG2d.GameManager;

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

    [Export] public float MaxHealth    { get; set; } = 100f;
    [Export] public float MoveSpeed    { get; set; } = 60f;
    [Export] public float AttackDamage { get; set; } = 10f;

    private Vector2 _syncPosition  = Vector2.Zero;
    private string  _syncAnimation = "";

    protected AnimatedSprite2D Sprite { get; private set; }
    protected string FacingDirection   { get; private set; } = "down";
    public float CurrentHealth         { get; protected set; }
    public bool  IsDead                { get; protected set; }

    private bool _deathAnimPlaying;

    public override void _Ready()
    {
        YSortEnabled = false;
        CurrentHealth = MaxHealth;

        Sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

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

    protected virtual void OnReady() { }

    private void OnSpriteAnimFinished()
    {
        if (_deathAnimPlaying) return;
        if (!IsDead)
            PlayAnim("idle");
    }

    protected virtual void ProcessAI(double delta) { }

    protected virtual void OnHit(float amount) { }

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
        "up"    => Vector2.Up,
        "down"  => Vector2.Down,
        "left"  => Vector2.Left,
        "right" => Vector2.Right,
        _       => Vector2.Zero,
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

            if (Multiplayer.HasMultiplayerPeer() && PeersStable())
                Rpc(MethodName.SyncStateRpc, Position, Sprite?.Animation ?? "");
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
}
