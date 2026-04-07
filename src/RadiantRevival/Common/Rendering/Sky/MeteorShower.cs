using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace RadiantRevival.Common;

public static class MeteorShower
{
    private record struct Meteor(Vector3 Position, Vector3 Velocity, int Lifetime, bool Active);

    private const int meteor_count = 200;

    private const int max_lifetime = 300;

    private const int spawn_chance = 7;

    private static readonly Meteor[] meteors = new Meteor[meteor_count];

    private static Vector3 showerPosition = Vector3.Normalize(new Vector3(1, -.4f, 0.7f));

    [OnLoad]
    private static void Load()
    {
        On_Main.DrawSunAndMoon += DrawSunAndMoon_DrawMeteors;

        On_Main.DoUpdate += DoUpdate_UpdateMeteors;
    }

    private static void DrawSunAndMoon_DrawMeteors(On_Main.orig_DrawSunAndMoon orig, Main self, Main.SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)
    {
        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);

        var sb = Main.spriteBatch;

        const float offscreen_margin = 70f;

        const float screen_length_denom = 1101f;

        var screenSize = Main.graphics.GraphicsDevice.Viewport.Bounds.Size();

        var center = screenSize * 0.5f;

        var screenScale = center.Length();
        screenScale += offscreen_margin * screenScale / screen_length_denom;

        var transform = Matrix.CreateScale(screenScale)
          * Matrix.CreateTranslation(new Vector3(center, 0));

        var texture = Assets.Sky.Stars.Circle.Asset.Value;

        var origin = texture.Size() * 0.5f;

        foreach (var meteor in meteors)
        {
            if (!meteor.Active)
            {
                continue;
            }

            var position = Vector3.Transform(meteor.Position, transform);

            var ratio = (float)meteor.Lifetime / max_lifetime;

            var scale = MathF.Sin(ratio * MathF.PI);

            scale *= scale;

            scale *= (1 - meteor.Position.Z);
            scale *= 0.3f;

            sb.Draw(
                new DrawParameters(texture)
                {
                    Position = new Vector2(position.X, position.Y),
                    Scale = new Vector2(scale),
                    Color = Color.White,
                    Origin = origin,
                }
            );
        }
    }

    private static void DoUpdate_UpdateMeteors(On_Main.orig_DoUpdate orig, Main self, ref GameTime gameTime)
    {
        orig(self, ref gameTime);
        showerPosition = Vector3.Normalize(new Vector3(0, -.3f, 1f));
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
            const float angle_radius = 1.2f;

            const float speed = 2.4f;

            var velocity = -showerPosition;

            var circle = Main.rand.NextVector2Circular(angle_radius, angle_radius);

            var tangent = FindPerpendicular(velocity);
            var biTangent = Vector3.Cross(tangent, velocity);

            velocity += circle.X * tangent;
            velocity += circle.Y * biTangent;

            velocity /= max_lifetime;

            velocity *= speed;

            meteors[index] = new Meteor(showerPosition, velocity, 0, true);
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
