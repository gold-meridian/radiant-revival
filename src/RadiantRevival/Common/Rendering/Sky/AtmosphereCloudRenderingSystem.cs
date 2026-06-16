using Daybreak.Common.Mathematics;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Core;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace RadiantRevival.Common.Rendering.Sky;

/// <summary>
///     The system responsible for the management of
///     atmosphere and clouds rendering.
/// </summary>
public sealed class AtmosphereCloudRenderingSystem : ModSystem
{
    // When it goes from day to night, or vice versa, the
    // position of the celestial body discretely and awkawrdly
    // jumps from on side of the screen to another.
    // This property is a solution to this problem, making it so that
    // the origin of light takes a short amount of time
    // to reach its new position.
    // It still looks a little strange, admittedly, but ultimately
    // it works sufficiently given the conditions Terraria
    // has set for itself.

    /// <summary>
    ///     The current position of the active celestial body (sun or moon)
    ///     for the purposes of lighting. This drags behind the
    ///     real celestial body position slightly.
    /// </summary>
    public static Vector2 CelestialBodyPosition
    {
        get;
        set;
    }

    /// <summary>
    ///     The color of the sky before <see cref="Main.atmo"/>
    ///     calculations are applied.
    /// </summary>
    public static Color ColorBeforeAtmoDarkening
    {
        get;
        private set;
    }

    /// <summary>
    ///     How far along the day is.
    /// </summary>
    /// <remarks>
    ///     This value will always be zero if it is night
    ///     time.
    /// </remarks>
    public static float DayProgress
    {
        get
        {
            var dayProgress = Utils.GetLerpValue(0f, (float)Main.dayLength, (float)Main.time, true);
            if (!Main.dayTime)
                dayProgress = 0f;

            return dayProgress;
        }
    }

    /// <summary>
    ///     A 0-1 value indicating how "low" the sun is.
    ///     This can be thought of analogously as how far
    ///     into a sunrise/sunset the day currently is.
    /// </summary>
    /// <remarks>
    ///     This value will always be one if it is night
    ///     time.
    /// </remarks>
    public static float LowSun => 1f - Utils.GetLerpValue(0f, 0.2f, DayProgress, true) * Utils.GetLerpValue(1f, 0.8f, DayProgress, true);

    /// <summary>
    ///     A 0-1 value that dictates how much moonglow should
    ///     be present when tinting clouds.
    /// </summary>
    /// <remarks>
    ///     Strictly speaking, this interpolant dictates how much
    ///     the background tints from black to white, and as such
    ///     has relatively low peaks of approximately 0.1.
    ///     
    ///     <br></br>
    ///     
    ///     Anything higher would be too bright for a night time
    ///     setting.
    /// </remarks>
    public static float MoonlightGlowInterpolant
    {
        get
        {
            // The moon phase sheet exists in eight total
            // frames, ranging from 0 to 7.
            // The moon starts as a full moon, and ends
            // in the last frame before waxing back into
            // a full moon. The new moon frames in the
            // middle of the vertical sheet.

            // Consequently, it is possible to take a sine
            // bump based on this phase value, and invert it
            // such that the brightest values are present
            // at values 0 and 7, and the dimmest are present
            // at around 3 and 4.
            var phaseCompletion = Math.Clamp(Main.moonPhase / 8f, 0f, 1f);
            var phaseBump = MathF.Sin(MathF.PI * phaseCompletion);
            var fullness = 1f - phaseBump;

            return Interpolate.Lerp(0.05f, 0.11f, fullness);
        }
    }

    /// <summary>
    ///     How strong the rain currently is, as a relative 0-1
    ///     interpolant value.
    /// </summary>
    public static float RainInterpolant => Utils.GetLerpValue(0f, 0.4f, Main.maxRaining, true);

    /// <summary>
    ///     The amount by which the background should be upwardly
    ///     saturated. This is not realistically accurate, but artistically
    ///     it's cool and helps the colors a bit more vivid.
    /// </summary>
    public static float AtmosphereSaturationBoost => 0.2f;

