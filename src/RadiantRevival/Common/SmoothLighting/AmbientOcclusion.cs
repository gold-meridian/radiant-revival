using Daybreak.Common.CIL;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Core;
using Terraria;
using Terraria.ModLoader;

namespace RadiantRevival.Common.SmoothLighting;

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
                    BlurTarget = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, GetBlurTargetSize),
                    BlurTargetSwap = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, GetBlurTargetSize),
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

    private static RenderTargetLease BlurTarget => Data.Instance.BlurTarget;

    private static RenderTargetLease BlurTargetSwap => Data.Instance.BlurTargetSwap;

    [OnLoad]
    private static void Load()
    {
        On_Main.RenderTiles += RenderTiles_BlurTarget;
        IL_Main.DoDraw_WallsAndBlacks += WallsAndBlacks_Occlusion;
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

    private static void WallsAndBlacks_Occlusion(ILContext il)
    {
        var c = new ILCursor(il);

        var snapshotDef = il.AddVariable<SpriteBatchSnapshot>();

        c.GotoNext(
            MoveType.After,
            i => i.MatchCallvirt<SpriteBatch>(nameof(SpriteBatch.Begin)),
            i => i.MatchBr(out _)
        );
        c.MoveAfterLabels();

        c.EmitLdloca(snapshotDef);
        c.EmitDelegate(
            static (ref SpriteBatchSnapshot ss) =>
            {
                var sb = Main.spriteBatch;
                sb.End(out ss);

                var color = Color.Black * 0.36f;
                var maskShader = Data.Instance.MaskShader;
                maskShader.Parameters.occlusion_color = color.ToVector4();
                maskShader.Parameters.tile_tex = new HlslSampler2D
                {
                    Texture = BlurTarget.Target,
                    Sampler = SamplerState.PointClamp,
                };
                maskShader.Apply();

                sb.Begin(ss with { SortMode = SpriteSortMode.Immediate, CustomEffect = maskShader.Shader });
            }
        );

        c.GotoNext(
            MoveType.After,
            i => i.MatchCallvirt<SpriteBatch>(nameof(SpriteBatch.Draw))
        );

        c.EmitLdloca(snapshotDef);
        c.EmitDelegate(
            static (ref SpriteBatchSnapshot ss) =>
            {
                Main.spriteBatch.Restart(in ss);
            }
        );
    }
}
