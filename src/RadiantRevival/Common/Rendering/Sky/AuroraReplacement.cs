using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Mathematics;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Core;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Skies;
using Terraria.Graphics;
using Terraria.Graphics.Effects;

namespace RadiantRevival.Common.Rendering.Sky;

/// <summary>
///     The system responsible for the rendering of
///     auroras.
/// </summary>
public static class AuroraReplacement
{
    internal static RenderTargetLease? auroraLease;

    internal static RenderTargetLease? depthLease;

    [OnLoad]
    internal static void Load()
    {
        On_AuroraSky.DrawAuroraSky += ReplaceAurora;
        On_AuroraSky.Update += MakeFadeoutSlower;
        IL_Main.DrawSurfaceBG += RemoveHallowRainbow;
    }

    private static void ReplaceAurora(On_AuroraSky.orig_DrawAuroraSky orig, VertexStrip vertexStrip, float skyOpacity, ref Color lastSkyColor)
    {
        auroraLease ??= ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, (w, h) => (w / 2, h / 2));
        depthLease ??= ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, (w, h) => (w / 2, h / 2), RenderTargetDescriptor.Default with
        {
            Format = SurfaceFormat.Single
        });

        {
            var gd = Main.instance.GraphicsDevice;
            var previousBindings = gd.GetRenderTargets();
            gd.SetRenderTargets(
            [
                auroraLease.Target,
                depthLease.Target
            ]);

            RenderIntoTargets();

            gd.SetRenderTargets(previousBindings);
        }

        using var _ = Main.spriteBatch.Scope();
        var viewportArea = new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.BackgroundViewMatrix.EffectMatrix);
        Main.spriteBatch.Draw(auroraLease.Target, viewportArea, new Color(255, 255, 255, 0) * skyOpacity);
    }

    private static void RenderIntoTargets()
    {
        using var _ = Main.spriteBatch.Scope();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        var solarActivity = MathF.Cos(CloudWeatherVarianceSystem.WeatherTimer * 0.0019f) * 0.5f + 0.5f;
        solarActivity = Interpolate.Lerp(solarActivity, 0f, 0.6f);

        var redBandWidth = Interpolate.Lerp(24f, 40f, solarActivity);
        var greenBandWidth = Interpolate.Lerp(18f, 26f, solarActivity);
        var blueBandWidth = Interpolate.Lerp(20f, 35f, solarActivity);

        var hueMixingA = MathF.Sin(CloudWeatherVarianceSystem.WeatherTimer * 0.0151f) * 0.5f + 0.5f;
        var hueMixingB = MathF.Sin(CloudWeatherVarianceSystem.WeatherTimer * 0.0132f) * 0.5f + 0.5f;

        var shader = AssetReferences.Assets.Sky.AuroraShader.CreateAutoloadPass();
        shader.Parameters.time = Main.GlobalTimeWrappedHourly * 0.0425f;

        // Oxygen exists primarily within the 100-200km range
        // and creates red and greenish colors upon excitement.
        shader.Parameters.redExcitementHeightKilometers = 172f + MathF.Cos(CloudWeatherVarianceSystem.WeatherTimer * 0.0074f) * 11f - MathF.Sqrt(hueMixingA) * 20f;
        shader.Parameters.greenExcitementHeightKilometers = 110f + MathF.Cos(CloudWeatherVarianceSystem.WeatherTimer * 0.0095f) * 8f;

        // Nitrogen exists lower down, within the approximately
        // 90km range, and creates blueish colors upon excitement.
        shader.Parameters.blueExcitementHeightKilometers = 90f + MathF.Cos(CloudWeatherVarianceSystem.WeatherTimer * 0.0117f) * 6f;

        shader.Parameters.redContributionCoefficients = new Vector3(0.8f, 0.1f + hueMixingA * 0.3f, 0f);
        shader.Parameters.greenContributionCoefficients = new Vector3(hueMixingB, 1f - hueMixingB * 0.3f, 0.2f + hueMixingA * 0.225f);
        shader.Parameters.blueContributionCoefficients = new Vector3(0.24f + hueMixingA * 0.54f, 0f, 0.8f);

        var profile = AtmosphereCloudRenderingSystem.Profile;
        foreach (var influence in profile.Influences)
        {
            var influenceIntensity = influence.InfluenceFunction(Main.LocalPlayer);
            shader.Parameters.redContributionCoefficients += influenceIntensity * influence.RedTermAuroraTint;
            shader.Parameters.greenContributionCoefficients += influenceIntensity * influence.GreenTermAuroraTint;
            shader.Parameters.blueContributionCoefficients += influenceIntensity * influence.BlueTermAuroraTint;
        }

        shader.Parameters.colorBandWidths = new Vector3(redBandWidth, greenBandWidth, blueBandWidth);

        var offsetFromSurface = (float)(Main.screenPosition.Y - Main.worldSurface * 16f);
        if (offsetFromSurface > 0f)
            offsetFromSurface = 0f;

        shader.Parameters.bandClumping = 0.85f;
        shader.Parameters.baseHeight = 1.2f + offsetFromSurface / 13000f;
        shader.Parameters.heightSuppressionExponent = 1.28f;
        shader.Parameters.raymarchStepDecay = 1.3f;
        shader.Parameters.noiseTexture = new HlslSampler2D
        {
            Texture = AssetReferences.Assets.Noise.CloudyNoise.Asset.Value,
            Sampler = SamplerState.LinearWrap
        };
        shader.Apply();

        var pixel = TextureAssets.MagicPixel.Value;
        var viewportArea = new Rectangle(0, 0, Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);
        Main.spriteBatch.Draw(pixel, viewportArea, Color.White);
    }

    private static void MakeFadeoutSlower(On_AuroraSky.orig_Update orig, AuroraSky self, GameTime gameTime)
    {
        orig(self, gameTime);
        if (Main.gamePaused)
            return;

        if (self._isLeaving)
        {
            const float base_fadeout_rate = 0.5f;
            const float new_fadeout_rate = 0.18f;
            self._opacity += (float)(gameTime.ElapsedGameTime.TotalSeconds * (base_fadeout_rate - new_fadeout_rate));
        }
    }

    private static void RemoveHallowRainbow(ILContext il)
    {
        var c = new ILCursor(il);
        c.GotoNext(i => i.MatchLdcI4(18),
                   i => i.MatchCallOrCallvirt<Main>(nameof(Main.LoadBackground)));

        c.GotoNext(MoveType.After, i => i.MatchLdfld<Main>(nameof(Main.bgLoops)));
        c.EmitDelegate((int originalLoopCount) =>
        {
            // Is there an aurora? Well then the rainbow
            // has gotta GO! DIE! IT LOOKS SO BAD!!!
            if (SkyManager.Instance["Aurora"] is AuroraSky { _opacity: var opacity } && opacity > 0f)
                return 0;

            return originalLoopCount;
        });
    }

    [ModSystemHooks.ModifySunLightColor]
    private static void ReplaceColorTints(ref Color tileColor, ref Color backgroundColor)
    {
        if (SkyManager.Instance["Aurora"] is AuroraSky { _opacity: var opacity } && opacity > 0f)
        {
            var colorTint = new Color(53, 223, 76);
            var profile = AtmosphereCloudRenderingSystem.Profile;
            foreach (var influence in profile.Influences)
            {
                var localTint = influence.AuroraBackgroundTintColor;
                var alpha = localTint.A / 255f;
                var influenceIntensity = influence.InfluenceFunction(Main.LocalPlayer) * alpha;
                colorTint = Color.Lerp(colorTint, localTint, influenceIntensity);
            }

            var tintInterpolant = opacity * 0.125f;
            tileColor = Color.Lerp(tileColor, colorTint, tintInterpolant);
            backgroundColor = Color.Lerp(backgroundColor, colorTint, tintInterpolant);
        }
    }
}
