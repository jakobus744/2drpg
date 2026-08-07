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
        int seed = 1337,
        HashSet<Vector2I> obstacles = null,
        HashSet<Vector2I> existingPaths = null,
        Rect2I? bounds = null,
        bool isHighway = false)
    {
        var pathCells = new HashSet<Vector2I>();
        float effectiveRoughness = isHighway ? roughness * 0.7f : roughness * 1.3f;
        List<Vector2I> linePoints = GenerateAStarLine(start, end, obstacles, existingPaths, bounds, seed, effectiveRoughness);

        FastNoiseLite edgeNoise = new FastNoiseLite();
        edgeNoise.Seed = seed ^ 0x5f3759df;
        edgeNoise.Frequency = 0.15f;

        int effectiveWidth = isHighway ? Mathf.Max(3, pathWidth + 1) : Mathf.Max(1, pathWidth - 1);
        int baseHalfWidth = effectiveWidth / 2;

        for (int i = 0; i < linePoints.Count; i++)
        {
            var point = linePoints[i];
            float progress = (float)i / Mathf.Max(1, linePoints.Count - 1);
            float widthFactor = 1.0f - (isHighway ? 0.15f : 0.35f) * Mathf.Pow(2.0f * (progress - 0.5f), 4);
            int currentHalfWidth = Mathf.Max(1, Mathf.RoundToInt(baseHalfWidth * widthFactor));

            for (int dx = -currentHalfWidth; dx <= currentHalfWidth; dx++)
            {
                for (int dy = -currentHalfWidth; dy <= currentHalfWidth; dy++)
                {
                    Vector2I cell = point + new Vector2I(dx, dy);

                    if (bounds.HasValue && !bounds.Value.HasPoint(cell)) continue;
                    if (obstacles != null && obstacles.Contains(cell)) continue;

                    float distFromCenter = Mathf.Sqrt(dx * dx + dy * dy);
                    float maxDist = currentHalfWidth + 0.5f;

                    if (distFromCenter <= maxDist - 0.5f)
                    {
                        pathCells.Add(cell);
                    }
                    else if (distFromCenter <= maxDist)
                    {
                        float noiseCutoff = isHighway ? -0.05f : -0.3f;
                        float n = edgeNoise.GetNoise2D(cell.X, cell.Y);
                        if (n > noiseCutoff)
                        {
                            pathCells.Add(cell);
                        }
                    }
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
            Vector2I detailCoords = settings?.DetailTileCoords ?? new Vector2I(-1, -1);
            bool hasDetails = detailCoords != new Vector2I(-1, -1);

            foreach (var cell in pathCells)
            {
                if (hasDetails && edgeNoise.GetNoise2D(cell.X * 3, cell.Y * 3) > 0.45f)
                {
                    layer.SetCell(cell, 0, detailCoords);
                }
                else
                {
                    layer.SetCell(cell, 0, pathCoords);
                }
            }
        }

        return pathCells;
    }

    private static List<Vector2I> GenerateAStarLine(
        Vector2I start,
        Vector2I end,
        HashSet<Vector2I> obstacles,
        HashSet<Vector2I> existingPaths,
        Rect2I? bounds,
        int seed,
        float roughness)
    {
        if (start == end) return new List<Vector2I> { start };

        FastNoiseLite terrainNoise = new FastNoiseLite();
        terrainNoise.Seed = seed ^ (start.X * 73856093) ^ (start.Y * 19349663);
        terrainNoise.Frequency = 0.04f;

        var openSet = new PriorityQueue<Vector2I, float>();
        var gScore = new System.Collections.Generic.Dictionary<Vector2I, float>();
        var cameFrom = new System.Collections.Generic.Dictionary<Vector2I, Vector2I>();

        gScore[start] = 0f;
        openSet.Enqueue(start, OctileDistance(start, end));

        int maxIterations = 3000;
        int iterations = 0;
        bool found = false;

        Vector2I[] neighborDirs = {
            new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1),
            new Vector2I(1, 1), new Vector2I(-1, 1), new Vector2I(1, -1), new Vector2I(-1, -1)
        };

        while (openSet.Count > 0 && iterations++ < maxIterations)
        {
            Vector2I current = openSet.Dequeue();
            if (current == end)
            {
                found = true;
                break;
            }

            float currentG = gScore[current];

            foreach (var dir in neighborDirs)
            {
                Vector2I neighbor = current + dir;

                if (bounds.HasValue && !bounds.Value.HasPoint(neighbor)) continue;
                if (obstacles != null && obstacles.Contains(neighbor)) continue;

                bool isDiagonal = dir.X != 0 && dir.Y != 0;
                float stepCost = isDiagonal ? 1.414f : 1.0f;

                float noiseVal = (terrainNoise.GetNoise2D(neighbor.X, neighbor.Y) + 1.0f) * 0.5f;
                float terrainCostMultiplier = 1.0f + roughness * 3.0f * noiseVal;

                if (existingPaths != null && existingPaths.Contains(neighbor))
                {
                    terrainCostMultiplier *= 0.35f;
                }

                float tentativeG = currentG + stepCost * terrainCostMultiplier;

                if (!gScore.TryGetValue(neighbor, out float existingG) || tentativeG < existingG)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    float h = OctileDistance(neighbor, end);
                    openSet.Enqueue(neighbor, tentativeG + h);
                }
            }
        }

        if (!found)
        {
            return GenerateCurvedLine(start, end, roughness, seed);
        }

        var rawPath = new List<Vector2I>();
        Vector2I curr = end;
        while (curr != start)
        {
            rawPath.Add(curr);
            curr = cameFrom[curr];
        }
        rawPath.Add(start);
        rawPath.Reverse();

        return SmoothAStarPath(rawPath);
    }

    private static List<Vector2I> SmoothAStarPath(List<Vector2I> rawPath)
    {
        if (rawPath.Count <= 2) return rawPath;

        var keyPoints = new List<Vector2I> { rawPath[0] };
        Vector2I lastDir = rawPath[1] - rawPath[0];

        for (int i = 2; i < rawPath.Count; i++)
        {
            Vector2I currentDir = rawPath[i] - rawPath[i - 1];
            if (currentDir != lastDir)
            {
                keyPoints.Add(rawPath[i - 1]);
                lastDir = currentDir;
            }
        }
        keyPoints.Add(rawPath[rawPath.Count - 1]);

        var smoothed = new List<Vector2I>();
        for (int i = 0; i < keyPoints.Count - 1; i++)
        {
            var segment = GenerateBresenhamLine(keyPoints[i], keyPoints[i + 1]);
            foreach (var pt in segment)
            {
                if (smoothed.Count == 0 || smoothed[smoothed.Count - 1] != pt)
                {
                    smoothed.Add(pt);
                }
            }
        }

        return smoothed;
    }

    private static float OctileDistance(Vector2I a, Vector2I b)
    {
        int dx = Mathf.Abs(a.X - b.X);
        int dy = Mathf.Abs(a.Y - b.Y);
        return 1.0f * (dx + dy) + (1.414f - 2.0f * 1.0f) * Mathf.Min(dx, dy);
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
