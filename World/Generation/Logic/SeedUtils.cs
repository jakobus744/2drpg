using Godot;

namespace RPG2d.World.Generation.Logic;

public static class SeedUtils
{
    public static int ParseSeed(string seedInput, int defaultSeed = 1337)
    {
        if (string.IsNullOrWhiteSpace(seedInput))
            return defaultSeed;

        if (int.TryParse(seedInput.Trim(), out int numericSeed))
            return numericSeed;

        return seedInput.Trim().GetHashCode();
    }

    public static int DeriveSeed(int baseSeed, Vector2I coord)
    {
        unchecked
        {
            int hash = baseSeed * 397;
            hash = (hash ^ coord.X) * 73856093;
            hash = (hash ^ coord.Y) * 19349663;
            return hash;
        }
    }

    public static int DeriveEdgeSeed(int baseSeed, Vector2I coordA, Vector2I coordB)
    {
        unchecked
        {
            Vector2I minCoord = (coordA.X < coordB.X || (coordA.X == coordB.X && coordA.Y < coordB.Y)) ? coordA : coordB;
            Vector2I maxCoord = (minCoord == coordA) ? coordB : coordA;

            int hash = baseSeed * 397;
            hash = (hash ^ minCoord.X) * 73856093;
            hash = (hash ^ minCoord.Y) * 19349663;
            hash = (hash ^ maxCoord.X) * 83492791;
            hash = (hash ^ maxCoord.Y) * 4256233;
            return hash;
        }
    }

    public static int GetSeedOffset(int edgeSeed, int margin, int halfSize)
    {
        int minOffset = -halfSize + margin;
        int maxOffset = halfSize - margin;
        int range = Mathf.Max(1, maxOffset - minOffset + 1);

        int positiveHash = Mathf.Abs(edgeSeed);
        return minOffset + (positiveHash % range);
    }

    public static Vector2 DeriveOffset2D(int seed, Vector2I p1, Vector2I p2, float maxMagnitude)
    {
        unchecked
        {
            int h = seed * 397 ^ p1.X * 73856093 ^ p1.Y * 19349663 ^ p2.X * 83492791 ^ p2.Y * 4256233;
            float angle = Mathf.Abs(h % 360) * (Mathf.Pi / 180f);
            float mag = (Mathf.Abs(h / 360) % 1000) / 1000f * maxMagnitude;
            return new Vector2(Mathf.Cos(angle) * mag, Mathf.Sin(angle) * mag);
        }
    }
}
