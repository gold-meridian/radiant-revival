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
using Terraria.GameContent;
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

    public sealed class RainRenderer : IScreenFilterStep
    {
        public EffectPriority Priority => EffectPriority.Medium;

        public bool Apply(in ScreenFilterRendererContext ctx)
        {
            return ApplyShader(ctx.ScreenTarget, ctx.ScreenTargetSwap, ctx.Color);
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

                var waterPos = Main.waterTarget.Position;
                var waterOffset = new Vector2(
                    IsIntegerOdd(waterPos.X) ? -0.5f : 0,
                    IsIntegerOdd(waterPos.Y) ? -0.5f : 0
                );
                waterOffset += (waterPos - tilePos) * 0.5f;

                var stepCount = Math.Min(Main.tileTarget.Texture.Width / (direction.X + float.Epsilon), Main.tileTarget.Texture.Height / direction.Y);
                stepCount = Math.Abs(stepCount);

                for (var i = 0; i <= stepCount; i++)
                {
                    var tilePosition = tileOffset + (direction * i);
                    var waterPosition = waterOffset + (direction * i);

                    sb.Draw(Main.tileTarget.Texture, tilePosition, null, Color.White, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
                    sb.Draw(Main.waterTarget.Texture, waterPosition, null, Color.White, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
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

    private static bool ApplyShader(RenderTarget2D screen, RenderTarget2D screenSwap, Color color)
    {
        if (!Active || Main.drawToScreen)
        {
            return false;
        }

        var distortionShader = Data.Instance.DistortionShader;

        var sb = Main.spriteBatch;
        var device = Main.graphics.GraphicsDevice;

        device.SetRenderTarget(screenSwap);
        device.Clear(Color.Transparent);

        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        {
            var tilePos = Main.tileTarget.Position;
            var tileOffset = new Vector2(
                IsIntegerOdd(tilePos.X) ? -0.5f : 0,
                IsIntegerOdd(tilePos.Y) ? -0.5f : 0
            );
            var screenPosition = Main.screenPosition;
            var direction = Vector2.Normalize(Terraria.Rain.GetRainFallVelocity());

            distortionShader.Parameters.Time = (float)Main.timeForVisualEffects;
            distortionShader.Parameters.Direction = direction;
            distortionShader.Parameters.Intensity = Main.cloudAlpha * MathF.Pow(Main.atmo, 3);

            distortionShader.Parameters.TilePixelOffset = tileOffset;
            distortionShader.Parameters.DrawZoom = 1f / Main.GameZoomTarget;

            distortionShader.Parameters.MaskOffset = Main.tileTarget.Position - screenPosition;
            distortionShader.Parameters.MaskTexture = new HlslSampler2D
            {
                Sampler = SamplerState.PointClamp,
                Texture = MaskTarget.Target,
            };

            distortionShader.Parameters.LightOffset = new Vector2(screenPosition.X % 16, screenPosition.Y % 16);
            distortionShader.Parameters.OffscreenTiles = LightingEngine.BufferOffscreenTileRange;
            distortionShader.Parameters.GlobalBrightness = Lighting.GlobalBrightness;
            distortionShader.Parameters.LightMap = new HlslSampler2D
            {
                Sampler = SamplerState.LinearClamp,
                Texture = LightingEngine.TileSpaceBuffer.Target,
            };

            distortionShader.Parameters.Noise = new HlslSampler2D
            {
                Sampler = SamplerState.LinearWrap,
                Texture = Assets.Weather.RainNoise.Asset.Value,
            };

            var rainTexture = TextureAssets.Rain.Value;
            var rainPosition = new Vector2(Main.waterStyle * 4 * 3 + 0.5f, 0f);

            if (Main.waterStyle >= 15)
            {
                rainTexture = LoaderManager.Get<WaterStylesLoader>().Get(Main.waterStyle).GetRainTexture().Value;
                rainPosition = new Vector2(0.5f, 0);
            }

            distortionShader.Parameters.RainPosition = rainPosition; 
            distortionShader.Parameters.RainTexture = new HlslSampler2D
            {
                Sampler = SamplerState.PointClamp,
                Texture = rainTexture,
            };

            distortionShader.Apply();

            sb.Draw(screen, Vector2.Zero, color);
        }
        sb.End();

        return true;

        static bool IsIntegerOdd(float f)
        {
            return (int)f % 2 == 1;
        }
    }
}
