using System;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using System.Diagnostics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.UI.Chat;

namespace RadiantRevival.Common;

// TODO: Config, DrawCapture
public static class Godrays
{
    private static WrapperShaderData<Assets.Sky.Godrays.Parameters>? godraysShaderData;

    private static RenderTargetLease? celestialBodyLease;

    [OnLoad]
    private static void Load()
    {
        godraysShaderData = Assets.Sky.Godrays.CreateGodraysShader();

        On_Main.DrawSunAndMoon += DrawSunAndMoon_CaptureCelestialBodies;

        On_Main.DrawLensFlare += DrawLensFlare_Godrays;
    }

    private static void DrawSunAndMoon_CaptureCelestialBodies(On_Main.orig_DrawSunAndMoon orig, Main self, Main.SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)
    {
        // FIXME: Moon seems to render as an occluder?
        if (!Main.dayTime || !Main.ForegroundSunlightEffects || Main.screenTarget is null)
        {
            orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
            return;
        }

        celestialBodyLease?.Dispose();
        celestialBodyLease = ScreenspaceTargetPool.Shared.Rent(self.GraphicsDevice);

        using (celestialBodyLease.Scope(clearColor: Color.Transparent))
        {
            orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);
        }

        using var _ = Main.spriteBatch.Scope();

        Main.spriteBatch.Begin();

        Main.spriteBatch.Draw(celestialBodyLease.Target, Vector2.Zero, Color.White);
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
        Debug.Assert(godraysShaderData is not null && celestialBodyLease is not null);

        HorizonHelper.GetCelestialBodyColors(out var sunColor, out var _);

        sunColor = sunColor.MultiplyRGB(Color.PeachPuff);

        NextHorizonRenderer.GetVisibilities(out var sunsetVisibility, out var sunriseVisibility, out var celestialVisibility);

        Color color = sunColor;

        float num = Math.Max(sunsetVisibility, sunriseVisibility) * celestialVisibility;

        color *= num;

        if (color is not { R: > 0, G: > 0, B: > 0 })
        {
            return;
        }

        var screenSize = new Vector2(Main.screenWidth, Main.screenHeight);

        using var lease = ScreenspaceTargetPool.Shared.Rent(device, (int)screenSize.X / 4, (int)screenSize.Y / 4);

        using var _ = sb.Scope();

        using (lease.Scope(clearColor: Color.Transparent))
        {
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);

            godraysShaderData.Parameters.light_position = Main.LastCelestialBodyPosition * screenSize;
            godraysShaderData.Parameters.light_size = 4.6f;
            godraysShaderData.Parameters.sample_count = 64;
            godraysShaderData.Parameters.decay_mult = 0.925f;

            godraysShaderData.Parameters.lights = new HlslSampler2D
            {
                Texture = celestialBodyLease.Target,
                Sampler = SamplerState.LinearClamp
            };

            godraysShaderData.Apply();

            sb.Draw(target, device.Viewport.Bounds, color);
            sb.End();
        }

        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);

        sb.Draw(lease.Target, device.Viewport.Bounds, Color.White);

        sb.End();

        celestialBodyLease.Dispose();
        celestialBodyLease = null;
    }
}
