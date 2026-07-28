using Godot;
using Godot.Collections;
using RPG2d.World.Generation.Data;

namespace RPG2d.World.Generation;

public static class PathGenerator
{
    public static System.Collections.Generic.HashSet<Vector2I> ConnectWaypoints(
        TileMapLayer layer,
        Vector2I start,
        Vector2I end,
        ZoneSettings settings = null,
        int terrainSet = 0,
        int terrain = 0,
        int pathWidth = 2,
        float roughness = 0.2f)
    {
        var pathCells = new System.Collections.Generic.HashSet<Vector2I>();
        var linePoints = GenerateCurvedLine(start, end, roughness);

        foreach (var point in linePoints)
        {
            int halfWidth = pathWidth / 2;
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                for (int dy = -halfWidth; dy <= halfWidth; dy++)
                {
                    pathCells.Add(point + new Vector2I(dx, dy));
                }
            }
        }

        if (layer != null && pathCells.Count > 0)
        {
            bool hasTerrains = layer.TileSet != null && layer.TileSet.GetTerrainSetsCount() > terrainSet;

            if (hasTerrains)
            {
                var godotArray = new Array<Vector2I>();
                foreach (var cell in pathCells)
                {
                    godotArray.Add(cell);
                }

                layer.SetCellsTerrainConnect(godotArray, terrainSet, terrain);
            }
            else
            {
                Vector2I pathCoords = settings != null ? settings.PathTileCoords : new Vector2I(1, 0);
                foreach (var cell in pathCells)
                {
                    layer.SetCell(cell, 0, pathCoords);
                }
            }
        }

        return pathCells;
    }

    private static System.Collections.Generic.List<Vector2I> GenerateCurvedLine(Vector2I start, Vector2I end, float roughness)
    {
        var points = new System.Collections.Generic.List<Vector2I>();
        
        int dx = System.Math.Abs(end.X - start.X);
        int dy = System.Math.Abs(end.Y - start.Y);
        int sx = start.X < end.X ? 1 : -1;
        int sy = start.Y < end.Y ? 1 : -1;
        int err = dx - dy;

        int currentX = start.X;
        int currentY = start.Y;

        FastNoiseLite noise = null;
        if (roughness > 0f)
        {
            noise = new FastNoiseLite();
            noise.Seed = (int)GD.Randi();
            noise.Frequency = 0.1f;
        }

        int step = 0;
        while (true)
        {
            Vector2I currentPos = new Vector2I(currentX, currentY);

            if (noise != null && roughness > 0f && (currentX != end.X || currentY != end.Y))
            {
                float noiseVal = noise.GetNoise2D(step * 2f, 0);
                if (System.Math.Abs(noiseVal) < roughness)
                {
                    if (dx > dy) currentPos.Y += noiseVal > 0 ? 1 : -1;
                    else currentPos.X += noiseVal > 0 ? 1 : -1;
                }
            }

            points.Add(currentPos);

            if (currentX == end.X && currentY == end.Y) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                currentX += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                currentY += sy;
            }
            step++;
        }

        return points;
    }
}
