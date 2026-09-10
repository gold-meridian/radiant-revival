using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Mathematics;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Core;
using System;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace RadiantRevival.Common.Rendering.Sky;

/// <summary>
///     The system responsible for the management of
///     atmosphere and clouds rendering.
/// </summary>
public static class AtmosphereCloudRenderingSystem
{
    private static RenderTargetLease? atmosphereLease;

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
    ///     The profile that dictates all artistically relevant
    ///     details of the sky.
    /// </summary>
    public static SkyProfile Profile
    {
        get;
        set;
    } = InitializeDefaultProfile();

    /// <summary>
    ///     The fixed relative screen size of the game from
    ///     the perspective of this system.
    /// </summary>
    public static Vector2 FixedScreenSize => new(2560f, 1440f);

    /// <summary>
    ///     The position of the screen.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="Main.screenPosition"/>, this
    ///     property works equivalently with this system on the game
    ///     menu.
    /// </remarks>
    public static Vector2 ScreenPosition
    {
        get
        {
            if (Main.gameMenu)
                return new Vector2(Main.maxTilesX * 16f, Main.maxTilesY * 16f) * new Vector2(0.5f, 0.145f);

            return Main.screenPosition;
        }
    }

    /// <summary>
    ///     The size of the cloud box in the sky.
    /// </summary>
    public static Vector3 CloudSize => new(6300f, 1700f, 850f);

    [OnLoad]
    private static void Load()
    {
        IL_Main.SetBackColor += DisableTypicalSunriseSunsetLighting;
        IL_Main.DrawSurfaceBG += RemoveDefaultCloudBackground;
        On_Main.UpdateAtmosphereTransparencyToSkyColor += DisableAtmosphereBackgroundDarkening;
        On_Main.DrawSunAndMoon += Render;

        MonoModHooks.Add(
            typeof(SystemLoader).GetMethod(
                nameof(SystemLoader.ModifySunLightColor),
                BindingFlags.Public | BindingFlags.Static
            ),
            ModifySunlightColor
        );
        IL_Main.ApplyColorOfTheSkiesToTiles += _ => { };

        On_Main.Update += Update;
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

        c.EmitDelegate((float bgAlpha) => DensityFieldSystem.ShouldBeDisabled ? 0f : bgAlpha);
    }

    private static void DisableAtmosphereBackgroundDarkening(On_Main.orig_UpdateAtmosphereTransparencyToSkyColor orig, float y)
    {
        ColorBeforeAtmoDarkening = Main.ColorOfTheSkies;
        orig(y);
    }

    private static void Render(On_Main.orig_DrawSunAndMoon orig, Main self, Main.SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)
    {
        Main.spriteBatch.End(out var ss);
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.BackgroundViewMatrix.EffectMatrix);

        var sunWorldPosition = CelestialBodyPosition + ScreenPosition;
        RenderSkyGradient(Profile, sunWorldPosition);

        Rainbows.Render(Profile);

        // Ordinarily this gets called from the god rays
        // system, but that seems to only be active during the day.
        // So, if it's night time, this background is the one
        // responsible for the clouds instead.
        if (!Main.dayTime)
            AtmosphereCloudRenderingSystem.RenderCloudsToBackground(Profile);