    /// <summary>
    ///     The factor by which clouds should be saturated relative
    ///     to the baseline background color.
    /// </summary>
    public static float CloudDesaturationFactor => 0.5f;

    /// <summary>
    ///     The color used for tinting the tiles and background
    ///     as the sun is low (e.g. during sunrise or sunset).
    /// </summary>
    public static Color LowSunTintColor => new(255, 25, 15);

    /// <summary>
    ///     The size of the cloud box in the sky.
    /// </summary>
    public static Vector3 CloudSize => new(6300f, 1700f, 850f);

    public override void OnModLoad()
    {
        IL_Main.SetBackColor += DisableTypicalSunriseSunsetLighting;
        IL_Main.DrawSurfaceBG += RemoveDefaultCloudBackground;
        On_Main.UpdateAtmosphereTransparencyToSkyColor += DisableAtmosphereBackgroundDarkening;
        On_Main.DrawSunAndMoon += Render;
    }

    private static void DisableTypicalSunriseSunsetLighting(ILContext il)
    {
        var c = new ILCursor(il);
        c.GotoNext(MoveType.After, i => i.MatchLdsfld<Main>(nameof(Main.time)));
        c.EmitDelegate((double time) =>
        {
            if (Main.dayTime)
                return Main.dayLength * 0.5;

            return Main.nightLength * 0.5;
        });
    }

    private static void RemoveDefaultCloudBackground(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(i => i.MatchLdsfld<Main>(nameof(Main.cloudBG)));
        c.GotoPrev(MoveType.After, i => i.MatchLdsfld<Main>(nameof(Main.cloudBGAlpha)));

        c.EmitDelegate((float bgAlpha) => 0f);
    }

    private static void DisableAtmosphereBackgroundDarkening(On_Main.orig_UpdateAtmosphereTransparencyToSkyColor orig, float y)
    {
        ColorBeforeAtmoDarkening = Main.ColorOfTheSkies;
        orig(y);
    }

    private static void Render(On_Main.orig_DrawSunAndMoon orig, Main self, Main.SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)
    {
        Main.spriteBatch.End(out var ss);
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.identity);

        var sunWorldPosition = CelestialBodyPosition + Main.screenPosition;
        RenderSkyGradient(sunWorldPosition);

        Rainbows.Render();

        // Ordinarily this gets called from the god rays
        // system, but that seems to only be active during the day.
        // So, if it's night time, this background is the one
        // responsible for the clouds instead.
        if (!Main.dayTime)
            AtmosphereCloudRenderingSystem.RenderCloudsToBackground();

