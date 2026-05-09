using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using Newtonsoft.Json.Linq;
using RadiantRevival.Core;
using System;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.Graphics;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

// TODO: Config, Retro Lighting support (may entail making rl use tileTarget)
public static class Rain
{
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.Weather.RainBlur.Parameters> DirectionalBlurShader { get; init; }

        public required WrapperShaderData<Assets.Weather.RainDistortion.Parameters> DistortionShader { get; init; }

        public required RenderTargetLease MaskTarget { get; init; }

        public required RenderTargetLease MaskTargetSwap { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    DirectionalBlurShader = Assets.Weather.RainBlur.CreateRainBlurShader(),
                    DistortionShader = Assets.Weather.RainDistortion.CreateRainDistortionShader(),
                    MaskTarget = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, GetMaskTargetSize, RenderTargetDescriptor.Default with { Format = SurfaceFormat.Alpha8 }),
                    MaskTargetSwap = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, GetMaskTargetSize, RenderTargetDescriptor.Default with { Format = SurfaceFormat.Alpha8 }),
                }
            ).GetAwaiter().GetResult();

            static (int w, int h) GetMaskTargetSize(int width, int height, int targetWidth, int targetHeight)
            {
                return (targetWidth / 2, targetHeight / 2);
            }
        }

        public static void UnloadData(Data data)
        {
            Main.RunOnMainThread(
                () =>
                {
                    data.MaskTarget.Dispose();
                    data.MaskTargetSwap.Dispose();
                }
            );
        }
    }

    private static RenderTargetLease MaskTarget => Data.Instance.MaskTarget;

    private static RenderTargetLease MaskTargetSwap => Data.Instance.MaskTargetSwap;

    private static bool Active =>
        Main.cloudAlpha > 0f
     && !Main.dedServ
     && !Main.gameMenu // TODO: Allow on main menu when config is introduced
     && Main.SceneMetrics.ZoneRain
     && Main.shimmerAlpha == 0f;

    [OnLoad]
    private static void Load()
    {
        On_Rain.MakeRain += MakeRain_Disable;
        On_Main.DrawRain += DrawRain_Disable;

        On_Main.RenderTiles += RenderTiles_MaskTarget;
        IL_FilterManager.EndCapture_RenderTarget2D_RenderTarget2D_RenderTarget2D_Vector2_Vector2_Vector2 += EndCapture_RainShader;
        On_FilterManager.CanCapture += CanCapture_AllowRain;
        IL_Main.DoDraw += _ => { };
    }

    private static void MakeRain_Disable(On_Rain.orig_MakeRain orig)
    {
        if (Main.drawToScreen)
        {
            orig();
        }
    }

    private static void DrawRain_Disable(On_Main.orig_DrawRain orig, Main self)
    {
        if (Main.drawToScreen)
        {
            orig(self);
        }
    }

    private static void RenderTiles_MaskTarget(On_Main.orig_RenderTiles orig, Main self)
    {
        orig(self);

        if (!Active)
        {
            return;
        }

        var blurShader = Data.Instance.DirectionalBlurShader;

        var sb = Main.spriteBatch;
        var device = Main.graphics.GraphicsDevice;

        var direction = Vector2.Normalize(Terraria.Rain.GetRainFallVelocity());

        direction *= 8;

        using (MaskTargetSwap.Scope(clearColor: Color.Transparent))
        {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            {
                var tilePos = Main.tileTarget.Position;
                var tileOffset = new Vector2(
                    IsIntegerOdd(tilePos.X) ? -0.5f : 0,
                    IsIntegerOdd(tilePos.Y) ? -0.5f : 0
                );

                var stepCount = Math.Min(Main.tileTarget.Texture.Width / (direction.X + float.Epsilon), Main.tileTarget.Texture.Height / direction.Y);
                stepCount = Math.Abs(stepCount);

                for (var i = 0; i <= stepCount; i++)
                {
                    var position = tileOffset + (direction * i);

                    sb.Draw(Main.tileTarget.Texture, position, null, Color.White, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
                }
            }
            sb.End();
        }

        using (MaskTarget.Scope(clearColor: Color.Transparent))
        {
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            {
                blurShader.Parameters.BlurSize = -direction;
                blurShader.Parameters.SampleCount = 8;

                blurShader.Apply();

                sb.Draw(MaskTargetSwap.Target, Vector2.Zero, Color.White);
            }
            sb.End();
        }

        return;

        static bool IsIntegerOdd(float f)
        {
            return (int)f % 2 == 1;
        }
    }

    private static void EndCapture_RainShader(ILContext il)
    {
        var c = new ILCursor(il);

        var tIndex = -1;  // loc
        var t2Index = -1; // loc

        c.GotoNext(
            MoveType.After,
            i => i.MatchLdloca(out t2Index),
            i => i.MatchLdloca(out tIndex),
            i => i.MatchCall(typeof(Utils), nameof(Utils.Swap))
        );

        c.EmitLdloca(tIndex);
        c.EmitLdloca(t2Index);

        c.EmitDelegate(
            static (ref RenderTarget2D target, ref RenderTarget2D target2) =>
            {
                // TODO: Should rlf use tileTarget ?
                if (!Active || Main.drawToScreen)
                {
                    return;
                }

                var distortionShader = Data.Instance.DistortionShader;

                var sb = Main.spriteBatch;
                var device = Main.graphics.GraphicsDevice;

                device.SetRenderTarget(target2);
                device.Clear(Color.Transparent);

                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                {
                    var tilePos = Main.tileTarget.Position;
                    var tileOffset = new Vector2(
                        IsIntegerOdd(tilePos.X) ? -0.5f : 0,
                        IsIntegerOdd(tilePos.Y) ? -0.5f : 0
                    );

                    var color = Lighting.UpdateEveryFrame ? Color.White : Main.ColorOfTheSkies;

                    distortionShader.Parameters.Time = (float)Main.timeForVisualEffects;

                    distortionShader.Parameters.DrawOffset = Main.tileTarget.Position - Main.screenPosition;
                    distortionShader.Parameters.TilePixelOffset = tileOffset;

                    distortionShader.Parameters.DrawZoom = 1f / Main.GameZoomTarget;

                    distortionShader.Parameters.MaskTexture = new HlslSampler2D
                    {
                        Texture = MaskTarget.Target,
                        Sampler = SamplerState.PointClamp,
                    };

                    distortionShader.Apply();

                    sb.Draw(target, Vector2.Zero, color);
                }
                sb.End();

                Utils.Swap(ref target2, ref target);
            }
        );

        return;

        static bool IsIntegerOdd(float f)
        {
            return (int)f % 2 == 1;
        }
    }

    private static bool CanCapture_AllowRain(On_FilterManager.orig_CanCapture orig, FilterManager self)
    {
        return Active || orig(self);
    }
}
