using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace RadiantRevival.Common.Rendering.Sky;

/// <summary>
///     A simple system responsible for the handling of weather
///     variance over time.
/// </summary>
public sealed class CloudWeatherVarianceSystem : ModSystem
{
    /// <summary>
    ///     The scroll offset of the clouds due to wind.
    /// </summary>
    public static float ScrollOffset
    {
        get;
        private set;
    }

    /// <summary>
    ///     An ever-incrementing, global timer that dictates
    ///     slow changes for weather effects, such as the
    ///     density of clouds in the sky.
    /// </summary>
    public static float WeatherTimer
    {
        get;
        private set;
    }

    public override void ClearWorld()
    {
        ScrollOffset = 0f;
        WeatherTimer = 0f;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag[nameof(ScrollOffset)] = ScrollOffset;
        tag[nameof(WeatherTimer)] = WeatherTimer;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        ScrollOffset = tag.GetFloat(nameof(ScrollOffset));
        WeatherTimer = tag.GetFloat(nameof(WeatherTimer));
    }

    public override void PostUpdateNPCs()
    {
        ScrollOffset -= Main.windSpeedCurrent;
        WeatherTimer += 0.002f;
    }

    /// <summary>
    ///     Calculates one-dimensional noise based 
    ///     on several sinusoidal octaves with exponentially
    ///     decaying successive amplitudes.
    /// </summary>
    public static float Noise1D(float x)
    {
        var sum = 0f;
        var y = 0f;
        var exponentialBase = 2.81f;
        for (var i = 0; i < 8; i++)
        {
            var coefficient = MathF.Pow(exponentialBase, i);
            y += MathF.Sin(x * coefficient) / coefficient;
            sum += 1f / coefficient;
        }

        return y / sum * 0.5f + 0.5f;
    }
}
