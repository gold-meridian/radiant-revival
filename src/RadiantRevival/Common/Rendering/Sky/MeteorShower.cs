using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using System;
using Daybreak.Common.Mathematics;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace RadiantRevival.Common;

public static class MeteorShower
{
    private record struct Meteor(Vector3 Position, Vector3 Velocity, Color Color, int Lifetime, bool Active);

    private const int meteor_count = 200;

    private const int max_lifetime = 500;

    private const int spawn_chance = 7;

    private static readonly Color meteor_color_low = new(244, 178, 255);
    private static readonly Color meteor_color_high = new(255, 228, 178);

    private static readonly Meteor[] meteors = new Meteor[meteor_count];

    private static Vector3 showerPosition = Vector3.Normalize(new Vector3(0.6f, -0.4f, 0.7f));

    [OnLoad]
    private static void Load()
    {
        On_Main.DrawSunAndMoon += DrawSunAndMoon_DrawMeteors;

        On_Main.DoUpdate += DoUpdate_UpdateMeteors;
    }

    private static void DrawSunAndMoon_DrawMeteors(On_Main.orig_DrawSunAndMoon orig, Main self, Main.SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)
    {
        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);

        showerPosition = Vector3.Normalize(new Vector3(0.6f, -0.4f, 1.7f));

        var sb = Main.spriteBatch;

        sb.End(out var ss);

        const float offscreen_margin = 70f;

        const float screen_length_denom = 1101f;

        const float meteor_scale = 0.04f;

        var screenSize = Main.graphics.GraphicsDevice.Viewport.Bounds.Size();

        var center = screenSize * 0.5f;

        var screenScale = center.Length();
        screenScale += offscreen_margin * screenScale / screen_length_denom;

        var transform = Matrix.CreateScale(screenScale, screenScale, 0f)
          * Matrix.CreateTranslation(new Vector3(center, 0));

        var texture = Assets.Sky.Meteor.Asset.Value;

        var origin = new Vector2(texture.Width, texture.Height * 0.5f);

        var startPosition = Vector3.Transform(showerPosition, transform);

        sb.Begin(ss with { SamplerState = SamplerState.AnisotropicClamp, CustomEffect = null });

        foreach (var meteor in meteors)
        {
            if (!meteor.Active)
            {
                continue;
            }

            var position = Vector3.Transform(meteor.Position, transform);

            var ratio = (float)meteor.Lifetime / max_lifetime;

            var sin = MathF.Sin(ratio * MathF.PI);

            var scale = sin;

            scale *= scale;

            scale *= Math.Max(1 - meteor.Position.Z, 0.7f);
            scale *= meteor_scale;

            var stretch = ((position - startPosition).Length() / (texture.Width * scale)) * sin;

            stretch *= 0.7f;

            var color = meteor.Color;
            color.A = 0;

            var rotation = new Vector2(meteor.Velocity.X, meteor.Velocity.Y).ToRotation();

            sb.Draw(
                new DrawParameters(texture)
                {
                    Position = new Vector2(position.X, position.Y),
                    Scale = new Vector2(scale * stretch, scale),
                    Color = color,
                    Rotation = Angle.FromRadians(rotation),
                    Origin = origin,
                }
            );
        }

        sb.Restart(in ss);
    }

    private static void DoUpdate_UpdateMeteors(On_Main.orig_DoUpdate orig, Main self, ref GameTime gameTime)
    {
        orig(self, ref gameTime);

        for (var i = 0; i < meteors.Length; i++)
        {
            ref var meteor = ref meteors[i];

            if (!meteor.Active)
            {
                continue;
            }

            meteor.Position += meteor.Velocity;

            if (meteor.Lifetime++ > max_lifetime)
            {
                meteor.Active = false;
            }
        }

        if (WorldGen.meteorShowerCount <= 0)
        {
            // return;
        }

        if (!Main.rand.NextBool(spawn_chance))
        {
            return;
        }

        var index = Array.FindIndex(meteors, m => !m.Active);

        if (index != -1)
        {
            SpawnMeteor(index);
        }

        return;

        static void SpawnMeteor(int index)
        {
            const float angle_radius = MathHelper.PiOver2;

            const float speed = 3f;

            var velocity = -showerPosition;

            var circle = Main.rand.NextVector2Circular(angle_radius, angle_radius);

            var tangent = FindPerpendicular(velocity);
            var biTangent = Vector3.Cross(tangent, velocity);

            velocity += circle.X * tangent;
            velocity += circle.Y * biTangent;

            velocity /= max_lifetime;

            velocity *= speed;

            var color = Color.OklabLerp(meteor_color_low, meteor_color_high, Main.rand.NextFloat());

            meteors[index] = new Meteor(showerPosition, velocity, color, 0, true);
        }
    }

    private static Vector3 FindPerpendicular(Vector3 vector)
    {
        var axis = Math.Abs(vector.Y) < Math.Abs(vector.X)
          ? Vector3.UnitY
          : Vector3.UnitX;

        var perpendicular = Vector3.Cross(vector, axis);

        if (perpendicular.LengthSquared() < float.Epsilon)
        {
            perpendicular = Vector3.Cross(vector, Vector3.UnitZ);
        }

        perpendicular.Normalize();

        return perpendicular;
    }
}
