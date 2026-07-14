using Daybreak.Common.CIL;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Common.Rendering.Sky;
using RadiantRevival.Core;
using System;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

// TODO: Config, DrawCapture
public static class Godrays
{
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.Sky.Godrays.Parameters> GodraysShader { get; init; }

        public required WrapperShaderData<Assets.Sky.GodraysSampler.Parameters> BlurShader { get; init; }

        public required RenderTargetLease CelestialBodyTarget { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    GodraysShader = Assets.Sky.Godrays.CreateGodraysShader(),
                    BlurShader = Assets.Sky.GodraysSampler.CreateRadialBlurShader(),
                    CelestialBodyTarget = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice),
                }
            ).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data)
        {
            Main.RunOnMainThread(
                () =>
                {
                    data.CelestialBodyTarget.Dispose();
                }
            );
        }
    }

    private static RenderTargetLease CelestialBodyTarget => Data.Instance.CelestialBodyTarget;

    [OnLoad]
    private static void Load()
    {
        IL_Main.DrawSunAndMoon += DrawSunAndMoon_CaptureCelestialBodies;
        On_Main.DrawLensFlare += DrawLensFlare_Godrays;
    }

    private static void DrawSunAndMoon_CaptureCelestialBodies(ILContext il)
    {
        var c = new ILCursor(il);

        var scopeDef = il.AddVariable<RenderTargetScope?>();

        c.EmitLdloca(scopeDef);

        c.EmitDelegate(
            static (ref RenderTargetScope? scope) =>
            {
                if (!Main.dayTime || !Main.ForegroundSunlightEffects || Main.screenTarget is null)
                {
                    return;
                }

                Main.spriteBatch.End(out var ss);

                scope = CelestialBodyTarget.Scope(clearColor: Color.Transparent);

                Main.spriteBatch.Begin(in ss);
            }
        );

        while (c.TryGotoNext(
                   MoveType.Before,
                   i => i.MatchRet()
               ))
        {
            c.MoveAfterLabels();

            c.EmitLdloca(scopeDef);

            c.EmitDelegate(
                static (ref RenderTargetScope? scope) =>
                {
                    if (scope is null)
                    {
                        return;
                    }

                    AtmosphereCloudRenderingSystem.RenderCloudsToBackground(AtmosphereCloudRenderingSystem.Profile);
                    scope?.Dispose();

                    var sb = Main.spriteBatch;
                    using var _ = sb.Scope();

                    sb.Begin();
                    {
                        sb.Draw(CelestialBodyTarget.Target, Vector2.Zero, Color.White);
                    }
                    sb.End();
                }
            );

            c.GotoNext(
                MoveType.After,
                i => i.MatchRet()
            );
        }
    }

    private static void DrawLensFlare_Godrays(On_Main.orig_DrawLensFlare orig)
    {
        if (!Main.dayTime || !Main.ForegroundSunlightEffects || Main.screenTarget is null)
        {
            orig();
            return;
        }

        Draw(Main.spriteBatch, Main.graphics.GraphicsDevice, Main.screenTarget);

        orig();
    }

    private static void Draw(SpriteBatch sb, GraphicsDevice device, RenderTarget2D target)
    {
        const int godrays_samples = 32;
        const int radial_blur_samples = 16;
        const float radial_blur_strength = 0.25f;

        var godraysShader = Data.Instance.GodraysShader;
        var blurShader = Data.Instance.BlurShader;

        var screenSize = new Vector2(Main.screenWidth, Main.screenHeight);

        var lightPosition = Main.LastCelestialBodyPosition * screenSize;

        if (Main.GameViewMatrix.Effects.HasFlag(SpriteEffects.FlipVertically))
        {
            lightPosition.Y = screenSize.Y - lightPosition.Y;
        }

        HorizonHelper.GetCelestialBodyColors(out var sunColor, out var _);

        sunColor = sunColor.MultiplyRGB(Color.PeachPuff);

        NextHorizonRenderer.GetVisibilities(out var sunsetVisibility, out var sunriseVisibility, out var celestialVisibility);

        var color = sunColor;

        var num = Math.Max(sunsetVisibility, sunriseVisibility) * celestialVisibility;

        color *= num;

        if (color is { R: <= 0, G: <= 0, B: <= 0 })
        {
            return;
        }

        using var lease = ScreenspaceTargetPool.Shared.Rent(device, (int)screenSize.X / 4, (int)screenSize.Y / 4);

        using var _ = sb.Scope();

        using (lease.Scope(clearColor: Color.Transparent))
        {
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);
            {
                godraysShader.Parameters.LightPosition = lightPosition;
                godraysShader.Parameters.SampleCount = godrays_samples;
                godraysShader.Parameters.DecayMult = 0.92f;

                godraysShader.Parameters.LightsTexture = new HlslSampler2D
                {
                    Texture = CelestialBodyTarget.Target,
                    Sampler = SamplerState.LinearClamp,
                };

                godraysShader.Apply();

                sb.Draw(target, device.Viewport.Bounds, color);
            }
            sb.End();
        }

        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);
        {
            blurShader.Parameters.LightPosition = lightPosition;
            blurShader.Parameters.SampleCount = radial_blur_samples;
            blurShader.Parameters.BlurStrength = radial_blur_strength;

            blurShader.Apply();

            sb.Draw(lease.Target, device.Viewport.Bounds, Color.White);
        }
        sb.End();
    }
}
