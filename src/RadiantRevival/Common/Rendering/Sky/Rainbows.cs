using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Mathematics;
using Microsoft.Xna.Framework;
using RadiantRevival.Core;
using System;
using Terraria;
using Terraria.GameContent;

namespace RadiantRevival.Common.Rendering.Sky;

/// <summary>
///     A simple system responsible for the rendering
///     of rainbows in specific game circumstances.
/// </summary>
public static class Rainbows
{
    /// <summary>
    ///     The rain intensity of the previous frame.
    /// </summary>
    private static float previousRainIntensity;

    /// <summary>
    ///     The current derivative of the rain intensity in
    ///     the world.
    /// </summary>
    private static float dRainIntensity;

    /// <summary>
    ///     The current intensity of the rainbow.
    /// </summary>
    public static float RainbowIntensity
    {
        get;
        private set;
    }

    /// <summary>
    ///     How long the rainbow has existed for, in frames.
    /// </summary>
    public static float RainbowExistenceTimer
    {
        get;
        private set;
    }

    /// <summary>
    ///     How long the rainbow should exist for, in frames.
    /// </summary>
    public static float RainbowExistenceDuration
    {
        get;
        private set;
    }

    /// <summary>
    ///     The shortest amount of time that a rainbow can exist
    ///     for, in frames.
    /// </summary>
    public static int MinRainbowExistenceDuration => 95;

    /// <summary>
    ///     The longest amount of time that a rainbow can exist
    ///     for, in frames.
    /// </summary>
    public static int MaxRainbowExistenceDuration => 1200;

    [ModSystemHooks.PreUpdateNPCs]
    private static void UpdateMovingRainIntensity()
    {
        var currentRainIntensity = Main.maxRaining;
        dRainIntensity = currentRainIntensity - previousRainIntensity;
        previousRainIntensity = currentRainIntensity;

        // While the rain is still ongoing and not going away, choose
        // how long the rainbow that exists after it should last for,
        // along with related values that dictate randomness.
        if (currentRainIntensity >= 0.001f && dRainIntensity >= 0f)
            InitializeRainbowVariables(currentRainIntensity);

        var rainIsEnding = dRainIntensity < 0f;
        var rainbowShouldAppear = rainIsEnding || (RainbowExistenceTimer >= 1 && RainbowExistenceTimer <= RainbowExistenceDuration);
        if (rainbowShouldAppear)
        {
            RainbowExistenceTimer++;

            var existenceProgress = Utils.GetLerpValue(0f, RainbowExistenceDuration, RainbowExistenceTimer, true);
            var fadeIn = Utils.GetLerpValue(0f, 0.16f, existenceProgress, true);
            var fadeOut = Utils.GetLerpValue(1f, 0.5f, existenceProgress, true);
            var globalIntensity = 0.56f + MathF.Cos(CloudWeatherVarianceSystem.WeatherTimer * 0.0125f) * 0.09f;
            RainbowIntensity = fadeIn * fadeOut * globalIntensity;
        }
        else
            RainbowIntensity *= 0.97f;
    }

    /// <summary>
    ///     Initializes semi-randomly determined variables
    ///     for the rainbow visual effect.
    /// </summary>
    private static void InitializeRainbowVariables(float currentRainIntensity)
    {
        var rainbowDurationInterpolant = Math.Clamp(currentRainIntensity * 2.3f + Main.rand.NextFloat(0.2f), 0f, 1f);
        RainbowExistenceDuration = (int)Interpolate.Lerp(MinRainbowExistenceDuration, MaxRainbowExistenceDuration, rainbowDurationInterpolant);

        // Introduce a good chance for a rainbow to not appear at all.
        // This keeps it random and interesting when it does
        // and the player notices.
        if (Main.rand.NextBool())
            RainbowExistenceDuration = 0;

        RainbowExistenceTimer = 0;
    }

    /// <summary>
    ///     Calculates the tint of the rainbow based on
    ///     gameplay context, such as the biome the player
    ///     is currently inhabiting.
    /// </summary>
    private static Color CalculateContextualTint()
    {
        var tint = Color.White;

        var evilBiomeInterpolant = Utils.GetLerpValue(0f, 700f, Main.SceneMetrics.EvilTileCount, true);
        tint = Color.Lerp(tint, new Color(85, 255, 174) * 0.75f, evilBiomeInterpolant);

        var mushroomBiomeInterpolant = Utils.GetLerpValue(0f, 500f, Main.SceneMetrics.MushroomTileCount, true);
        tint = Color.Lerp(tint, new Color(50, 50, 255), mushroomBiomeInterpolant);

        return tint;
    }

    /// <summary>
    ///     Renders the rainbow in the sky.
    /// </summary>
    public static void Render()
    {
        if (RainbowIntensity <= 0f)
            return;

        var shader = AssetReferences.Assets.Sky.RainbowShader.CreateAutoloadPass();
        shader.Parameters.zoom = Vector2.One;
        shader.Parameters.screenPosition = Main.screenPosition;
        shader.Parameters.screenSize = Main.ScreenSize.ToVector2();
        shader.Parameters.fieldSqrtDepth = DensityFieldSystem.SqrtDepth;
        shader.Parameters.fieldTargetSize2D = DensityFieldSystem.BaselineTargetSize.ToVector2();
        shader.Parameters.sunPosition = new Vector3(AtmosphereCloudRenderingSystem.CelestialBodyPosition + Main.screenPosition, 1000f);
        shader.Apply();

        var viewportArea = new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);

        var overallLight = MathF.Pow(1f - AtmosphereCloudRenderingSystem.LowSun, 0.15f);
        var opacity = RainbowIntensity * overallLight;

        var pixel = TextureAssets.MagicPixel.Value;
        var tint = CalculateContextualTint();
        Main.spriteBatch.Draw(pixel, viewportArea, tint * MathF.Sqrt(1f - AtmosphereCloudRenderingSystem.LowSun) * opacity);
    }
}
