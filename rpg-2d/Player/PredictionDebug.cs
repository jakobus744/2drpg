using Godot;
using System.Collections.Generic;

namespace RPG2d.Player;

public partial class PredictionDebug : Node2D
{
    public static PredictionDebug Instance { get; private set; }

    private Vector2 _serverPosition;
    private Vector2 _serverVelocity;
    private Vector2 _historicalPredictedPosition;
    private Vector2 _predictedVel;
    private readonly List<Vector2> _unacknowledgedPath = new();

    private bool _hasData = false;
    private bool _showDebug = true;

    // rollback lag-compensation visualization
    private struct RollbackEntry
    {
        public Vector2 CurrentPos;
        public Vector2 RewindPos;
        public bool HitValid;
        public float Age;
    }

    private readonly List<RollbackEntry> _rollbackEntries = new();
    private const float RollbackFadeTime = 1.0f;

    public override void _Ready()
    {
        Instance = this;
        TopLevel = true;
        ZIndex = 100;
        SetProcessInput(true);
        SetProcess(true);
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.F3)
            {
                _showDebug = !_showDebug;
                QueueRedraw();
            }
        }
    }

    public override void _Process(double delta)
    {
        bool anyAlive = false;
        for (int i = _rollbackEntries.Count - 1; i >= 0; i--)
        {
            var entry = _rollbackEntries[i];
            entry.Age += (float)delta;
            if (entry.Age > RollbackFadeTime)
                _rollbackEntries.RemoveAt(i);
            else
            {
                _rollbackEntries[i] = entry;
                anyAlive = true;
            }
        }

        if (anyAlive)
            QueueRedraw();
    }

    public void UpdateDebugData(Vector2 serverPos, Vector2 serverVel, Vector2 historicalPos, Vector2 predictedVel,
        IEnumerable<Vector2> path)
    {
        _serverPosition = serverPos;
        _serverVelocity = serverVel;
        _historicalPredictedPosition = historicalPos;
        _predictedVel = predictedVel;

        _unacknowledgedPath.Clear();
        _unacknowledgedPath.AddRange(path);

        _hasData = true;

        if (_showDebug) QueueRedraw();
    }

    public void ShowMobRollback(Vector2 currentPos, Vector2 rewindPos, bool hitValid)
    {
        _rollbackEntries.Add(new RollbackEntry
        {
            CurrentPos = currentPos,
            RewindPos = rewindPos,
            HitValid = hitValid,
            Age = 0f
        });
        if (_showDebug) QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_showDebug) return;

        DrawPrediction();
        DrawRollbacks();
    }

    private void DrawPrediction()
    {
        if (!_hasData) return;

        DrawLine(_historicalPredictedPosition, _serverPosition, new Color(1f, 0f, 1f, 1f), 2f);

        DrawCircle(_historicalPredictedPosition, 4f, new Color(1f, 1f, 0f, 1f));

        if (_unacknowledgedPath.Count > 1)
        {
            for (int i = 0; i < _unacknowledgedPath.Count - 1; i++)
            {
                DrawLine(_unacknowledgedPath[i], _unacknowledgedPath[i + 1], new Color(1f, 1f, 0f, 0.5f), 2f);
            }
        }

        if (_predictedVel != Vector2.Zero)
        {
            DrawLine(_historicalPredictedPosition, _historicalPredictedPosition + _predictedVel * 0.2f,
                new Color(1f, 1f, 0f, 0.5f), 2f);
        }

        DrawCircle(_serverPosition, 12f, new Color(0f, 1f, 0f, 0.5f));
        if (_serverVelocity != Vector2.Zero)
        {
            DrawLine(_serverPosition, _serverPosition + _serverVelocity * 0.2f, new Color(0f, 1f, 0f, 0.5f), 2f);
        }
    }

    private void DrawRollbacks()
    {
        foreach (var entry in _rollbackEntries)
        {
            float alpha = 1f - entry.Age / RollbackFadeTime;
            Color color = entry.HitValid
                ? new Color(0f, 1f, 0f, alpha)
                : new Color(1f, 0f, 0f, alpha);

            DrawLine(entry.CurrentPos, entry.RewindPos, color, 2f);
            DrawCircle(entry.RewindPos, 8f, color);
            DrawCircle(entry.CurrentPos, 4f, new Color(color, 0.3f));
        }
    }
}