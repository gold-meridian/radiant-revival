using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Core;
using ReLogic.Content;
using SteelSeries.GameSense;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ModLoader;
// ReSharper disable PossiblyImpureMethodCallOnReadonlyVariable

namespace RadiantRevival.Common;

public static class MoonStyles
{
    private static readonly Color moon_sky_color = new(128, 168, 248);

    private static readonly Color moon_atmosphere = new(226, 235, 255);
    private static readonly Color moon_atmosphere_shadow = new(54, 12, 34);

    private static readonly Dictionary<int, Asset<Texture2D>> moonTextures = new()
    {
        { 0, Assets.Sky.CelestialBodies.Moon0.Asset },
        { 1, Assets.Sky.CelestialBodies.Moon1.Asset },
        { 2, Assets.Sky.CelestialBodies.Moon2.Asset },
        { 3, Assets.Sky.CelestialBodies.Moon3.Asset },
        { 4, Assets.Sky.CelestialBodies.Moon4.Asset },
        { 5, Assets.Sky.CelestialBodies.Moon5.Asset },
        { 6, Assets.Sky.CelestialBodies.Moon6.Asset },
        { 7, Assets.Sky.CelestialBodies.Moon7.Asset },
        { 8, Assets.Sky.CelestialBodies.Moon8.Asset },
    };

    public delegate bool DrawAction(SpriteBatch sb, GraphicsDevice device, Vector2 position, Color color, float rotation, float scale);

    public static readonly Dictionary<int, DrawAction> SpecialStyleDrawing = [];

    private static WrapperShaderData<Assets.Sky.MoonShader.Parameters>? moonShaderData;

    [OnLoad]
    private static void Load()
    {
        moonShaderData = Assets.Sky.MoonShader.CreateMoonShader();

        IL_Main.DrawSunAndMoon += DrawSunAndMoon_MoonStyles;

        MonoModHooks.Modify(
            typeof(ModMenu).GetProperty(
                nameof(ModMenu.MoonTexture),
                BindingFlags.Instance | BindingFlags.Public
            )!.GetMethod,
            get_MoonTexture_MoonStyles
        );
    }

    public static int AddMoonStyle(Color color, Asset<Texture2D> texture, Asset<Texture2D>? hdTexture, DrawAction? specialAction = null)
    {
        int index = TextureAssets.Moon.Length;

        Array.Resize(ref HorizonHelper.MoonColors, index + 1);
        HorizonHelper.MoonColors[index] = color;

        Array.Resize(ref TextureAssets.Moon, index + 1);
        TextureAssets.Moon[index] = texture;

        if (hdTexture is not null)
        {
            moonTextures.Add(index, hdTexture);
        }

        if (specialAction is not null)
        {
            SpecialStyleDrawing.Add(index, specialAction);
        }

        return index;
    }

    [OnUnload]
    private static void ClearMoonStyles()
    {
        Array.Resize(ref HorizonHelper.MoonColors, Main.maxMoons);
        Array.Resize(ref TextureAssets.Moon, Main.maxMoons);
    }

    private static void DrawSunAndMoon_MoonStyles(ILContext il)
    {
        var c = new ILCursor(il);

        int moonPositionIndex = -1; // loc
        int moonColorIndex = -1; // arg
        int moonRotationIndex = -1; // loc
        int moonScaleIndex = -1; // loc

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
        if (SpecialStyleDrawing.TryGetValue(Main.moonType, out var action)
            && action.Invoke(sb, device, position, color, rotation, scale))
        {
            return true;
        }

        if (!moonTextures.TryGetValue(Main.moonType, out var asset))
        {
            return false;
        }

        DrawBody(asset.Value);

        return true;

        void DrawBody(Texture2D texture)
        {
            Debug.Assert(moonShaderData is not null);

            sb.End(out var snapshot);
            sb.Begin(snapshot with { SortMode = SpriteSortMode.Immediate });

            const float atmo_ratio = 0.78f;

            Color skyColor = Main.ColorOfTheSkies.MultiplyRGB(moon_sky_color);

            moonShaderData.Parameters.atmoColor = moon_atmosphere.ToVector4();
            moonShaderData.Parameters.atmoShadowColor = moon_atmosphere_shadow.ToVector4();

            moonShaderData.Parameters.shadowColor = skyColor.ToVector4();

            moonShaderData.Parameters.shadowRotation = -(Main.moonPhase / 8f) * MathHelper.TwoPi;

            moonShaderData.Parameters.radius = atmo_ratio;

            moonShaderData.Apply();

            Vector2 size = new Vector2(TextureAssets.Moon[Main.moonType].Value.Width / atmo_ratio);

            size *= scale;

            size /= texture.Size();

            Vector2 origin = texture.Size() * 0.5f;

            sb.Draw(texture, position, null, color, rotation, origin, size, SpriteEffects.None, 0f);

            sb.Restart(in snapshot);
        }
    }
}

