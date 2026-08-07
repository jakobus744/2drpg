using Godot;
using RPG2d.World.Generation.Data;

namespace RPG2d.World.Generation.Logic;

[Tool]
public partial class ZoneBackground : Node2D
{
    private const int GridResolution = 64;

    [Export] public ZoneSettings Settings { get; set; }
    [Export] public bool UseGradient { get; set; } = true;
    [Export] public Vector2I ZoneCoord { get; set; } = Vector2I.Zero;

    public Vector2 EffectiveZoneSize => WorldManager.GetZoneSize(GetEffectiveZoneCoord());

    private ImageTexture _texture;
    private Vector2I _renderedCoord = new(int.MinValue, int.MinValue);

    public override void _Ready()
    {
        ZIndex = -100;
        TextureFilter = TextureFilterEnum.Nearest;
    }

    public void Setup(ZoneSettings settings, Vector2 zoneSize, Vector2I zoneCoord, bool useGradient)
    {
        Settings = settings;
        ZoneCoord = zoneCoord;
        UseGradient = useGradient;
        RebuildTexture();
        QueueRedraw();
    }

    public void Setup(ZoneSettings settings, int zoneSize, Vector2I zoneCoord, bool useGradient)
    {
        Setup(settings, new Vector2(zoneSize, zoneSize), zoneCoord, useGradient);
    }

    public void RebuildTexture()
    {
        Vector2I currentCoord = GetEffectiveZoneCoord();
        _renderedCoord = currentCoord;

        Color centerColor = Settings != null && Settings.PrimaryColor.A > 0 
            ? Settings.PrimaryColor 
            : WorldManager.GetZonePrimaryColor(currentCoord);

        Color secondaryColor = Settings != null && Settings.SecondaryColor.A > 0
            ? Settings.SecondaryColor
            : centerColor;

        if (!UseGradient)
        {
            Image solidImage = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            solidImage.SetPixel(0, 0, centerColor);
            _texture = ImageTexture.CreateFromImage(solidImage);
            return;
        }

        Color topColor = GetEdgeColor(currentCoord + Vector2I.Up, centerColor, secondaryColor);
        Color botColor = GetEdgeColor(currentCoord + Vector2I.Down, centerColor, secondaryColor);
        Color leftColor = GetEdgeColor(currentCoord + Vector2I.Left, centerColor, secondaryColor);
        Color rightColor = GetEdgeColor(currentCoord + Vector2I.Right, centerColor, secondaryColor);

        Color topLeftColor = Blend4(centerColor, topColor, leftColor, GetEdgeColor(currentCoord + Vector2I.Up + Vector2I.Left, centerColor, secondaryColor));
        Color topRightColor = Blend4(centerColor, topColor, rightColor, GetEdgeColor(currentCoord + Vector2I.Up + Vector2I.Right, centerColor, secondaryColor));
        Color botLeftColor = Blend4(centerColor, botColor, leftColor, GetEdgeColor(currentCoord + Vector2I.Down + Vector2I.Left, centerColor, secondaryColor));
        Color botRightColor = Blend4(centerColor, botColor, rightColor, GetEdgeColor(currentCoord + Vector2I.Down + Vector2I.Right, centerColor, secondaryColor));

        Image image = Image.CreateEmpty(GridResolution, GridResolution, false, Image.Format.Rgba8);

        for (int y = 0; y < GridResolution; y++)
        {
            float v = (y + 0.5f) / GridResolution;
            for (int x = 0; x < GridResolution; x++)
            {
                float u = (x + 0.5f) / GridResolution;
                Color cellColor = SampleQuadColor(
                    u, v,
                    centerColor,
                    topLeftColor, topColor, topRightColor,
                    leftColor, rightColor,
                    botLeftColor, botColor, botRightColor
                );
                image.SetPixel(x, y, cellColor);
            }
        }

        _texture = ImageTexture.CreateFromImage(image);
    }

    public override void _Draw()
    {
        Vector2I currentCoord = GetEffectiveZoneCoord();
        if (_texture == null || _renderedCoord != currentCoord)
        {
            RebuildTexture();
        }

        if (_texture != null)
        {
            Vector2 size = EffectiveZoneSize;
            Vector2 half = size / 2f;
            DrawTextureRect(_texture, new Rect2(-half.X, -half.Y, size.X, size.Y), tile: false);
        }
    }

    private Vector2I GetEffectiveZoneCoord()
    {
        if (ZoneCoord != Vector2I.Zero) return ZoneCoord;
        if (WorldManager.Instance != null && IsInsideTree())
        {
            return WorldManager.WorldToZoneCell(GlobalPosition);
        }
        return Vector2I.Zero;
    }

    private static Color SampleQuadColor(
        float u, float v,
        Color center,
        Color tl, Color t, Color tr,
        Color l, Color r,
        Color bl, Color b, Color br)
    {
        if (v <= 0.5f)
        {
            float localV = v * 2f;
            if (u <= 0.5f)
            {
                float localU = u * 2f;
                Color topRow = tl.Lerp(t, localU);
                Color botRow = l.Lerp(center, localU);
                return topRow.Lerp(botRow, localV);
            }
            else
            {
                float localU = (u - 0.5f) * 2f;
                Color topRow = t.Lerp(tr, localU);
                Color botRow = center.Lerp(r, localU);
                return topRow.Lerp(botRow, localV);
            }
        }
        else
        {
            float localV = (v - 0.5f) * 2f;
            if (u <= 0.5f)
            {
                float localU = u * 2f;
                Color topRow = l.Lerp(center, localU);
                Color botRow = bl.Lerp(b, localU);
                return topRow.Lerp(botRow, localV);
            }
            else
            {
                float localU = (u - 0.5f) * 2f;
                Color topRow = center.Lerp(r, localU);
                Color botRow = b.Lerp(br, localU);
                return topRow.Lerp(botRow, localV);
            }
        }
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


