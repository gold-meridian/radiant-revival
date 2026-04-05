using System;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using System.Diagnostics;
using Terraria;
using Terraria.GameContent.Drawing;

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
        if (!Main.ForegroundSunlightEffects || Main.screenTarget is null || celestialBodyLease is null)
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

        var screenSize = new Vector2(Main.screenWidth, Main.screenHeight);

        using var lease = ScreenspaceTargetPool.Shared.RentScaled(device, Main.ScreenSize, 0.25f);

        using var _ = sb.Scope();

        sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);

        using (lease.Scope(clearColor: Color.Transparent))
        {
            godraysShaderData.Parameters.light_position = Main.LastCelestialBodyPosition * screenSize;
            godraysShaderData.Parameters.light_size = 4.6f;
            godraysShaderData.Parameters.sample_count = 128;
            godraysShaderData.Parameters.decay_mult = 0.97f;
            godraysShaderData.Parameters.lights = new HlslSampler2D
            {
                Texture = celestialBodyLease.Target,
                Sampler = SamplerState.LinearClamp
            };

            godraysShaderData.Apply();

            HorizonHelper.GetCelestialBodyColors(out var sunColor, out var moonColor);

            sunColor = sunColor.MultiplyRGB(Color.PeachPuff);
            moonColor = Color.Pow(moonColor, 6f) * 100f;

            NextHorizonRenderer.GetVisibilities(out var sunsetVisibility, out var sunriseVisibility, out var celestialVisibility);

            Color color = Main.dayTime ? sunColor : moonColor;

            float num = Math.Max(sunsetVisibility, sunriseVisibility) * celestialVisibility;
            if (!Main.dayTime)
            {
                num = Math.Max(num, celestialVisibility * 0.15f) * 2.2f;
            }

            color *= num;

            sb.Draw(target, Vector2.Zero, color);
            sb.End();
        }

        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise);

        sb.Draw(lease.Target, device.Viewport.Bounds, Color.White);

        celestialBodyLease.Dispose();
    }
}
