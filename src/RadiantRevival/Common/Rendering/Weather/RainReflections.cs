using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

public static class RainReflections
{
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.Weather.Rain.Reflection.Parameters> ReflectionsShader { get; init; }

        public required WrapperShaderData<Assets.Weather.Rain.DistanceMap.Parameters> DistanceProcessorShader { get; init; }
        public required WrapperShaderData<Assets.Weather.Rain.DistanceMap.Parameters> DistanceMapShader { get; init; }

        // public required RenderTargetLease ReflectionTarget { get; init; }

        public required RenderTargetLease DistanceMap { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    ReflectionsShader = Assets.Weather.Rain.Reflection.CreateReflectionShader(),
                    DistanceProcessorShader = Assets.Weather.Rain.DistanceMap.CreateDistanceMapProcessingShader(),
                    DistanceMapShader = Assets.Weather.Rain.DistanceMap.CreateDistanceMapShader(),
                    DistanceMap = ScreenspaceTargetProvider.Shared.Create(Graphics.Device, RenderTargetDescriptor.Default with { Format = SurfaceFormat.HalfVector2 }),
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

    private static RenderTargetLease DistanceMap => Data.Instance.DistanceMap;

    private const int max_reflection_length = 16;

    private static Vector2 priorScreenPosition;

    [ScreenFilter(EffectPriority.Low)]
    private static bool ApplyShader(
        SpriteBatch sb,
        GraphicsDevice device,
        RenderTarget2D screen,
        RenderTarget2D screenSwap,
        Color color
    )
    {
        if (!Rain.Active)
        {
            return false;
        }

        var processorShader = Data.Instance.DistanceProcessorShader;
        var distanceShader = Data.Instance.DistanceMapShader;
        var reflectionsShader = Data.Instance.ReflectionsShader;

        var intensity = Rain.Intensity;

        var screenPosition = Main.screenPosition;

        var deltaTime = (float)Main.gameTimeCache.ElapsedGameTime.TotalSeconds * 60f;

        var fadeSpeed = 0.04f * deltaTime;

        device.SetRenderTarget(screenSwap);
        device.Clear(Color.Transparent);

        // Draw tileTarget to a screen target to make the UVs a little nicer for the distance shader
        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
        {
            var tilePosition = Main.tileTarget.Position - screenPosition;

            var direction = Vector2.Normalize(Terraria.Rain.GetRainFallVelocity());

            direction *= max_reflection_length / direction.Y;

            processorShader.Parameters.FadeSpeed = fadeSpeed;

            processorShader.Parameters.RainMaskOffset = direction;
            processorShader.Parameters.RainMask = new HlslSampler2D
            {
                Sampler = SamplerState.PointClamp,
                Texture = Rain.MaskTarget.Target,
            };

            processorShader.Parameters.ScreenPositionDifference = priorScreenPosition - Main.screenPosition;
            processorShader.Parameters.DistanceMap = new HlslSampler2D
            {
                Sampler = SamplerState.PointClamp,
                Texture = DistanceMap.Target,
            };

            processorShader.Apply();

            sb.Draw(Main.tileTarget.Texture, tilePosition, Color.White);
        }
        sb.End();

        device.SetRenderTarget(DistanceMap.Target);
        device.Clear(Color.Transparent);

        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        {
            var distance = -1f / Main.screenHeight;

            distanceShader.Parameters.SampleCount = max_reflection_length;
            distanceShader.Parameters.SampleDistance = distance;
            distanceShader.Parameters.ScreenPositionDifference = priorScreenPosition - Main.screenPosition;
            distanceShader.Parameters.FadeSpeed = fadeSpeed;

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

            reflectionsShader.Parameters.DrawZoom = 1f / Main.GameZoomTarget;
            reflectionsShader.Parameters.Intensity = intensity;

            reflectionsShader.Apply();

            sb.Draw(screen, Vector2.Zero, color);
        }
        sb.End();

        priorScreenPosition = Main.screenPosition;

        return true;
    }
}
