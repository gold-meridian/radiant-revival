using Daybreak.Common.CIL;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Core;
using System;
using System.Diagnostics;
using Terraria;
using Terraria.GameContent.Drawing;

namespace RadiantRevival.Common;

// TODO: Config, DrawCapture
public static class Godrays
{
    private static WrapperShaderData<Assets.Sky.Godrays.Parameters>? godraysShaderData;
    private static WrapperShaderData<Assets.Sky.GodraysSampler.Parameters>? blurShaderData;

    private static RenderTargetLease? celestialBodyLease;

    [OnLoad]
    private static void Load()
    {
        godraysShaderData = Assets.Sky.Godrays.CreateGodraysShader();
        blurShaderData = Assets.Sky.GodraysSampler.CreateRadialBlurShader();

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

                celestialBodyLease?.Dispose();
                celestialBodyLease = ScreenspaceTargetPool.Shared.Rent(Main.graphics.GraphicsDevice);

                scope = celestialBodyLease.Scope(clearColor: Color.Transparent);

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
                    if (celestialBodyLease is null || scope is null)
                    {
                        return;
                    }

                    using var _ = Main.spriteBatch.Scope();

                    scope?.Dispose();

                    Main.spriteBatch.Begin();
                    {
                        Main.spriteBatch.Draw(celestialBodyLease.Target, Vector2.Zero, Color.White);
                    }
                    Main.spriteBatch.End();
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
        if (!Main.dayTime || !Main.ForegroundSunlightEffects || Main.screenTarget is null || celestialBodyLease is null)
        {
            orig();
            return;
        }

        Draw(Main.spriteBatch, Main.graphics.GraphicsDevice, Main.screenTarget);

        orig();
    }

    private static void Draw(SpriteBatch sb, GraphicsDevice device, RenderTarget2D target)
    {
        Debug.Assert(godraysShaderData is not null && blurShaderData is not null && celestialBodyLease is not null);

        const int godrays_samples = 32;
        const int radial_blur_samples = 16;
        const float radial_blur_strength = 0.25f;

        var screenSize = new Vector2(Main.screenWidth, Main.screenHeight);

        var lightPosition = Main.LastCelestialBodyPosition * screenSize;

        HorizonHelper.GetCelestialBodyColors(out var sunColor, out var _);

        sunColor = sunColor.MultiplyRGB(Color.PeachPuff);

        NextHorizonRenderer.GetVisibilities(out var sunsetVisibility, out var sunriseVisibility, out var celestialVisibility);

        var color = sunColor;

        var num = Math.Max(sunsetVisibility, sunriseVisibility) * celestialVisibility;

        color *= num;

        if (color is not { R: > 0, G: > 0, B: > 0 })
        {
            return;
        }

        using var lease = ScreenspaceTargetPool.Shared.Rent(device, (int)screenSize.X / 4, (int)screenSize.Y / 4);

        using var _ = sb.Scope();

        using (lease.Scope(clearColor: Color.Transparent))
        {
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);

            godraysShaderData.Parameters.light_position = lightPosition;
            godraysShaderData.Parameters.sample_count = godrays_samples;
            godraysShaderData.Parameters.decay_mult = 0.92f;

            godraysShaderData.Parameters.lights = new HlslSampler2D
            {
                Texture = celestialBodyLease.Target,
                Sampler = SamplerState.LinearClamp,
            };

            godraysShaderData.Apply();

            sb.Draw(target, device.Viewport.Bounds, color);
            sb.End();
        }

        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);

        blurShaderData.Parameters.light_position = lightPosition;
        blurShaderData.Parameters.sample_count = radial_blur_samples;
        blurShaderData.Parameters.blur_strength = radial_blur_strength;

        blurShaderData.Apply();

        sb.Draw(lease.Target, device.Viewport.Bounds, Color.White);

        sb.End();

        celestialBodyLease.Dispose();
        celestialBodyLease = null;
    }
}
