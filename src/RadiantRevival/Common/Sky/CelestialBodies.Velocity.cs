using Daybreak.Common.Features.Hooks;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace RadiantRevival.Common;

public static class CelestialBodyVelocity
{
    private static readonly Vector2 velocity_multiplier = new(0.92f, 0.76f);
    private const float mod_multiplier = 0.976f;

    private static Vector2 celestialBodyVelocity;
    private static float previousPositionY;

    [OnLoad]
    private static void Load()
    {
        IL_Main.UpdateMenu += UpdateMenu_ReverseTime;

        On_Main.DrawSunAndMoon += DrawSunAndMoon_Velocity;
    }

    private static void UpdateMenu_ReverseTime(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(
            MoveType.After,
            i => i.MatchBrfalse(out _),
            i => i.MatchRet()
        );

        c.MoveAfterLabels();

        c.EmitDelegate(() =>
        {
            if (Main.time >= 0)
            {
                return;
            }

            double timeRatio = Main.dayTime
              ? Main.nightLength / Main.dayLength
              : Main.dayLength / Main.nightLength;

            if (Main.dayTime)
            {
                Main.moonPhase = (Main.moonPhase - 1) % 8;
            }

            Main.time = (Main.dayTime ? Main.nightLength : Main.dayLength) + Main.time * timeRatio;
            Main.dayTime = !Main.dayTime;

            if (!Main.lockMenuBGChange)
            {
                Main.moonType = Main.rand.Next(TextureAssets.Moon.Length);
            }
        });

        c.GotoNext(
            MoveType.Before,
            i => i.MatchLdsfld<Main>(nameof(Main.moonPhase)),
            i => i.MatchLdcI4(1)
        );

        c.MoveAfterLabels();

        c.EmitDelegate(
            static () =>
            {
                if (!Main.lockMenuBGChange)
                {
                    Main.moonType = Main.rand.Next(TextureAssets.Moon.Length);
                }
            }
        );
    }

    private static void DrawSunAndMoon_Velocity(On_Main.orig_DrawSunAndMoon orig, Main self, Main.SceneArea sceneArea, Color moonColor, Color sunColor, float tempMushroomInfluence)
    {
        Vector2 screenSize = new Vector2(Main.screenWidth, Main.screenHeight);

        Vector2 oldPosition = Main.LastCelestialBodyPosition * screenSize;

        orig(self, sceneArea, moonColor, sunColor,  tempMushroomInfluence);

        Vector2 position = Main.LastCelestialBodyPosition * screenSize;

        // TODO: Allow celestial body movement when connecting to a server
        if (!Main.gameMenu ||
            Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        float sunMoonWidth =
            Main.dayTime ?
                TextureAssets.Sun.Value.Width :
                TextureAssets.Moon[Main.moonType].Value.Width;

        double timeLength =
            Main.dayTime ?
                Main.dayLength :
                Main.nightLength;

        if (Main.alreadyGrabbingSunOrMoon)
        {
            celestialBodyVelocity = position - oldPosition;
            return;
        }

        /*
        celestialBodyVelocity *= velocity_multiplier;

        if (Main.dayTime)
        {
            Main.sunModY += (short)celestialBodyVelocity.Y;
        }
        else
        {
            Main.moonModY += (short)celestialBodyVelocity.Y;
        }

        Main.sunModY = (short)(Main.sunModY * mod_multiplier);
        Main.moonModY = (short)(Main.moonModY * mod_multiplier);
        */

        ref short modY = ref (Main.dayTime ? ref Main.sunModY : ref Main.moonModY);

        var positionY = (float)modY;
        var displacement = (float)-positionY;

        const float spring_strength = 0.07f;
        const float min_dampening = 0.94f;
        const float max_dampening = 0.78f;
        var dist = Math.Abs(positionY);
        var t = MathHelper.Clamp(dist / 200f, 0f, 1f);
        var dampening = MathHelper.Lerp(min_dampening, max_dampening, MathF.Pow(t, 2));

        celestialBodyVelocity.Y += displacement * spring_strength;
        celestialBodyVelocity.Y *= dampening;
        celestialBodyVelocity.Y += 0.03f;
        positionY += celestialBodyVelocity.Y;

        previousPositionY = positionY;
        modY = (short)positionY;

        /*
        if (Math.Abs(celestialBodyVelocity.Y) < 0.01f && Math.Abs(positionY) < 0.5f)
        {
            celestialBodyVelocity.Y = 0f;
            modY = 0;
        }
        */

        const float x_dampening = 0.045f;
        celestialBodyVelocity.X = MathHelper.Lerp(celestialBodyVelocity.X, 0f, x_dampening);

        double newTime = position.X + celestialBodyVelocity.X + sunMoonWidth;
        newTime /= Main.screenWidth + sunMoonWidth * 2f;
        newTime *= timeLength;
        Main.time = newTime;
    }
}
