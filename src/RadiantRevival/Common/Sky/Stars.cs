using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using System;
using System.Diagnostics;
using RadiantRevival.Core;
using Terraria;
using Terraria.Utilities;

namespace RadiantRevival.Common;

// TODO: Config for:
// - star count
// - star texture
public static class Stars
{
    private readonly record struct Star(Vector3 Position, Color Color, float Scale, float Phase);

    private const int star_count = 1600;

    private static readonly Star[] stars = new Star[star_count];

    private const float star_min_scale = 0.15f;
    private const float star_max_scale = 1.1f;

    private static readonly Color star_color_low = new(148, 182, 255);
    private static readonly Color star_color_high = new(255, 204, 152);

    private static WrapperShaderData<Assets.Sky.StarShader.Parameters>? starShaderData;

    [OnLoad]
    private static void Load()
    {
        starShaderData = Assets.Sky.StarShader.CreateStarShader();

        for (int i = 0; i < stars.Length; i++)
        {
            var position = Main.rand.NextPointSphereSurface();

            var color = Color.OklabLerp(star_color_low, star_color_high, Main.rand.NextFloat());

            float scale = Main.rand.NextFloat(star_min_scale, star_max_scale);

            float phase = Main.rand.NextFloat(MathF.Tau);

            stars[i] = new Star(position, color, scale, phase);
        }

        IL_Main.DrawStarsInBackground += DrawStarsInBackground_DrawStars;
    }

    private static void DrawStarsInBackground_DrawStars(ILContext il)
    {
        var c = new ILCursor(il);

        ILLabel jumpRetTarget = c.DefineLabel();

        c.EmitDelegate(
            static () =>
            {
                Draw(Main.spriteBatch, Main.graphics.graphicsDevice);
                
                return true;
            }
        );

        c.EmitBrfalse(jumpRetTarget);

        c.EmitRet();

        c.MarkLabel(jumpRetTarget);
    }

    private static void Draw(SpriteBatch sb, GraphicsDevice device)
    {
        Debug.Assert(starShaderData is not null);

        Main.spriteBatch.End(out var snapshot);

        starShaderData.Apply();

        Main.spriteBatch.Begin(snapshot with { SamplerState = SamplerState.LinearClamp, CustomEffect = starShaderData._effect });

        const float offscreen_margin = 70f;

        const float screen_length_denom = 1101f;

        const float star_scale = 0.36f;

        // Angle upwards to make the curve more pronounced
        const float rotation_x = 0.4f;
        const float rotation_speed = 0.0055f;

        var screenSize = device.Viewport.Bounds.Size();

        var center = new Vector2(screenSize.X * 0.5f, screenSize.Y * 0.5f);

        float screenScale = center.Length();
        screenScale += offscreen_margin * screenScale / screen_length_denom;

        var transform =
            Matrix.CreateRotationY(Main.GlobalTimeWrappedHourly * rotation_speed)
          * Matrix.CreateRotationX(rotation_x)
          * Matrix.CreateScale(screenScale)
          * Matrix.CreateTranslation(new(center, 0));

        var texture = Assets.Sky.Stars.Circle.Asset.Value;

        var origin = texture.Size() * 0.5f;

        float alpha = GetStarAlpha();

        foreach (var star in stars)
        {
            var position = Vector3.Transform(star.Position, transform);

            if (position.Z < 0)
            {
                continue;
            }

            float twinkle = (MathF.Sin(star.Phase + Main.GlobalTimeWrappedHourly * 2.3f) + 1) * 0.5f;

            // Scale up stars near the edge of the screen.
            float edgeScale = (1 - position.Z / screenScale) * 2;

            float fade = 1f - MathF.Pow(position.Y / Main.screenHeight, 2f) + edgeScale;

            float scale = star.Scale * star_scale * fade * twinkle;

            scale = Math.Max(scale, 0.13f) * alpha;

            var color = star.Color * alpha;

            sb.Draw(
                new DrawParameters(texture)
                {
                    Position = new(position.X, position.Y),
                    Scale = new(scale),
                    Color = color,
                    Origin = origin,
                }
            );
        }

        Main.spriteBatch.Restart(in snapshot);
    }

    private static float GetStarAlpha()
    {
        const float dawn_time = 7700f;
        const float dusk_start_time = 41000f;
        const float day_length = 54000f;

        const float graveyard_alpha = 1.4f;
        const float atmo_multiplier = 0.43f;

        float alpha = 1f;

        float time = (float)Main.time;

        if (Main.dayTime)
        {
            alpha = 0f;

            if (time < dawn_time)
            {
                alpha = 1f - time / dawn_time;
            }
            else if (time > dusk_start_time)
            {
                alpha = (time - dusk_start_time) / (day_length - dusk_start_time);
            }
        }

        alpha += Main.shimmerAlpha;

        alpha *= 1f - Main.GraveyardVisualIntensity * graveyard_alpha;

        float atmosphericBoost = MathF.Pow(1f - Main.atmo, 3) * atmo_multiplier;

        alpha = Math.Clamp(MathF.Pow(alpha + atmosphericBoost, 1.7f), 0f, 1f);

        return alpha;
    }

    private static Vector3 NextPointSphereSurface(this UnifiedRandom rand, float radius = 1f)
    {
        float u = rand.NextFloat();
        float v = rand.NextFloat();

        float theta = 2 * MathF.PI * u;
        float phi = MathF.Acos(2 * v - 1);

        float x = MathF.Sin(phi) * MathF.Cos(theta);
        float y = MathF.Sin(phi) * MathF.Sin(theta);
        float z = MathF.Cos(phi);

        return new Vector3(x, y, z) * radius;
    }
}
