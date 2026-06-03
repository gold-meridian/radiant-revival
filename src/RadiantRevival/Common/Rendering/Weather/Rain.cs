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
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using Daybreak.Common.Mathematics;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.GameContent.Skies.SolarSky;

namespace RadiantRevival.Common;

// TODO: Config, Screen Flipping
public static class Rain
{
    private record struct Droplet(Vector2 Position, Vector2 EndPosition, Vector2 Velocity, Color Color, float Scale, float Lifetime, bool Active);
    private record struct Splash(Vector2 Position, Vector2 Velocity, Color Color, float Lifetime, bool Active);

    private const float lifetime_increment = 0.03f;

    private const int droplet_count = 500;
    private static readonly Droplet[] droplets = new Droplet[droplet_count];

    private const int splash_count = 100;
    private static readonly Splash[] splashes = new Splash[splash_count];

    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.Weather.RainDistortion.Parameters> DistortionShader { get; init; }

        public required RenderTargetLease RainTarget { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    DistortionShader = Assets.Weather.RainDistortion.CreateRainDistortionShader(),
                    RainTarget = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice),
                }
            ).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data)
        {
            Main.RunOnMainThread(
                () =>
                {
                    data.RainTarget.Dispose();
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

    private static RenderTargetLease RainTarget => Data.Instance.RainTarget;

    private static bool Active =>
        Main.cloudAlpha > 0f
     && !Main.dedServ
     && !Main.gameMenu // TODO: Allow on main menu when config is introduced
     && Main.SceneMetrics.ZoneRain
     && Main.shimmerAlpha == 0f;

    [OnLoad]
    private static void Load()
    {
        On_Rain.MakeRain += MakeRain_Spawn;
        On_Main.DrawRain += DrawRain_Update;
    }

    private static void MakeRain_Spawn(On_Rain.orig_MakeRain orig)
    {
        if (!Active || !FocusHelper.AllowRain)
        {
            return;
        }

        var intensity = Main.cloudAlpha * MathF.Pow(Main.atmo, 3);

        var chance = (int)MathHelper.Lerp(5, -2, intensity);

        if (chance <= 1)
        {
            var count = (int)MathHelper.Lerp(0, 4, intensity);

            for (var i = 0; i <= count; i++)
            {
                SpawnDroplet();
            }

            return;
        }

        if (Main.rand.NextBool(chance))
        {
            SpawnDroplet();
        }
    }

    private static void DrawRain_Update(On_Main.orig_DrawRain orig, Main self)
    {
        if (!FocusHelper.AllowRain)
        {
            return;
        }

        for (var i = 0; i < droplets.Length; i++)
        {
            ref var rain = ref droplets[i];

            if (!rain.Active)
            {
                continue;
            }

            rain.Position += rain.Velocity;
            rain.Lifetime += lifetime_increment;

            if (rain.Lifetime <= 1f)
            {
                continue;
            }

            rain.Active = false;

            if (Main.rand.NextBool(4))
            {
                SpawnSplash(rain.EndPosition, rain.Velocity * 0.4f, rain.Color);
            }
        }

        for (var i = 0; i < splashes.Length; i++)
        {
            ref var splash = ref splashes[i];

            if (!splash.Active)
            {
                continue;
            }

            const float gravity = 0.2f;

            splash.Position += splash.Velocity;
            splash.Velocity += new Vector2(0, gravity);

            splash.Lifetime += lifetime_increment;

            if (splash.Lifetime > 1f || Collision.SolidCollision(splash.Position, 2, 2))
            {
                splash.Active = false;
            }
        }
    }

    private static void SpawnDroplet()
    {
        var index = Array.FindIndex(droplets, m => !m.Active);

        if (index == -1)
        {
            return;
        }

        const float min_velocity = 40f;
        const float max_velocity = 110f;

        const float min_scale = 0.1f;
        const float max_scale = 0.6f;

        const float top = 300f;

        var intensity = Main.cloudAlpha * MathF.Pow(Main.atmo, 3);

        var direction = Vector2.Normalize(Terraria.Rain.GetRainFallVelocity()).RotatedByRandom(0.1f);

        var height = (Main.screenHeight + top);

        var offset = height * direction.X * 2f;

        offset += Main.LocalPlayer.velocity.X;

        var position = Main.screenPosition;
        position.X += Main.screenWidth * 0.5f;
        position.X -= offset * 0.5f;

        var range = Main.screenWidth + Math.Abs(offset);
        range *= 0.5f;

        position.X += Main.rand.NextFloat(-range, range);
        position.Y -= top;

        if (Collision.SolidCollision(position, 2, 2))
        {
            return;
        }

        var samples = new float[3];
        Collision.LaserScan(position, direction, 0, height * 2f, samples);

        var length = samples[1];

        var velocity = direction * MathHelper.Lerp(min_velocity, max_velocity, intensity);

        var endPosition = position + direction * length;

        var scale = Main.rand.NextFloat(min_scale, max_scale) * MathF.Max(intensity, 0.3f);

        var lifetime = 1 - ((length / velocity.Length()) * lifetime_increment);

        droplets[index] = new Droplet(position, endPosition, velocity, Color.White, scale, lifetime, true);
    }

    private static void SpawnSplash(Vector2 position, Vector2 velocity, Color color)
    {
        var index = Array.FindIndex(splashes, m => !m.Active);

        if (index == -1)
        {
            return;
        }

        velocity.Y = -velocity.Y;

        splashes[index] = new Splash(position + velocity, velocity, color, 0f, true);
    }

    private static bool ApplyShader(RenderTarget2D screen, RenderTarget2D screenSwap, Color color)
    {
        if (!droplets.Any(d => d.Active) && !splashes.Any(s => s.Active))
        {
            return false;
        }

        var distortionShader = Data.Instance.DistortionShader;

        var sb = Main.spriteBatch;
        var device = Main.graphics.GraphicsDevice;

        device.SetRenderTarget(RainTarget.Target);
        device.Clear(Color.Transparent);

        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        {
            var dropletTexture = Assets.Weather.Rain.Asset.Value;
            var dropletOrigin = new Vector2(11, 138);

            var intensity = Main.cloudAlpha * MathF.Pow(Main.atmo, 3);

            foreach (var droplet in droplets)
            {
                if (!droplet.Active)
                {
                    continue;
                }

                var rotation = Angle.FromVector(droplet.Velocity) - Angle.HalfPi;

                var scale = new Vector2(droplet.Scale);
                scale.Y *= 1 + (6.5f * intensity);

                sb.Draw(
                    new DrawParameters(dropletTexture)
                    {
                        Position = droplet.Position - Main.screenPosition,
                        Color = droplet.Color,
                        Rotation = rotation,
                        Scale = scale,
                        Origin = dropletOrigin,
                    }
                );
            }
        }
        sb.End();

        device.SetRenderTarget(screenSwap);
        device.Clear(Color.Transparent);

        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        {
            /*
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
                Texture = RainTarget.Target,
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
            */

            sb.Draw(screen, Vector2.Zero, Color.White); // color
            sb.Draw(RainTarget.Target, Vector2.Zero, Color.White);
        }
        sb.End();

        return true;
    }
}
