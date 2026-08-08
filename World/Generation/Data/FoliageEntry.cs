using Godot;

namespace RPG2d.World.Generation.Data;

[GlobalClass]
public partial class FoliageEntry : Resource
{
    [Export] public string Name { get; set; } = "Tree";
    [Export] public PackedScene PrefabScene { get; set; }
    [Export] public int SourceId { get; set; } = 0;
    [Export] public Vector2I TileCoords { get; set; } = new(-1, -1);

    [Export(PropertyHint.Range, "0,1")] public float SpawnWeight { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0,1")] public float MinNoiseThreshold { get; set; } = 0.2f;
    [Export] public float ClearingRadius { get; set; } = 16f;
    private int _cachedTileRadius = -1;

    [ExportGroup("Climate Preferences")]
    [Export(PropertyHint.Range, "0,1")] public float IdealTemperature { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0.01,1")] public float TemperatureTolerance { get; set; } = 0.15f;
    [Export(PropertyHint.Range, "0,1")] public float IdealMoisture { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0.01,1")] public float MoistureTolerance { get; set; } = 0.15f;

    public float CalculateSuitability(float temp, float moisture)
    {
        float tempTol = Mathf.Max(0.01f, TemperatureTolerance);
        float moistTol = Mathf.Max(0.01f, MoistureTolerance);

        float dTempAbs = Mathf.Abs(temp - IdealTemperature);
        float dMoistAbs = Mathf.Abs(moisture - IdealMoisture);

        // Strict cutoff outside 1.5x tolerance window to prevent improper biome bleeding
        if (dTempAbs > tempTol * 1.5f || dMoistAbs > moistTol * 1.5f)
        {
            return 0f;
        }

        float dTemp = dTempAbs / tempTol;
        float dMoist = dMoistAbs / moistTol;

        return Mathf.Exp(-0.5f * (dTemp * dTemp + dMoist * dMoist));
    }

    public int GetClearingTileRadius(TileMapLayer groundLayer = null)
    {
        if (_cachedTileRadius > 0) return _cachedTileRadius;

        if (ClearingRadius > 0f)
        {
            _cachedTileRadius = Mathf.Clamp(Mathf.CeilToInt(ClearingRadius / 32f), 1, 6);
            return _cachedTileRadius;
        }

        int tileSize = groundLayer?.TileSet != null ? groundLayer.TileSet.TileSize.X : 16;
        float maxDimensionPx = 32f;

        if (PrefabScene != null)
        {
            var tempInstance = PrefabScene.Instantiate();
            if (tempInstance != null)
            {
                var sprite = tempInstance.FindChild("*Sprite*", recursive: true, owned: false);
                if (sprite is Sprite2D s2D && s2D.Texture != null)
                {
                    Vector2 sz = s2D.Texture.GetSize() * s2D.Scale;
                    maxDimensionPx = Mathf.Max(sz.X, sz.Y);
                }
                else if (sprite is AnimatedSprite2D anim && anim.SpriteFrames != null)
                {
                    string animName = !string.IsNullOrEmpty(anim.Autoplay) ? anim.Autoplay : (anim.SpriteFrames.GetAnimationNames().Length > 0 ? anim.SpriteFrames.GetAnimationNames()[0] : "");
                    if (!string.IsNullOrEmpty(animName))
                    {
                        Texture2D frameTex = anim.SpriteFrames.GetFrameTexture(animName, 0);
                        if (frameTex != null)
                        {
                            Vector2 sz = frameTex.GetSize() * anim.Scale;
                            maxDimensionPx = Mathf.Max(sz.X, sz.Y);
                        }
                    }
                }

                tempInstance.Free();
            }
        }

        _cachedTileRadius = Mathf.Clamp(Mathf.CeilToInt(maxDimensionPx / (2f * tileSize)), 1, 6);
        return _cachedTileRadius;
    }
}


