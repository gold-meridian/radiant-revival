using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Core;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ModLoader;

// ReSharper disable PossiblyImpureMethodCallOnReadonlyVariable

namespace RadiantRevival.Common;

// TODO: Config
public static class MoonStyles
{
#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Init()
    {
        // May inline if a ModMenu declares MoonTexture and calls base.MoonTexture
        MonoModHooks.Modify(
            typeof(ModMenu).GetProperty(
                nameof(ModMenu.MoonTexture),
                BindingFlags.Instance | BindingFlags.Public
            )!.GetMethod,
            get_MoonTexture_MoonStyles
        );
    }
#pragma warning restore CA2255

    [OnLoad]
    private static void Load()
    {
        IL_Main.DrawSunAndMoon += DrawSunAndMoon_MoonStyles;
    }

    private static void DrawSunAndMoon_MoonStyles(ILContext il)
    {
        var c = new ILCursor(il);

        var moonPositionIndex = -1; // loc
        var moonColorIndex = -1;    // arg
        var moonRotationIndex = -1; // loc
        var moonScaleIndex = -1;    // loc

        ILLabel? jumpMoonRenderingTarget = null;

        c.GotoNext(
            MoveType.Before,
            i => i.MatchCall(typeof(Utils), nameof(Utils.Clamp))
        );

        c.EmitPop();

        c.EmitDelegate(static () => TextureAssets.Moon.Length - 1);

        c.GotoNext(
            MoveType.Before,
            i => i.MatchLdsfld<WorldGen>(nameof(WorldGen.drunkWorldGen)),
            i => i.MatchBrfalse(out _)
        );

        c.FindPrev(
            out _,
            i => i.MatchStarg(out moonColorIndex),
            i => i.MatchLdloca(out moonPositionIndex),
            i => i.MatchLdsfld<Main>(nameof(Main.moonModY))
        );

        c.FindNext(
            out _,
            i => i.MatchLdarg(moonColorIndex),
            i => i.MatchLdloc(out moonRotationIndex),
            i => i.MatchLdcR4(2f),
            i => i.MatchDiv(),
            i => i.MatchNewobj<Vector2>(),
            i => i.MatchLdloc(out moonScaleIndex)
        );

        c.EmitLdloc(moonPositionIndex);
        c.EmitLdarg(moonColorIndex);
        c.EmitLdloc(moonRotationIndex);
        c.EmitLdloc(moonScaleIndex);

        c.EmitDelegate(
            static (Vector2 position, Color color, float rotation, float scale) =>
            {
                // TODO
                if (Main.pumpkinMoon || Main.snowMoon || WorldGen.drunkWorldGen)
                {
                    return false;
                }

                color.A = byte.MaxValue;

                return Draw(
                    Main.spriteBatch,
                    Main.graphics.GraphicsDevice,
                    position,
                    color,
                    rotation,
                    scale
                );
            }
        );

        {
            var c2 = c.Clone();

            c2.GotoNext(
                i => i.MatchBr(out jumpMoonRenderingTarget)
            );

            Debug.Assert(jumpMoonRenderingTarget is not null);

            c2.GotoNext(
                MoveType.Before,
                i => i.MatchLdsfld<Main>(nameof(Main.dayTime)),
                i => i.MatchBrfalse(out _)
            );

            c2.MoveAfterLabels();
        }

        c.EmitBrtrue(jumpMoonRenderingTarget);
    }

    private static void get_MoonTexture_MoonStyles(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(
            MoveType.Before,
            i => i.MatchCall(typeof(Utils), nameof(Utils.Clamp))
        );

        c.EmitPop();

        c.EmitDelegate(static () => TextureAssets.Moon.Length - 1);
    }

    private static bool Draw(SpriteBatch sb, GraphicsDevice device, Vector2 position, Color color, float rotation, float scale)
    {
        var _ = Assets.Sky.CelestialBodies.MoonTest.Asset.Value;
        return true;
    }
}
