using Daybreak.Common.Features.Hooks;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using System;
using System.Diagnostics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;

namespace RadiantRevival.Common;

public static class CelestialBodyVelocity
{
    private static Vector2 celestialBodyVelocity;

    private static bool CanGrabCelestialBody
    {
        get
        {
            return Main.instance.focusMenu == -1
                && Main.MenuUI._lastElementHover is null or UIState
                   // Niche slider behavior
                && IngameOptions.rightHover == -1
                && IngameOptions.rightLock == -1
                && !IngameOptions.inBar
                && RangeElement.rightHover is null
                && RangeElement.rightLock is null;
        }
    }

    [OnLoad]
    private static void Load()
    {
        IL_Main.DrawSunAndMoon += DrawSunAndMoon_PreventDragging;

        On_Main.DrawMenu += DrawMenu_DisableInteraction;

        On_UserInterface.Update += Update_DisableInteraction;

        IL_Main.UpdateMenu += UpdateMenu_ReverseTime;

        On_Main.DrawSunAndMoon += DrawSunAndMoon_Velocity;
    }

    private static void DrawSunAndMoon_PreventDragging(ILContext il)
    {
        var c = new ILCursor(il);

        ILLabel? jumpCelestialBodyGrabbingTarget = null;

        c.GotoNext(
            MoveType.After,
            i => i.MatchCall(typeof(FocusHelper), $"get_{nameof(FocusHelper.AllowUIInputs)}"),
            i => i.MatchBrfalse(out jumpCelestialBodyGrabbingTarget)
        );

        Debug.Assert(jumpCelestialBodyGrabbingTarget is not null);

        c.EmitDelegate(static () => Main.alreadyGrabbingSunOrMoon || CanGrabCelestialBody);

        c.EmitBrfalse(jumpCelestialBodyGrabbingTarget);
    }

    private static void DrawMenu_DisableInteraction(On_Main.orig_DrawMenu orig, Main self, GameTime gameTime)
    {
        int oldMouseX = Main.mouseX;
        int oldMouseY = Main.mouseY;

        if (Main.alreadyGrabbingSunOrMoon)
        {
            Main.mouseX = int.MaxValue;
            Main.mouseY = int.MaxValue;
        }

        orig(self, gameTime);

        Main.mouseX = oldMouseX;
        Main.mouseY = oldMouseY;
    }

    private static void Update_DisableInteraction(On_UserInterface.orig_Update orig, UserInterface self, GameTime time)
    {
        if (self != Main.MenuUI)
        {
            orig(self, time);

            return;
        }

        int oldMouseX = Main.mouseX;
        int oldMouseY = Main.mouseY;

        if (Main.alreadyGrabbingSunOrMoon)
        {
            Main.mouseX = int.MaxValue;
            Main.mouseY = int.MaxValue;
        }

        orig(self, time);

        Main.mouseX = oldMouseX;
        Main.mouseY = oldMouseY;
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

        c.EmitDelegate(
            () =>
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
                    Main.moonPhase = Math.Abs((Main.moonPhase - 1) % 8);
                }

                Main.time = (Main.dayTime ? Main.nightLength : Main.dayLength) + Main.time * timeRatio;
                Main.dayTime = !Main.dayTime;

                if (!Main.lockMenuBGChange)
                {
                    Main.moonType = Main.rand.Next(TextureAssets.Moon.Length);
                }
            }
        );

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
        var screenSize = new Vector2(Main.screenWidth, Main.screenHeight);
        var oldPosition = Main.LastCelestialBodyPosition * screenSize;

        orig(self, sceneArea, moonColor, sunColor, tempMushroomInfluence);

        var position = Main.LastCelestialBodyPosition * screenSize;

        // TODO: Allow celestial body movement when connecting to a server
        if (!Main.gameMenu || Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        if (Main.alreadyGrabbingSunOrMoon)
        {
            celestialBodyVelocity = position - oldPosition;
            return;
        }

        var sunMoonWidth = Main.dayTime
            ? TextureAssets.Sun.Value.Width
            : TextureAssets.Moon[Main.moonType].Value.Width;

        var timeLength = Main.dayTime
            ? Main.dayLength
            : Main.nightLength;

        ref short modY = ref (Main.dayTime ? ref Main.sunModY : ref Main.moonModY);

        if (Main.mouseRight && CanGrabCelestialBody)
        {
            const float pull_speed = 0.13f;
            const float screen_margin = 0.65f;
            const float max_velocity = 350f;
            const float drag = 0.12f;

            var diff = Main.MouseScreen - position;

            if (Math.Abs(diff.X) >= screenSize.X * screen_margin)
            {
                diff.X = -diff.X;
            }

            diff = diff.SafeNormalize(Vector2.UnitY) * MathF.Min(diff.Length(), max_velocity);

            celestialBodyVelocity = Vector2.Lerp(celestialBodyVelocity, diff * pull_speed, drag);
        }
        else
        {
            var displacement = -modY;

            const float spring_strength = 0.07f;
            const float min_dampening = 0.94f;
            const float max_dampening = 0.78f;

            var dist = Math.Abs(modY);
            var t = MathHelper.Clamp(dist / 200f, 0f, 1f);
            var dampening = MathHelper.Lerp(min_dampening, max_dampening, MathF.Pow(t, 2));

            celestialBodyVelocity.Y += displacement * spring_strength;
            celestialBodyVelocity.Y *= dampening;
            celestialBodyVelocity.Y += 0.03f;

            const float x_dampening = 0.035f;
            celestialBodyVelocity.X = MathHelper.Lerp(celestialBodyVelocity.X, 0f, x_dampening);
        }

        modY += (short)celestialBodyVelocity.Y;

        double newTime = position.X + celestialBodyVelocity.X + sunMoonWidth;

        newTime /= Main.screenWidth + sunMoonWidth * 2f;
        newTime *= timeLength;

        Main.time = newTime;
    }
}
