using Daybreak.Common.Mathematics;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using System;
using Terraria;
using Terraria.ModLoader;

namespace RadiantRevival.Common.Rendering.Sky;

/// <summary>
///     The system responsible for the updating of
///     the cloud density field.
/// </summary>
[Autoload(Side = ModSide.Client)]
public sealed class DensityFieldSystem : ModSystem
{
    private static RenderTargetLease? swapField;

    /// <summary>
    ///     The scalar field which contains screen-spaced
    ///     cloud density data.
    /// </summary>
    public static RenderTargetLease? DensityField
    {
        get;
        private set;
    }

    /// <summary>
    ///     The square root of the depth of the target.
    /// </summary>
    public static int SqrtDepth => 9;

    /// <summary>
    ///     The baseline size of the field target.
    /// </summary>
    /// <remarks>
    public static Point BaselineTargetSize => new(500, 612);

    /// <summary>
    ///     Whether this system should be disabled and not
    ///     update, in the interest of reducing general
    ///     performance costs.
    /// </summary>
    public static bool ShouldBeDisabled
    {
        get
        {
            // Hmm yes seeing a bunch of clouds underground I hadn't
            // though of that! Yeah, no.
            var underground = Main.LocalPlayer.Center.Y >= Main.worldSurface * 16f;
            if (underground)
                return true;

            return false;
        }
    }

    /// <summary>
    ///     Converts a temperature in Fahrenheit to Kelvin.
    /// </summary>
    private static float FahrenheitToKelvin(float f) => (f - 32f) * 5f / 9f + 273.15f;

    /// <summary>
    ///     Calculates the humidity factor based on the current
    ///     gameplay situation, such as the biome the
    ///     player is currently inhabiting.
    /// </summary>
    private static float CalculateHumidityFactor()
    {
        var humidityFactor = 1f;

        var desertInterpolant = Utils.GetLerpValue(0f, 900f, Main.SceneMetrics.SandTileCount, true);
        humidityFactor *= Interpolate.Lerp(1f, 0.3f, desertInterpolant);

        var jungleInterpolant = Utils.GetLerpValue(0f, 900f, Main.SceneMetrics.JungleTileCount, true);
        humidityFactor *= Interpolate.Lerp(1f, 1.54f, jungleInterpolant);

        var evilBiomeInterpolant = Utils.GetLerpValue(0f, 700f, Main.SceneMetrics.EvilTileCount, true);
        humidityFactor *= Interpolate.Lerp(1f, 0.85f, evilBiomeInterpolant);

        if (Main.LocalPlayer.ZoneBeach)
            humidityFactor *= 1.25f;

        return humidityFactor;
    }

    /// <summary>
    ///     Calculates the surface temperature, in Fahrenheit, based
    ///     on the current gameplay situation, mostly notable example
    ///     being the current time of day.
    /// </summary>
    private static float CalculateSurfaceTemperatureFahrenheit()
    {
        var dayCompletion = (float)(Main.time / Main.dayLength);
        var nightCompletion = (float)(Main.time / Main.nightLength);
        if (Main.dayTime)
            nightCompletion = 0f;
        else
            dayCompletion = 1f;

        var baseTemperature = 68f - Main.maxRaining * 17f - Main.eclipseLight * 18f;

        var dayBump = MathF.Pow(MathF.Sin(MathF.PI * dayCompletion), 2f);
        var dayCycleWarming = dayBump * (1f - Main.maxRaining) * 9.3f;

        var nightBump = Math.Clamp(MathF.Sin(MathF.PI * nightCompletion), 0f, 1f);
        var nightCycleCooling = MathF.Sqrt(nightBump) * 6.5f;
        var surfaceTemperature = baseTemperature + dayCycleWarming - nightCycleCooling;

        return surfaceTemperature;
    }

    public override void PostDrawTiles()
    {
        if (Main.gamePaused && DensityField is not null)
            return;

        var descriptor = RenderTargetDescriptor.Default with
        {
            Format = SurfaceFormat.Single
        };

        var scaledSize = new Point(BaselineTargetSize.X * SqrtDepth, BaselineTargetSize.Y * SqrtDepth);
        swapField ??= RenderTargetPool.Shared.Rent(Main.instance.GraphicsDevice, scaledSize.X, scaledSize.Y, descriptor);
        DensityField ??= RenderTargetPool.Shared.Rent(Main.instance.GraphicsDevice, scaledSize.X, scaledSize.Y, descriptor);

        if (ShouldBeDisabled)
            return;

        var targetSize2D = BaselineTargetSize.ToVector2();
        using (DensityField.Scope(clearColor: Color.Transparent))
        {
            var viewportArea = new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            var shader = AssetReferences.Assets.Sky.DensityFieldEvolutionShader.CreateAutoloadPass();
            shader.Parameters.depth = SqrtDepth * SqrtDepth;
            shader.Parameters.sqrtDepth = SqrtDepth;
            shader.Parameters.targetSize2D = targetSize2D;

            var condensationInterpolant = CloudWeatherVarianceSystem.Noise1D(CloudWeatherVarianceSystem.WeatherTimer * 0.0125f);
            var condensationCoefficient = Interpolate.Lerp(0.01993f, 0.043f, condensationInterpolant);

            var buoyancyInterpolant = CloudWeatherVarianceSystem.Noise1D(CloudWeatherVarianceSystem.WeatherTimer * 0.017f);
            var buoyancyIntensity = Interpolate.Lerp(0.033f, 0.099f, buoyancyInterpolant);

            var humidityBias = CloudWeatherVarianceSystem.Noise1D(CloudWeatherVarianceSystem.WeatherTimer * 0.018f) * 0.12f;

            shader.Parameters.densityDampeningDecayFactor = 0.9997f;
            shader.Parameters.advectionBlendInterpolant = 0.33f;
            shader.Parameters.densityGrowthCoefficient = 0.01f;
            shader.Parameters.densityDecayCoefficient = 0.04f;
            shader.Parameters.condensationCoefficient = condensationCoefficient;
            shader.Parameters.humidityBase = (0.14f + humidityBias) * CalculateHumidityFactor() + MathF.Sqrt(Main.maxRaining) * 1.3f;
            shader.Parameters.humidityHeightFalloff = 0.6f;
            shader.Parameters.buoyancyIntensity = buoyancyIntensity;

            shader.Parameters.surfaceTemperature = FahrenheitToKelvin(CalculateSurfaceTemperatureFahrenheit());
            shader.Parameters.spaceTemperature = FahrenheitToKelvin(-59f);
            shader.Parameters.buoyancyReferenceTemperature = FahrenheitToKelvin(12.5f);

            shader.Parameters.time = CloudWeatherVarianceSystem.WeatherTimer * 0.75f;
            shader.Parameters.horizontalScrollSpeed = Main.windSpeedCurrent * 4.1f;

            shader.Parameters.noiseTexture = new HlslSampler2D
            {
                Texture = AssetReferences.Assets.Noise.PerlinNoise.Asset.Value,
                Sampler = SamplerState.LinearWrap
            };

            shader.Apply();

            Main.spriteBatch.Draw(swapField.Target, viewportArea, Color.White);

            Main.spriteBatch.End();
        }
        using (swapField.Scope(clearColor: Color.Transparent))
        {
            var viewportArea = new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);
            Main.spriteBatch.Begin();
            Main.spriteBatch.Draw(DensityField.Target, viewportArea, Color.White);
            Main.spriteBatch.End();
        }
    }
}
