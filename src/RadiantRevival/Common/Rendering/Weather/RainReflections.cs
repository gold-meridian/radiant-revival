using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

public static class RainReflections
{
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.Weather.RainReflections.Parameters> ReflectionsShader { get; init; }

        public required WrapperShaderData<Assets.Weather.RainDistance.Parameters> DistanceShader { get; init; }

        // public required RenderTargetLease ReflectionTarget { get; init; }

        public required RenderTargetLease DistanceMap { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    ReflectionsShader = Assets.Weather.RainReflections.CreateRainReflectionsShader(),
                    DistanceShader = Assets.Weather.RainDistance.CreateRainDistanceShader(),
                    DistanceMap = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, RenderTargetDescriptor.Default with { Format = SurfaceFormat.HalfVector2 }),
                }
            ).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data)
        {
            Main.RunOnMainThread(
                () =>
                {
                    // data.ReflectionTarget.Dispose();
                    data.DistanceMap.Dispose();
                }
            );
        }
    }

    public sealed class ReflectionRenderer : IScreenFilterStep
    {
        public EffectPriority Priority => EffectPriority.Medium;

        public bool Apply(in ScreenFilterRendererContext ctx)
        {
            return ApplyShader(ctx.ScreenTarget, ctx.ScreenTargetSwap, ctx.Color);
        }
    }

    private static RenderTargetLease DistanceMap => Data.Instance.DistanceMap;

    private const int max_reflection_length = 16;

    private static bool ApplyShader(RenderTarget2D screen, RenderTarget2D screenSwap, Color color)
    {
        if (!Rain.Active)
        {
            return false;
        }

        var distanceShader = Data.Instance.DistanceShader;
        var reflectionsShader = Data.Instance.ReflectionsShader;

        var intensity = Main.cloudAlpha * MathF.Pow(Main.atmo, 3);

        var screenPosition = Main.screenPosition;

        var sb = Main.spriteBatch;
        var device = Main.graphics.GraphicsDevice;

        device.SetRenderTarget(screenSwap);
        device.Clear(Color.Transparent);

        // Draw tileTarget to a screen target to make the UVs a little nicer for the distance shader
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        {
            var tilePosition = Main.tileTarget.Position - screenPosition;

            sb.Draw(Main.tileTarget.Texture, tilePosition, Color.White);
        }
        sb.End();

        device.SetRenderTarget(DistanceMap.Target);
        device.Clear(Color.Transparent);

        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        {
            var distance = (-1f * Main.GameZoomTarget) / Main.screenHeight;

            distanceShader.Parameters.SampleCount = max_reflection_length;
            distanceShader.Parameters.SampleDistance = distance;
            distanceShader.Parameters.DrawZoom = Main.GameZoomTarget;

            distanceShader.Apply();

            sb.Draw(screenSwap, Vector2.Zero, Color.White);
        }
        sb.End();

        device.SetRenderTarget(screenSwap);
        device.Clear(Color.Transparent);

        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        {
            reflectionsShader.Parameters.DistanceMap = new HlslSampler2D
            {
                Sampler = SamplerState.PointClamp,
                Texture = DistanceMap.Target,
            };

            reflectionsShader.Parameters.Intensity = intensity;

            reflectionsShader.Apply();

            sb.Draw(screen, Vector2.Zero, color);
        }
        sb.End();

        return true;
    }
}
