using System.Collections.Generic;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using Terraria;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

// TODO: Config

public static class AmbientOcclusion
{
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.SmoothLighting.TileBlur.Parameters> HorizontalShader { get; init; }

        public required WrapperShaderData<Assets.SmoothLighting.TileBlur.Parameters> VerticalShader { get; init; }

        public required WrapperShaderData<Assets.SmoothLighting.AmbientOcclusionSampler.Parameters> MaskShader { get; init; }

        public required RenderTargetLease BlurTarget { get; init; }

        public required RenderTargetLease BlurTargetSwap { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    HorizontalShader = Assets.SmoothLighting.TileBlur.CreateHorizontalShader(),
                    VerticalShader = Assets.SmoothLighting.TileBlur.CreateVerticalShader(),
                    MaskShader = Assets.SmoothLighting.AmbientOcclusionSampler.CreateMaskShader(),
                    BlurTarget = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, GetBlurTargetSize, RenderTargetDescriptor.Default with { Format = SurfaceFormat.Alpha8 }),
                    BlurTargetSwap = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, GetBlurTargetSize, RenderTargetDescriptor.Default with { Format = SurfaceFormat.Alpha8 }),
                }
            ).GetAwaiter().GetResult();

            static (int w, int h) GetBlurTargetSize(int width, int height, int targetWidth, int targetHeight)
            {
                return (targetWidth / 2, targetHeight / 2);
            }
        }

        public static void UnloadData(Data data)
        {
            Main.RunOnMainThread(
                () =>
                {
                    data.BlurTarget.Dispose();
                    data.BlurTargetSwap.Dispose();
                }
            );
        }
    }

    public sealed class WallRenderer : IVanillaPipelineStep
    {
        public List<WorldSceneLayerTarget> Inputs => [Main.wallTarget, Main.tileTarget];

        public List<WorldSceneLayerTarget> Apply(in VanillaTargetRendererContext ctx)
        {
            RenderToWallTarget(ctx.WorldSceneTargetSwap);
            return [Main.wallTarget];
        }
    }

    private static RenderTargetLease BlurTarget => Data.Instance.BlurTarget;

    private static RenderTargetLease BlurTargetSwap => Data.Instance.BlurTargetSwap;

    [OnLoad]
    private static void Load()
    {
        On_Main.RenderTiles += RenderTiles_BlurTarget;
    }

    private static void RenderTiles_BlurTarget(On_Main.orig_RenderTiles orig, Main self)
    {
        orig(self);

        var horizShader = Data.Instance.HorizontalShader;
        var vertShader = Data.Instance.VerticalShader;

        var sb = Main.spriteBatch;
        var device = Main.graphics.GraphicsDevice;

        const int sample_count = 16;

        var screenSize = new Vector2(Main.screenWidth, Main.screenHeight);
        var blurSize = new Vector2(16f) / screenSize;

        using (BlurTargetSwap.Scope(clearColor: Color.Transparent))
        {
            horizShader.Parameters.sample_count = sample_count;
            horizShader.Parameters.blur_size = blurSize;

            horizShader.Apply();

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, horizShader.Shader);
            {
                sb.Draw(Main.tileTarget.Texture, device.Viewport.Bounds, Color.White);
            }
            sb.End();
        }

        using (BlurTarget.Scope(clearColor: Color.Transparent))
        {
            vertShader.Parameters.sample_count = sample_count;
            vertShader.Parameters.blur_size = blurSize;

            vertShader.Apply();

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, vertShader.Shader);
            {
                sb.Draw(BlurTargetSwap.Target, device.Viewport.Bounds, Color.White);
            }
            sb.End();
        }
    }

    private static void RenderToWallTarget(RenderTargetLease wallTargetSwap)
    {
        var sb = Main.spriteBatch;

        using (wallTargetSwap.Scope(clearColor: Color.Transparent))
        {
            var wallPos = Main.wallTarget.Position;
            var tileOffset = new Vector2(
                IsIntegerOdd(wallPos.X) ? -0.5f : 0,
                IsIntegerOdd(wallPos.Y) ? -0.5f : 0
            );

            var color = Color.Black * 0.36f;
            var maskShader = Data.Instance.MaskShader;
            maskShader.Parameters.occlusion_color = color.ToVector4();
            maskShader.Parameters.tex_pixel_offset = tileOffset;
            maskShader.Parameters.tile_tex = new HlslSampler2D
            {
                Texture = BlurTarget.Target,
                Sampler = SamplerState.PointClamp,
            };
            maskShader.Apply();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, maskShader.Shader);
            sb.Draw(Main.wallTarget.Texture, Vector2.Zero, Color.White);
            sb.End();
        }

        using (Main.wallTarget._target.Scope(clearColor: Color.Transparent))
        {
            sb.Begin();
            sb.Draw(wallTargetSwap.Target, Vector2.Zero, Color.White);
            sb.End();
        }

        return;

        static bool IsIntegerOdd(float f)
        {
            return (int)f % 2 == 1;
        }
    }
}
