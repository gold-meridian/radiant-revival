using System;
using Microsoft.Xna.Framework;
using Terraria.Graphics.Light;

namespace RadiantRevival.Common;

internal static class FullbrightLightMap
{
    private sealed class LightMapScope(LightMap lightMap, Vector3[] colors, LightMaskMode[] masks) : IDisposable
    {
        public void Dispose()
        {
            lightMap._colors = colors;
            lightMap._mask = masks;
        }
    }

    private static Vector3[] colors = [];
    private static LightMaskMode[] masks = [];

    private static readonly LegacyLighting.LightingState default_lighting_state = new()
    {
        R = 1f,
        G = 1f,
        B = 1f,
    };

    private static LegacyLighting.LightingState[] innerStateArray = [];
    private static LegacyLighting.LightingState[][] outerStateArray = [];

    static FullbrightLightMap()
    {
        EnsureSize(LightMap.DEFAULT_WIDTH * LightMap.DEFAULT_HEIGHT);
    }

    public static IDisposable ApplyTo(LightMap lightMap)
    {
        EnsureSize(lightMap._colors.Length);

        var oldColors = lightMap._colors;
        var oldMasks = lightMap._mask;

        // The array lengths is only checked for colors, and it's in the
        // clearing operation, so we can pass our cached buffer.
        // Clear should never be reached in our typical operations, so we should
        // just care about not outright crashing.
        lightMap._colors = colors;
        lightMap._mask = masks;

        return new LightMapScope(lightMap, oldColors, oldMasks);
    }

    private static void EnsureSize(int size)
    {
        // masks must match colors in length due to a constraint in
        // LightMap::Clear.
        if (colors.Length >= size && masks.Length == colors.Length)
        {
            return;
        }

        colors = new Vector3[size];
        colors.AsSpan().Fill(Vector3.One);

        masks = new LightMaskMode[size];
    }

    public static IDisposable ApplyTo(ref LegacyLighting.LightingState[][] states)
    {
        default_lighting_state.R = 1f;
        default_lighting_state.G = 1f;
        default_lighting_state.B = 1f;

        var length1 = states.Length;
        var length2 = states[0].Length;

        EnsureSize(length1, length2);

        states = outerStateArray;
    }

    private static void EnsureSize(int length1, int length2)
    {
        var dirty = false;
        if (innerStateArray.Length < length2)
        {
            innerStateArray = new LegacyLighting.LightingState[length2];
            innerStateArray.AsSpan().Fill(default_lighting_state);
            dirty = true;
        }

        if (dirty || outerStateArray.Length < length1)
        {
            outerStateArray = new LegacyLighting.LightingState[length1][];
            outerStateArray.AsSpan().Fill(innerStateArray);
        }
    }
}