        Main.spriteBatch.Restart(ss);

        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
    }

    /// <summary>
    ///     Calculates the appropriately scaled Rayleigh scattering
    ///     coefficients based on a set of RGB/wavelength mappings, in
    ///     accordance with the Rayleigh scattering cross‑section equation.
    ///     <br></br>
    ///     These mappings indicate what the wavelength of light is for
    ///     a given color channel, in meters.
    /// </summary>
    /// <remarks>
    ///     Since the denominator of the equation used here is governed by
    ///     the wavelength taken o the fourth power, larger wavelengths will
    ///     generally scatter away unless a ray travels a great enough
    ///     distance through the atmospheric medium, e.g. when the
    ///     sun is at a sunrise/sunset angle.
    /// </remarks>
    /// <param name="wavelengthsMeters">The RGB wavelength mappings, measured in meters.</param>
    /// <param name="refractiveIndex">The refractive index of the atmospheric medium.</param>
    private static Vector3 CalculateRayleighScatterCoefficients(Vector3 wavelengthsMeters, float refractiveIndex)
    {
        var eightPiCubed = Math.Pow(Math.PI, 3D) * 8f;
        var molecularNumberDensity = 2.545e25;

        var r4 = Math.Pow(wavelengthsMeters.X, 4D);
        var g4 = Math.Pow(wavelengthsMeters.Y, 4D);
        var b4 = Math.Pow(wavelengthsMeters.Z, 4D);

        var numerator = eightPiCubed * MathF.Pow(MathF.Pow(refractiveIndex, 2f) - 1f, 2f);

        var resultR = numerator / (r4 * molecularNumberDensity * 3D);
        var resultG = numerator / (g4 * molecularNumberDensity * 3D);
        var resultB = numerator / (b4 * molecularNumberDensity * 3D);

        return new Vector3((float)resultR, (float)resultG, (float)resultB);
    }

    /// <summary>
    ///     Calculate the color tint factor based on the player's
    ///     current biome context, in order to make the blue sky
    ///     change slightly to fit certain biomes on the surface better.
    /// </summary>
    /// <remarks>
    ///     Note that the rules of Rayleigh scattering still apply after
    ///     color multiplication. This means that colors with a greater
    ///     wavelength (redder colors naturally) will naturally be
    ///     suppressed, and the corresponding factors required to
    ///     increase the prevalence of these colors will be much greater.
    /// </remarks>
    private static Vector3 CalculateBiomeColorInfluence()
    {
        Main.InfoToSetBackColor info = default;
        info.isInGameMenuOrIsServer = Main.gameMenu || Main.netMode == NetmodeID.Server;
        info.CorruptionBiomeInfluence = Math.Clamp(Main.SceneMetrics.EvilTileCount / (float)SceneMetrics.CorruptionTileMax, 0f, 1f);
        info.CrimsonBiomeInfluence = Math.Clamp(Main.SceneMetrics.BloodTileCount / (float)SceneMetrics.CrimsonTileMax, 0f, 1f);
        info.JungleBiomeInfluence = Math.Clamp(Main.SceneMetrics.JungleTileCount / (float)SceneMetrics.JungleTileMax, 0f, 1f);
        info.MushroomBiomeInfluence = Main.SmoothedMushroomLightInfluence;
        info.GraveyardInfluence = Main.GraveyardVisualIntensity;
        info.BloodMoonActive = Main.bloodMoon || Main.SceneMetrics.BloodMoonMonolith;
        info.LanternNightActive = LanternNight.LanternsUp;

        // Jungle influence is more or less irrelevant, since
        // the jungle background textures have sone translucent
        // green pixels at the top that tint the sky
        // naturally.
        var color = Vector3.One;
        color = Vector3.Lerp(color, new Vector3(1f, 0.52f, 0.45f), info.CorruptionBiomeInfluence);
        color = Vector3.Lerp(color, new Vector3(1f, 0.6f, 0.4f), info.CrimsonBiomeInfluence);
        color = Vector3.Lerp(color, new Vector3(0.2f, 0.3f, 0.4f), info.MushroomBiomeInfluence);
        color = Vector3.Lerp(color, new Vector3(1f, 0.5f, 0.35f), info.GraveyardInfluence);

        return color;
    }

    /// <summary>
    ///     Renders the atmospheric gradient to the background.
    /// </summary>
    /// <param name="sunMoonWorldPosition">The world position of the sun/moon, depending on whichever is active currently.</param>
    private static void RenderSkyGradient(Vector2 sunMoonWorldPosition)
    {
        var darkeningFactor = Utils.GetLerpValue(0f, 0.06f, DayProgress, true) * Utils.GetLerpValue(1f, 0.94f, DayProgress, true);

        var wavelengthNanometers = new Vector3(690f, 550f, 440f);
        var wavelengthMeters = wavelengthNanometers * 1e-9f;

        var viewportSize = new Vector2(Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);
        var viewportArea = new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);

        var shader = AssetReferences.Assets.Sky.RayleighScatteringShader.CreateAutoloadPass();
        shader.Parameters.globalTime = Main.GlobalTimeWrappedHourly * 0.3f;
        shader.Parameters.zoom = Vector2.One;
        shader.Parameters.screenPosition = Main.screenPosition;
        shader.Parameters.screenSize = viewportSize;
        shader.Parameters.worldSize = new Vector3(Main.maxTilesX, Main.maxTilesY, 3000f) * 16f;
        shader.Parameters.radii = shader.Parameters.worldSize * new Vector3(25.2f, 1f, 1f) * 0.5f;
        shader.Parameters.sunlightFactor = new Vector3(1f + LowSun * 0.4f, 0.9f - LowSun * 0.65f, 1f + LowSun * 0.6f) * CalculateBiomeColorInfluence();
        shader.Parameters.sunPosition = new Vector3(sunMoonWorldPosition, 3300f);
        shader.Parameters.scatterCoefficients = CalculateRayleighScatterCoefficients(wavelengthMeters, 1.00037f);
        shader.Parameters.saturationBoost = AtmosphereSaturationBoost;
        shader.Apply();

        var pixel = TextureAssets.MagicPixel.Value;
        Main.spriteBatch.Draw(pixel, viewportArea, Color.White * darkeningFactor);
    }

    /// <summary>
    ///     Renders the clouds to the background.
    /// </summary>
    public static void RenderCloudsToBackground()
    {
        if (DensityFieldSystem.DensityField is null)
            return;

        var skyline = Main.maxTilesY * 16f * 0.133f;
        var viewportSize = new Vector2(Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);
        var cloudDrawPosition = viewportSize * new Vector2(0.5f, 0.22f);
        cloudDrawPosition.Y += (skyline - Main.screenPosition.Y) * 0.12f;

        Main.spriteBatch.End(out var ss);
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.identity);

        var tint = Vector3.One;

        var reddening = MathF.Pow(LowSun, 3f);
        tint += new Vector3(1f, 0.76f - reddening * 0.6f, 0.68f - reddening * 0.15f) * LowSun * 2f;

        var nightBrightness = 0.45f + MoonlightGlowInterpolant * 8f;
        var nightCompletion = (float)(Main.time / Main.nightLength);
        if (Main.dayTime)
            nightCompletion = 0f;

        var nightBump = MathF.Sin(MathF.PI * nightCompletion);
        var scatterBias = 1f + nightBump * nightBrightness * 0.95f;

        var darkeningFactor = MathF.Pow(1f - LowSun, 0.54f);
        tint = Vector3.Lerp(tint, Vector3.One, MathF.Sqrt(1f - darkeningFactor));
        tint *= Interpolate.Lerp(nightBrightness, 1f, darkeningFactor);

        if (!Main.dayTime)
        {
            var moonTintingBias = Utils.GetLerpValue(0f, 0.125f, nightBump, true);
            var moonTint = MoonColorTintCalculator.Get(Main.moonType).ToVector3();
            var moonTintingFactor = Vector3.Lerp(Vector3.One, Vector3.One * 0.5f + moonTint * 0.72f, moonTintingBias);
            tint *= moonTintingFactor;
        }

        var rainInfluence = Vector3.Lerp(Vector3.One, new Vector3(0.5f, 0.58f, 0.7f), RainInterpolant);
        tint *= rainInfluence;

        var skyColor = ColorBeforeAtmoDarkening;
        var skyColorHsl = Main.rgbToHsl(skyColor);
        skyColorHsl.Y *= CloudDesaturationFactor;
        skyColor = Main.hslToRgb(skyColorHsl);

        var sunMoonWorldPosition = CelestialBodyPosition + Main.screenPosition;

        var shader = AssetReferences.Assets.Sky.RealisticCloudShader.CreateAutoloadPass();
        shader.Parameters.densityPosterizationLevel = 15.4f;
        shader.Parameters.pixelationLevel = 3f;
        shader.Parameters.horizontalScroll = Main.screenPosition.X / 30900f;
        shader.Parameters.screenPosition = Main.screenPosition;
        shader.Parameters.screenSize = viewportSize;
        shader.Parameters.cloudSize = CloudSize;
        shader.Parameters.sunlightFactor = skyColor.ToVector3() * new Vector3(0.91f, 1f, 1f) * tint;
        shader.Parameters.sunPosition = new Vector3(sunMoonWorldPosition, 8500f);
        shader.Parameters.scatterCoefficients = CalculateRayleighScatterCoefficients(new Vector3(400f, 400f, 400f) * 1e-9f, 1.00037f) * scatterBias;
        shader.Parameters.extinctionCoefficients = new Vector3(1f, 1f, 1f) * MathF.Cbrt(scatterBias) * 0.0021f;
        shader.Parameters.phaseAnisotropy = 0.81f;
        shader.Parameters.fieldSqrtDepth = DensityFieldSystem.SqrtDepth;
        shader.Parameters.fieldTargetSize2D = DensityFieldSystem.BaselineTargetSize.ToVector2();
        shader.Parameters.noiseTexture = new HlslSampler2D
        {
            Texture = AssetReferences.Assets.Noise.CloudyNoise.Asset.Value,
            Sampler = SamplerState.LinearWrap
        };
        shader.Apply();

        var field = DensityFieldSystem.DensityField.Target;
        var scale = new Vector2(CloudSize.X, CloudSize.Y) / field.Size();
        Main.spriteBatch.Draw(field, cloudDrawPosition, null, Color.White, 0f, field.Size() * 0.5f, scale, 0, 0f);

        Main.spriteBatch.Restart(ss);
    }

    public override void ModifySunLightColor(ref Color backgroundColor, ref Color tileLightColor)
    {
        var darkeningBump = Utils.GetLerpValue(0f, 0.13f, DayProgress, true) * Utils.GetLerpValue(1f, 0.87f, DayProgress, true);
        var darkeningFactor = MathF.Pow(darkeningBump, 0.8f);
        var eveningColorBias = LowSun * Interpolate.Lerp(1f, 0.2f, RainInterpolant) * 0.85f;
        var nightColor = Color.Lerp(Color.Black, Color.White, MoonlightGlowInterpolant);

        backgroundColor = Color.Lerp(backgroundColor, LowSunTintColor, eveningColorBias);
        backgroundColor = Color.Lerp(backgroundColor, nightColor, 1f - darkeningFactor);

        tileLightColor = Color.Lerp(tileLightColor, LowSunTintColor, eveningColorBias);
        tileLightColor = Color.Lerp(tileLightColor, nightColor, 1f - darkeningFactor);
    }

    public override void ClearWorld() => CelestialBodyPosition = Vector2.Zero;

    public override void PostUpdateNPCs()
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        for (var i = 0; i < Main.maxClouds; i++)
            Main.cloud[i].active = false;

        var viewportSize = new Vector2(Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);
        var trueBodyPosition = Main.LastCelestialBodyPosition * viewportSize;
        if (CelestialBodyPosition == Vector2.Zero || Main.dayTime)
            CelestialBodyPosition = trueBodyPosition;

        var origin = viewportSize * 0.5f;
        var trueOffset = (trueBodyPosition - origin).SafeNormalize(Vector2.UnitY);
        var currentOffset = (CelestialBodyPosition - origin).SafeNormalize(Vector2.UnitY);
        var offsetOrthogonality = Math.Clamp(Vector2.Dot(trueOffset, currentOffset), -1f, 1f);
        var angularDiscrepancy = MathF.Acos(offsetOrthogonality);

        // This ensures that the angular correction only spins
        // clockwise, exactly like the cycles of the sun and moon
        // in the sky.
        // This should result in a more natural-looking turn-around
        // effect as day/night change compared to a linear
        // interpolation, which would just zip linearly to the
        // destination and not respect rotation.
        var spin = angularDiscrepancy * 0.04f;

        // If the angular discrepancy is really
        // really low, however, then it's fine to go backwards
        // a little bit as a treat.
        if (angularDiscrepancy <= 0.05f)
        {
            var wedgeProduct = trueOffset.X * currentOffset.Y - trueOffset.Y * currentOffset.X;
            var fastestDirection = MathF.Sign(wedgeProduct);
            spin *= fastestDirection;
        }

        CelestialBodyPosition = (CelestialBodyPosition - origin).RotatedBy(spin) + origin;
    }
}
