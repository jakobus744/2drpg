using Godot;
using System.Collections.Generic;

namespace RPG2d.Player;

public partial class PredictionDebug : Node2D
{
    private Vector2 _serverPosition;
    private Vector2 _serverVelocity;
    private Vector2 _historicalPredictedPosition;
    private readonly List<Vector2> _unacknowledgedPath = new();
    
    private bool _hasData = false;
    private bool _showDebug = true;

    public override void _Ready()
    {
        TopLevel = true;
        ZIndex = 100;
        SetProcessInput(true);
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

    public void UpdateDebugData(Vector2 serverPos, Vector2 serverVel, Vector2 historicalPos, IEnumerable<Vector2> path)
    {
        _serverPosition = serverPos;
        _serverVelocity = serverVel;
        _historicalPredictedPosition = historicalPos;
        
        _unacknowledgedPath.Clear();
        _unacknowledgedPath.AddRange(path);
        
        _hasData = true;
        
        if (_showDebug) QueueRedraw(); 
    }

    public override void _Draw()
    {
        if (!_hasData || !_showDebug) return;

        // Magenta Fehler Linie
        DrawLine(_historicalPredictedPosition, _serverPosition, new Color(1f, 0f, 1f, 1f), 2f);
        DrawCircle(_historicalPredictedPosition, 4f, new Color(1f, 0f, 1f, 1f));

        // Predicted Pfad 
        if (_unacknowledgedPath.Count > 1)
        {
            for (int i = 0; i < _unacknowledgedPath.Count - 1; i++)
            {
                DrawLine(_unacknowledgedPath[i], _unacknowledgedPath[i + 1], new Color(1f, 1f, 0f, 0.5f), 2f);
            }
        }

        // Server Pos
        DrawCircle(_serverPosition, 12f, new Color(1f, 0f, 0f, 0.5f));
        DrawArc(_serverPosition, 12f, 0, Mathf.Tau, 32, new Color(1f, 0f, 0f, 1f), 2f);

        // Server Vel
        if (_serverVelocity != Vector2.Zero)
        {
            DrawLine(_serverPosition, _serverPosition + _serverVelocity * 0.2f, new Color(1f, 0.5f, 0f, 1f), 2f);
        }
    }
}