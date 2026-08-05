using Godot;
using RPG2d.World.Generation.Data;

namespace RPG2d.World.Generation.Logic;

[Tool]
public partial class ZoneBackground : Node2D
{
    [Export] public ZoneSettings Settings { get; set; }
    [Export] public int ZoneSize { get; set; } = 3424;
    [Export] public bool UseGradient { get; set; } = true;
    [Export] public Vector2I ZoneCoord { get; set; } = Vector2I.Zero;

    public override void _Ready()
    {
        ZIndex = -100;
    }

    public void Setup(ZoneSettings settings, int zoneSize, Vector2I zoneCoord, bool useGradient)
    {
        Settings = settings;
        ZoneSize = zoneSize;
        ZoneCoord = zoneCoord;
        UseGradient = useGradient;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float half = ZoneSize / 2f;
        Color centerColor = Settings != null && Settings.PrimaryColor.A > 0 
            ? Settings.PrimaryColor 
            : WorldManager.GetZonePrimaryColor(ZoneCoord);

        Color secondaryColor = Settings != null && Settings.SecondaryColor.A > 0
            ? Settings.SecondaryColor
            : centerColor;

        if (!UseGradient)
        {
            DrawRect(new Rect2(-half, -half, ZoneSize, ZoneSize), centerColor, filled: true);
            return;
        }

        Color topColor = GetEdgeColor(ZoneCoord + Vector2I.Up, centerColor, secondaryColor);
        Color botColor = GetEdgeColor(ZoneCoord + Vector2I.Down, centerColor, secondaryColor);
        Color leftColor = GetEdgeColor(ZoneCoord + Vector2I.Left, centerColor, secondaryColor);
        Color rightColor = GetEdgeColor(ZoneCoord + Vector2I.Right, centerColor, secondaryColor);

        Color topLeftColor = Blend4(centerColor, topColor, leftColor, GetEdgeColor(ZoneCoord + Vector2I.Up + Vector2I.Left, centerColor, secondaryColor));
        Color topRightColor = Blend4(centerColor, topColor, rightColor, GetEdgeColor(ZoneCoord + Vector2I.Up + Vector2I.Right, centerColor, secondaryColor));
        Color botLeftColor = Blend4(centerColor, botColor, leftColor, GetEdgeColor(ZoneCoord + Vector2I.Down + Vector2I.Left, centerColor, secondaryColor));
        Color botRightColor = Blend4(centerColor, botColor, rightColor, GetEdgeColor(ZoneCoord + Vector2I.Down + Vector2I.Right, centerColor, secondaryColor));

        // 3x3 Control Points
        Vector2 pTL = new(-half, -half);
        Vector2 pTM = new(0, -half);
        Vector2 pTR = new(half, -half);

        Vector2 pML = new(-half, 0);
        Vector2 pC  = new(0, 0);
        Vector2 pMR = new(half, 0);

        Vector2 pBL = new(-half, half);
        Vector2 pBM = new(0, half);
        Vector2 pBR = new(half, half);

        // Draw 4 quad sectors using triangles
        DrawQuad(pTL, pTM, pC, pML, topLeftColor, topColor, centerColor, leftColor);
        DrawQuad(pTM, pTR, pMR, pC, topColor, topRightColor, rightColor, centerColor);
        DrawQuad(pML, pC, pBM, pBL, leftColor, centerColor, botColor, botLeftColor);
        DrawQuad(pC, pMR, pBR, pBM, centerColor, rightColor, botRightColor, botColor);
    }

    private void DrawQuad(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, Color c1, Color c2, Color c3, Color c4)
    {
        Vector2[] points1 = { p1, p2, p3 };
        Color[] colors1 = { c1, c2, c3 };
        DrawPolygon(points1, colors1);

        Vector2[] points2 = { p1, p3, p4 };
        Color[] colors2 = { c1, c3, c4 };
        DrawPolygon(points2, colors2);
    }

    private Color GetEdgeColor(Vector2I neighborCoord, Color centerColor, Color secondaryColor)
    {
        Color neighborColor = WorldManager.GetZonePrimaryColor(neighborCoord);
        if (neighborColor == centerColor)
        {
            return centerColor.Lerp(secondaryColor, 0.5f);
        }
        return centerColor.Lerp(neighborColor, 0.5f);
    }

    private static Color Blend(Color c1, Color c2) => c1.Lerp(c2, 0.5f);

    private static Color Blend4(Color c1, Color c2, Color c3, Color c4)
    {
        return new Color(
            (c1.R + c2.R + c3.R + c4.R) * 0.25f,
            (c1.G + c2.G + c3.G + c4.G) * 0.25f,
            (c1.B + c2.B + c3.B + c4.B) * 0.25f,
            (c1.A + c2.A + c3.A + c4.A) * 0.25f
        );
    }
}
