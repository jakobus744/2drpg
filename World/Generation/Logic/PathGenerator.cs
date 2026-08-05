using System.Collections.Generic;
using Godot;
using Godot.Collections;
using RPG2d.World.Generation.Data;

namespace RPG2d.World.Generation.Logic;

public static class PathGenerator
{
    public static HashSet<Vector2I> ConnectWaypoints(
        TileMapLayer layer,
        Vector2I start,
        Vector2I end,
        ZoneSettings settings = null,
        int terrainSet = 0,
        int terrain = 0,
        int pathWidth = 2,
        float roughness = 0.2f,
        int seed = 1337)
    {
        var pathCells = new HashSet<Vector2I>();
        var linePoints = GenerateCurvedLine(start, end, roughness, seed);

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

        if (layer == null || pathCells.Count <= 0) return pathCells;
        var hasTerrains = layer.TileSet != null && layer.TileSet.GetTerrainSetsCount() > terrainSet;

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
            Vector2I pathCoords = settings?.PathTileCoords ?? new Vector2I(1, 0);
            foreach (var cell in pathCells)
            {
                layer.SetCell(cell, 0, pathCoords);
            }
        }

        return pathCells;
    }

    private static List<Vector2I> GenerateCurvedLine(Vector2I start, Vector2I end, float roughness, int seed)
    {
        var points = new List<Vector2I>();
        int dist = Mathf.Max(1, (int)start.DistanceTo(end));

        if (roughness <= 0f || dist < 4)
        {
            return GenerateBresenhamLine(start, end);
        }

        FastNoiseLite noise = new FastNoiseLite();
        noise.Seed = seed ^ (start.X * 73856093) ^ (start.Y * 19349663) ^ (end.X * 83492791);
        noise.Frequency = 0.05f;

        Vector2 dir = new Vector2(end.X - start.X, end.Y - start.Y);
        Vector2 normal = new Vector2(-dir.Y, dir.X).Normalized();

        float noiseVal = noise.GetNoise2D(start.X, end.Y);
        float offsetAmount = dist * roughness * 0.4f * noiseVal;

        Vector2 mid = (new Vector2(start.X, start.Y) + new Vector2(end.X, end.Y)) * 0.5f;
        Vector2 control = mid + normal * offsetAmount;

        int steps = Mathf.Max(dist, 10);
        Vector2I lastPoint = start;
        points.Add(start);

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            float u = 1f - t;

            Vector2 p = u * u * new Vector2(start.X, start.Y) +
                        2f * u * t * control +
                        t * t * new Vector2(end.X, end.Y);

            Vector2I currentPoint = new Vector2I(Mathf.RoundToInt(p.X), Mathf.RoundToInt(p.Y));

            if (currentPoint != lastPoint)
            {
                var lineSegment = GenerateBresenhamLine(lastPoint, currentPoint);
                foreach (var pt in lineSegment)
                {
                    if (points.Count == 0 || points[points.Count - 1] != pt)
                    {
                        points.Add(pt);
                    }
                }
                lastPoint = currentPoint;
            }
        }

        return points;
    }

    private static List<Vector2I> GenerateBresenhamLine(Vector2I start, Vector2I end)
    {
        var points = new List<Vector2I>();
        int dx = System.Math.Abs(end.X - start.X);
        int dy = System.Math.Abs(end.Y - start.Y);
        int sx = start.X < end.X ? 1 : -1;
        int sy = start.Y < end.Y ? 1 : -1;
        int err = dx - dy;

        int currentX = start.X;
        int currentY = start.Y;

        while (true)
        {
            points.Add(new Vector2I(currentX, currentY));
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
        }

        return points;
    }
}
