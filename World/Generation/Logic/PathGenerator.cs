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
        int effectiveWidth = isHighway ? Mathf.Max(3, pathWidth + 1) : Mathf.Max(2, pathWidth);
        float effectiveRoughness = isHighway ? roughness * 0.4f : roughness * 0.8f;

        List<Vector2> splinePoints = GenerateSplinePoints(start, end, seed, effectiveRoughness, obstacles, bounds);
        RasterizeSplinePath(pathCells, splinePoints, effectiveWidth, seed, isHighway, bounds);

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

            FastNoiseLite detailNoise = new FastNoiseLite();
            detailNoise.Seed = seed ^ 0x5f3759df;
            detailNoise.Frequency = 0.15f;

            foreach (var cell in pathCells)
            {
                if (hasDetails && detailNoise.GetNoise2D(cell.X * 3, cell.Y * 3) > 0.45f)
                {
                    layer.SetCell(cell, settings?.DetailSourceId ?? 0, detailCoords);
                }
                else
                {
                    layer.SetCell(cell, settings?.PathSourceId ?? 0, pathCoords);
                }
            }
        }

        return pathCells;
    }

    private static List<Vector2> GenerateSplinePoints(
        Vector2I start,
        Vector2I end,
        int seed,
        float roughness,
        HashSet<Vector2I> obstacles,
        Rect2I? bounds)
    {
        Vector2 vStart = new Vector2(start.X, start.Y);
        Vector2 vEnd = new Vector2(end.X, end.Y);
        float dist = vStart.DistanceTo(vEnd);

        if (dist < 1f)
        {
            return new List<Vector2> { vStart };
        }

        Vector2 dir = (vEnd - vStart).Normalized();
        Vector2 normal = new Vector2(-dir.Y, dir.X);

        int hash = seed ^ (start.X * 73856093) ^ (start.Y * 19349663) ^ (end.X * 83492791) ^ (end.Y * 4256233);
        float sign = (Mathf.Abs(hash) % 2 == 0) ? 1.0f : -1.0f;
        float offsetMag = dist * roughness * 0.35f * (0.5f + (Mathf.Abs(hash / 2 % 1000) / 1000f) * 0.5f);

        Vector2 control = (vStart + vEnd) * 0.5f + normal * (sign * offsetMag);

        if (bounds.HasValue)
        {
            control.X = Mathf.Clamp(control.X, bounds.Value.Position.X + 2, bounds.Value.Position.X + bounds.Value.Size.X - 2);
            control.Y = Mathf.Clamp(control.Y, bounds.Value.Position.Y + 2, bounds.Value.Position.Y + bounds.Value.Size.Y - 2);
        }

        int steps = Mathf.Max(10, Mathf.RoundToInt(dist * 2f));
        var points = new List<Vector2>(steps + 1);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float u = 1.0f - t;
            Vector2 p = u * u * vStart + 2.0f * u * t * control + t * t * vEnd;
            points.Add(p);
        }

        return points;
    }

    private static void RasterizeSplinePath(
        HashSet<Vector2I> pathCells,
        List<Vector2> points,
        int width,
        int seed,
        bool isHighway,
        Rect2I? bounds = null)
    {
        if (points == null || points.Count == 0) return;

        float radius = width / 2.0f;
        float radiusSq = radius * radius;

        FastNoiseLite edgeNoise = new FastNoiseLite();
        edgeNoise.Seed = seed ^ 0x5f3759df;
        edgeNoise.Frequency = 0.2f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 p1 = points[i];
            Vector2 p2 = points[i + 1];
            float segmentDist = p1.DistanceTo(p2);
            int subSteps = Mathf.Max(1, Mathf.CeilToInt(segmentDist * 2.0f));

            for (int s = 0; s <= subSteps; s++)
            {
                float t = (float)s / subSteps;
                Vector2 p = p1.Lerp(p2, t);
                Vector2I centerCell = new Vector2I(Mathf.RoundToInt(p.X), Mathf.RoundToInt(p.Y));
                int rInt = Mathf.CeilToInt(radius + 0.5f);

                for (int dx = -rInt; dx <= rInt; dx++)
                {
                    for (int dy = -rInt; dy <= rInt; dy++)
                    {
                        Vector2I cell = centerCell + new Vector2I(dx, dy);
                        if (bounds.HasValue && !bounds.Value.HasPoint(cell)) continue;
                        float distSq = dx * dx + dy * dy;

                        // Core solid path disk
                        if (distSq <= radiusSq + 0.25f)
                        {
                            pathCells.Add(cell);
                        }
                        // Soft edge detail for organic borders
                        else if (distSq <= (radius + 0.5f) * (radius + 0.5f))
                        {
                            float noiseVal = edgeNoise.GetNoise2D(cell.X * 2f, cell.Y * 2f);
                            if (noiseVal > 0.1f)
                            {
                                pathCells.Add(cell);
                            }
                        }
                    }
                }
            }
        }
    }
}
