using Godot;

namespace RPG2d.UI.Settings;

public partial class BarSlider : TextureProgressBar
{
    private bool _dragging = false;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                _dragging = mb.Pressed;
                if (mb.Pressed)
                    UpdateValue(mb.Position.X);
            }
        }
        else if (@event is InputEventMouseMotion mm && _dragging)
        {
            UpdateValue(mm.Position.X);
        }
    }

    private void UpdateValue(float mouseX)
    {
        float ratio = Mathf.Clamp(mouseX / Size.X, 0f, 1f);
        Value = MinValue + ratio * (MaxValue - MinValue);
    }
}