        Main.spriteBatch.Restart(ss);

        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
    }

    private static SkyProfile InitializeDefaultProfile()
    {
        var snowBiome = new SkyProfileInfluence(p => Math.Clamp(Main.SceneMetrics.EvilTileCount / (float)SceneMetrics.CorruptionTileMax, 0f, 1f))
        {
            AtmosphereTintColor = Vector3.One,
            RainbowTintColor = Color.White,
            OverridingSurfaceTemperature = 19.5f,
            InfluencePriority = -1
        };

        var corruptionBiome = new SkyProfileInfluence(p => Math.Clamp(Main.SceneMetrics.EvilTileCount / (float)SceneMetrics.CorruptionTileMax, 0f, 1f))
        {
            AtmosphereTintColor = new Vector3(2.4f, 0.82f, 0.38f),
            RainbowTintColor = new Color(85, 255, 174) * 0.75f,
            InfluencePriority = 0,

            RedTermAuroraTint = Vector3.UnitZ * 1.2f,
            BlueTermAuroraTint = Vector3.UnitY * -0.95f,

            AuroraBackgroundTintColor = new Color(156, 209, 74)
        };
        var crimsonBiome = new SkyProfileInfluence(p => Math.Clamp(Main.SceneMetrics.BloodTileCount / (float)SceneMetrics.CrimsonTileMax, 0f, 1f))
        {
            AtmosphereTintColor = new Vector3(1f, 0.6f, 0.4f),
            RainbowTintColor = new Color(255, 50, 50) * 0.75f,
            InfluencePriority = 0,

            GreenTermAuroraTint = new Vector3(0.4f, -1f, 0.2f),
            RedTermAuroraTint = Vector3.UnitZ * 2.2f,

            AuroraBackgroundTintColor = new Color(198, 12, 16)
        };
        var hallowBiome = new SkyProfileInfluence(p => Math.Clamp(Main.SceneMetrics.HolyTileCount / (float)SceneMetrics.HallowTileMax, 0f, 1f))
        {
            AtmosphereTintColor = new Vector3(1f, 1f, 1f),
            RainbowTintColor = Color.White,
            InfluencePriority = 0,

            // The player gets to witness the Carrington event.
            RedTermAuroraTint = new Vector3(0.8f, 0.2f, 0.8f),
            GreenTermAuroraTint = new Vector3(0.8f, -0.4f, -0.8f),
            BlueTermAuroraTint = new Vector3(0.2f, 0.2f, 0.8f),

            AuroraBackgroundTintColor = new Color(211, 62, 196)
        };
        var graveyardBiome = new SkyProfileInfluence(p => Math.Clamp(Main.SceneMetrics.GraveyardTileCount / (float)SceneMetrics.GraveyardTileThreshold, 0f, 1f))
        {
            AtmosphereTintColor = new Vector3(1f, 0.5f, 0.35f),
            RainbowTintColor = new Color(105, 105, 105) * 0.5f,
            InfluencePriority = 1
        };
        var mushroomBiome = new SkyProfileInfluence(p => Math.Clamp(Main.SceneMetrics.MushroomTileCount / (float)SceneMetrics.MushroomTileMax, 0f, 1f))
        {
            AtmosphereTintColor = new Vector3(0.2f, 0.3f, 0.4f),
            RainbowTintColor = new Color(50, 50, 255) * 0.84f,
            InfluencePriority = 2
        };
        var eclipse = new SkyProfileInfluence(p => Main.eclipseLight)
        {
            AtmosphereTintColor = new Vector3(0.1f, 0.025f, 0.005f),
            RainbowTintColor = new Color(40, 40, 40) * 0.5f,
            InfluencePriority = 10
        };

        return new SkyProfile(snowBiome, corruptionBiome, crimsonBiome, hallowBiome, graveyardBiome, mushroomBiome, eclipse)
        {
            AtmosphereSaturationBoost = 0.2f,
            CloudSaturationFactor = 0.5f,
            ColorWavelengthsNanometers = new Vector3(690f, 550f, 440f),
            LowSunTintColor = new Color(255, 25, 15),
            CloudRainColorTint = new Vector3(0.5f, 0.58f, 0.7f),
            LowSunColorExaggerationFunction = LowSunColorExaggerationFunction
        };
    }

    private static Vector3 LowSunColorExaggerationFunction(float x)
    {
        var xCubed = MathF.Pow(x, 3f);
        return new Vector3(1f, 0.76f - xCubed * 0.6f, 0.68f - xCubed * 0.15f) * x * 2f;
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
    ///     the wavelength taken to the fourth power, larger wavelengths will
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
        var color = Vector3.One;
        foreach (var influence in Profile.Influences)
            color = Vector3.Lerp(color, influence.AtmosphereTintColor, influence.InfluenceFunction(Main.LocalPlayer));

        return color;
    }

    /// <summary>
    ///     Renders the atmospheric gradient to the background.
    /// </summary>
    /// <param name="sunMoonWorldPosition">The world position of the sun/moon, depending on whichever is active currently.</param>
    private static void RenderSkyGradient(SkyProfile profile, Vector2 sunMoonWorldPosition)
    {
        const float day_length = (float)Main.dayLength;

        const float dawn_time = 9200f / day_length;
        const float dusk_start_time = 33000f / day_length;

        var sb = Main.spriteBatch;

        using (sb.Scope())
        {
            atmosphereLease ??= ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, (w, h) => (w / 2, h / 2));
            using (atmosphereLease.Scope(clearColor: Color.Transparent))
            {
                using var _ = Main.spriteBatch.Scope();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

                var darkeningFactor = Utils.GetLerpValue(0f, dawn_time, DayProgress, true) * Utils.GetLerpValue(1f, dusk_start_time, DayProgress, true);
                darkeningFactor = MathF.Pow(darkeningFactor, 1.5f);

                var wavelengthNanometers = profile.ColorWavelengthsNanometers;
                var wavelengthMeters = wavelengthNanometers * 1e-9f;

                var viewportArea = new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);

                var s = 1f + profile.AtmosphereSaturationBoost;
                var inverseS = 1f - s;
                var luminanceVector = new Vector3(0.3f, 0.6f, 0.1f);
                var r = Vector3.One * luminanceVector.X * inverseS + Vector3.UnitX * s;
                var g = Vector3.One * luminanceVector.Y * inverseS + Vector3.UnitY * s;
                var b = Vector3.One * luminanceVector.Z * inverseS + Vector3.UnitZ * s;
                var saturationMatrix = new Matrix(
                    r.X,
                    r.Y,
                    r.Z,
                    0f,
                    g.X,
                    g.Y,
                    g.Z,
                    0f,
                    b.X,
                    b.Y,
                    b.Z,
                    0f,
                    0f,
                    0f,
                    0f,
                    1f
                );

                var depth = Main.gameMenu ? 1485f : 3000f;

                var shader = AssetReferences.Assets.Sky.RayleighScatteringShader.CreateAutoloadPass();
                shader.Parameters.globalTime = Main.GlobalTimeWrappedHourly * 0.3f;
                shader.Parameters.zoom = Vector2.One;
                shader.Parameters.screenPosition = ScreenPosition;
                shader.Parameters.screenSize = FixedScreenSize;
                shader.Parameters.worldSize = new Vector3(Main.maxTilesX, Main.maxTilesY, depth) * 16f;
                shader.Parameters.radii = shader.Parameters.worldSize * new Vector3(25.2f, 1f, 1f) * 0.5f;
                shader.Parameters.sunlightFactor = new Vector3(1f + LowSun * 0.4f, 0.9f - LowSun * 0.65f, 1f + LowSun * 0.6f) * CalculateBiomeColorInfluence();
                shader.Parameters.sunPosition = new Vector3(sunMoonWorldPosition, 3300f);
                shader.Parameters.scatterCoefficients = CalculateRayleighScatterCoefficients(wavelengthMeters, 1.00037f);
                shader.Parameters.saturationBoostMatrix = saturationMatrix;
                shader.Apply();

                var pixel = TextureAssets.MagicPixel.Value;
                Main.spriteBatch.Draw(pixel, viewportArea, Color.White * darkeningFactor);
            }
        }

        Main.spriteBatch.Draw(atmosphereLease.Target, new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height), Color.White);
    }

    /// <summary>
    ///     Renders the clouds to the background.
    /// </summary>
    public static void RenderCloudsToBackground(SkyProfile profile)
    {
        if (DensityFieldSystem.ShouldBeDisabled)
            return;

        if (DensityFieldSystem.DensityField is null)
            return;

        var skyline = Main.maxTilesY * 16f * 0.133f;
        var viewportSize = new Vector2(Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);
        var cloudDrawPosition = viewportSize * new Vector2(0.5f, 0.22f);
        cloudDrawPosition.Y += (skyline - ScreenPosition.Y) * 0.12f;

        Main.spriteBatch.End(out var ss);
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.identity);

        var tint = Vector3.One;
        tint += profile.LowSunColorExaggerationFunction(LowSun);

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

        var rainInfluence = Vector3.Lerp(Vector3.One, profile.CloudRainColorTint, RainInterpolant);
        tint *= rainInfluence;

        var skyColor = ColorBeforeAtmoDarkening;
        var skyColorHsl = Main.rgbToHsl(skyColor);
        skyColorHsl.Y *= profile.CloudSaturationFactor;
        skyColor = Main.hslToRgb(skyColorHsl);

        var sunMoonWorldPosition = CelestialBodyPosition + ScreenPosition;

        var shader = AssetReferences.Assets.Sky.RealisticCloudShader.CreateAutoloadPass();
        shader.Parameters.densityPosterizationLevel = 15.4f;
        shader.Parameters.pixelationLevel = 3f;
        shader.Parameters.horizontalScroll = ScreenPosition.X / 30900f;
        shader.Parameters.screenPosition = ScreenPosition;
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

    private static void ModifySunlightColor(SystemLoader.DelegateModifySunLightColor orig, ref Color backgroundColor, ref Color tileLightColor)
    {
        var darkeningBump = Utils.GetLerpValue(0f, 0.13f, DayProgress, true) * Utils.GetLerpValue(1f, 0.87f, DayProgress, true);
        var darkeningFactor = MathF.Pow(darkeningBump, 0.8f);
        var eveningColorBias = LowSun * Interpolate.Lerp(1f, 0.2f, RainInterpolant) * 0.85f;
        var nightColor = Color.Lerp(Color.Black, Color.White, MoonlightGlowInterpolant);

        backgroundColor = Color.Lerp(backgroundColor, Profile.LowSunTintColor, eveningColorBias);
        backgroundColor = Color.Lerp(backgroundColor, nightColor, 1f - darkeningFactor);

        tileLightColor = Color.Lerp(tileLightColor, Profile.LowSunTintColor, eveningColorBias);
        tileLightColor = Color.Lerp(tileLightColor, nightColor, 1f - darkeningFactor);

        orig(ref backgroundColor, ref tileLightColor);
    }

    private static void Update(On_Main.orig_Update orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);

        var gamePaused = Main.gamePaused && !Main.gameMenu;
        if (gamePaused)
            return;

        if (!DensityFieldSystem.ShouldBeDisabled)
        {
            for (var i = 0; i < Main.maxClouds; i++)
                Main.cloud[i].active = false;
        }

        var trueBodyPosition = Main.LastCelestialBodyPosition * FixedScreenSize;
        if (CelestialBodyPosition == Vector2.Zero || Main.dayTime)
            CelestialBodyPosition = trueBodyPosition;

        var origin = FixedScreenSize * 0.5f;
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
