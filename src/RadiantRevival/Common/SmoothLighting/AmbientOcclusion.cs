using Daybreak.Common.CIL;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Core;
using System.Diagnostics;
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

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    HorizontalShader = Assets.SmoothLighting.TileBlur.CreateHorizontalShader(),
                    VerticalShader = Assets.SmoothLighting.TileBlur.CreateVerticalShader(),
                    MaskShader = Assets.SmoothLighting.AmbientOcclusionSampler.CreateMaskShader(),
                }
            ).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data) { }
    }

    private static RenderTargetLease? blurTarget;

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

        SpriteBatch sb = Main.spriteBatch;
        GraphicsDevice device = Main.graphics.GraphicsDevice;

        const int sample_count = 16;

        var screenSize = new Vector2(Main.screenWidth, Main.screenHeight);

        var blurSize = new Vector2(16) / screenSize;

        var width = Main.tileTarget.Texture.Width / 2;
        var height = Main.tileTarget.Texture.Height / 2;

        using var blurTargetSwap = RenderTargetPool.Shared.Rent(device, width, height);

        blurTarget?.Dispose();
        blurTarget = RenderTargetPool.Shared.Rent(device, width, height);

        using (blurTargetSwap.Scope(clearColor: Color.Transparent))
        {
            horizShader.Parameters.sample_count = sample_count;
            horizShader.Parameters.blur_size = blurSize;

            horizShader.Apply();

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, horizShader.Shader);

            sb.Draw(Main.tileTarget.Texture, device.Viewport.Bounds, Color.White);

            sb.End();
        }

        using (blurTarget.Scope(clearColor: Color.Transparent))
        {
            vertShader.Parameters.sample_count = sample_count;
            vertShader.Parameters.blur_size = blurSize;

            vertShader.Apply();

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, vertShader.Shader); // vertShader.Shader

            sb.Draw(blurTargetSwap.Target, device.Viewport.Bounds, Color.White);

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
                Debug.Assert(blurTarget is not null);

                var maskShader = Data.Instance.MaskShader;

                SpriteBatch sb = Main.spriteBatch;

                sb.End(out ss);

                Color color = Color.Black * 0.36f;

                maskShader.Parameters.occlusion_color = color.ToVector4();

                maskShader.Parameters.tile_tex = new HlslSampler2D
                {
                    Texture = blurTarget.Target,
                    Sampler = SamplerState.PointClamp
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
