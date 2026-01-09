using System;
using System.Collections.Generic;
using CaveCrafter.CaveGen.Config;
using CaveCrafter.CaveGen.Core.Geometry;

namespace CaveCrafter.CaveGen.Core.Planning
{
    /// <summary>
    /// Phase 1 planner that generates highway start/end intents only.
    /// No splines, no carving — just anchors, endpoints, and intent lines.
    /// </summary>
    public sealed class HighwayPlanner
    {
        public IReadOnlyList<HighwayIntent> Plan(
            int mapWidth,
            int mapHeight,
            ScaledTunnelClassConfig highwayCfg,
            IRng rng)
        {
            if (mapWidth <= 0) throw new ArgumentOutOfRangeException(nameof(mapWidth));
            if (mapHeight <= 0) throw new ArgumentOutOfRangeException(nameof(mapHeight));
            if (highwayCfg == null) throw new ArgumentNullException(nameof(highwayCfg));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int count = ComputeHighwayCount(mapWidth, highwayCfg, rng);

            float columnWidth = mapWidth / (float)count;
            List<HighwayIntent> intents = new List<HighwayIntent>(count);

            for (int i = 0; i < count; i++)
            {
                float baseT = (count == 1) ? 0.5f : (i / (float)(count - 1)); // 0..1 across highways

                float colStart = i * columnWidth;
                float colCenter = colStart + (columnWidth * 0.5f);

                // Slightly stronger jitter to reduce "grid" feel (still bounded by column).
                float jitterRange = columnWidth * 0.40f;
                float jitter = (rng.NextFloat01() * 2f - 1f) * jitterRange;

                float edgeBiasedT = CoreMath.EaseToEdges01(baseT, highwayCfg.EdgeBiasFrac);
                float edgeBiasX = CoreMath.Lerp(0f, mapWidth - 1f, edgeBiasedT);

                float startX = CoreMath.Lerp(colCenter + jitter, edgeBiasX, 0.35f);
                startX = CoreMath.Clamp(startX, 0f, mapWidth - 1f);

                int startY = rng.NextInt(highwayCfg.StartYMin, highwayCfg.StartYMax + 1);

                int endY = rng.NextInt(highwayCfg.EndYMin, highwayCfg.EndYMax + 1);

                int minDown = rng.NextInt(highwayCfg.MinDownwardDeltaYMin, highwayCfg.MinDownwardDeltaYMax + 1);
                if (endY < startY + minDown)
                {
                    endY = Math.Min(mapHeight - 1, startY + minDown);
                }

                HighwaySide side = ChooseSideBias(startX, mapWidth, rng);

                // Horizontal travel baseline (kept similar, but slope enforcement will adjust endX precisely).
                float minDx = mapWidth * 0.25f;
                float maxDx = mapWidth * 0.55f;

                float dxMag = CoreMath.Lerp(minDx, maxDx, rng.NextFloat01());
                float dx = (side == HighwaySide.Right) ? dxMag : -dxMag;

                float endX = startX + dx;

                // Enforce diagonal trend rule (legacy floor): dy >= k * absDx => absDx <= dy / k
                float dy = endY - startY;
                float absDx = MathF.Abs(endX - startX);

                if (absDx > 1e-3f)
                {
                    float requiredDy = highwayCfg.MinDiagonalDyOverDx * absDx;
                    if (dy < requiredDy)
                    {
                        float maxAllowedDx = dy / highwayCfg.MinDiagonalDyOverDx;
                        float sign = MathF.Sign(endX - startX);
                        endX = startX + (sign * maxAllowedDx);
                    }
                }

                ApplyAngleBandPreferX(
                    startX: startX,
                    startY: startY,
                    endX: ref endX,
                    endY: endY,
                    side: side,
                    minAngleDeg: highwayCfg.MinAngleDeg,
                    maxAngleDeg: highwayCfg.MaxAngleDeg,
                    rng: rng);

                // Final safety: ensure some horizontal component remains.
                if (MathF.Abs(endX - startX) < 1f)
                {
                    float nudge = (side == HighwaySide.Right) ? 1f : -1f;
                    endX = CoreMath.Clamp(startX + nudge, 0f, mapWidth - 1f);
                }

                Float2 start = new Float2(startX, startY);
                Float2 end = new Float2(endX, endY);

                intents.Add(new HighwayIntent(i, start, end, side));
            }

            return intents;
        }

        private static int ComputeHighwayCount(int mapWidth, ScaledTunnelClassConfig cfg, IRng rng)
        {
            int baseCount = CoreMath.RoundToInt(mapWidth / (float)cfg.CountPerWidthTiles);

            int variance = Math.Max(0, cfg.CountVariance);
            int min = cfg.MinCount;
            int max = cfg.MaxCount;

            // Random within [base-variance .. base+variance], then clamp.
            int low = baseCount - variance;
            int high = baseCount + variance;

            // Bias slightly upward by rolling twice and taking the higher result (lightweight & deterministic).
            int a = rng.NextInt(low, high + 1);
            int b = rng.NextInt(low, high + 1);
            int chosen = Math.Max(a, b);

            return CoreMath.Clamp(chosen, min, max);
        }

        private static HighwaySide ChooseSideBias(float startX, int mapWidth, IRng rng)
        {
            float t = (mapWidth <= 1) ? 0.5f : (startX / (mapWidth - 1f));
            float edgeStrength = MathF.Abs((t * 2f) - 1f); // 0 centre -> 1 edges
            float preferCrossChance = CoreMath.Lerp(0.55f, 0.80f, edgeStrength);

            bool cross = rng.Chance(preferCrossChance);
            if (cross)
            {
                return (t < 0.5f) ? HighwaySide.Right : HighwaySide.Left;
            }

            return rng.Chance(0.5f) ? HighwaySide.Left : HighwaySide.Right;
        }

        /// <summary>
        /// Enforces slope angle band by adjusting endX (preferred).
        /// Keeps dy intact (deep endpoints) and solves for absDx so dy/absDx matches the band.
        /// </summary>
        private static void ApplyAngleBandPreferX(
            float startX,
            int startY,
            ref float endX,
            int endY,
            HighwaySide side,
            float minAngleDeg,
            float maxAngleDeg,
            IRng rng)
        {
            float dy = endY - startY;
            if (dy <= 0f) return;

            float aMin = MathF.Max(0.01f, MathF.Min(minAngleDeg, maxAngleDeg));
            float aMax = MathF.Min(89.0f, MathF.Max(minAngleDeg, maxAngleDeg));

            float t = rng.NextFloat01();
            float targetAngleDeg = aMin + ((aMax - aMin) * t);

            float targetSlope = TanDeg(targetAngleDeg);
            float requiredAbsDx = dy / targetSlope;

            float sign = (side == HighwaySide.Right) ? 1f : -1f;

            endX = startX + (sign * requiredAbsDx);
        }

        private static float TanDeg(float degrees)
        {
            return MathF.Tan(degrees * (MathF.PI / 180f));
        }
    }
}
