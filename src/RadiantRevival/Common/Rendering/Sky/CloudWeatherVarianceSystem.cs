using System;
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
    ///     An ever-incrementing, global timer that dictates
    ///     slow changes for weather effects, such as the
    ///     density of clouds in the sky.
    /// </summary>
    public static float WeatherTimer
    {
        get;
        private set;
    }

    public override void ClearWorld() => WeatherTimer = 0f;

    public override void SaveWorldData(TagCompound tag) => tag[nameof(WeatherTimer)] = WeatherTimer;

    public override void LoadWorldData(TagCompound tag) => WeatherTimer = tag.GetFloat(nameof(WeatherTimer));

    public override void PostUpdateNPCs() => WeatherTimer += 0.002f;

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
