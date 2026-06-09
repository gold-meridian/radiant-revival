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
    public delegate bool DrawAction(SpriteBatch sb, GraphicsDevice device, Vector2 position, Color color, float rotation, float scale);

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

    public static readonly Dictionary<int, DrawAction> SpecialStyleDrawing = new()
    {
        { 2, DrawMoon2Extras },
    };

    private static WrapperShaderData<Assets.Sky.RingShader.Parameters>? ringShaderData;

    private static WrapperShaderData<Assets.Sky.MoonShader.Parameters>? moonShaderData;

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
        ringShaderData = Assets.Sky.RingShader.CreateRingShader();
        moonShaderData = Assets.Sky.MoonShader.CreateMoonShader();

        IL_Main.DrawSunAndMoon += DrawSunAndMoon_MoonStyles;
    }

    [ModCall]
    public static int AddMoonStyle(Color color, Asset<Texture2D> texture, Asset<Texture2D>? hdTexture, DrawAction? specialAction = null)
    {
        var index = TextureAssets.Moon.Length;

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
        if (SpecialStyleDrawing.TryGetValue(Main.moonType, out var action)
         && !action.Invoke(sb, device, position, color, rotation, scale))
        {
            return true;
        }

        if (!moonTextures.TryGetValue(Main.moonType, out var asset))
        {
            return false;
        }

        sb.End(out var ss);
        sb.Begin(ss with { SortMode = SpriteSortMode.Immediate });

        DrawBody(sb, asset.Value, position, color, rotation, scale);

        sb.Restart(in ss);

        return true;
    }

    private static void DrawBody(SpriteBatch sb, Texture2D texture, Vector2 position, Color color, float rotation, float scale, float atmoRatio = 0.65f)
    {
        Debug.Assert(moonShaderData is not null);

        var skyColor = Main.ColorOfTheSkies.MultiplyRGB(moon_sky_color);

        moonShaderData.Parameters.AtmoColor = moon_atmosphere.ToVector4();
        moonShaderData.Parameters.AtmoShadowColor = moon_atmosphere_shadow.ToVector4();
        moonShaderData.Parameters.ShadowColor = skyColor.ToVector4();
        moonShaderData.Parameters.ShadowRotation = Main.moonPhase / 8f * MathHelper.TwoPi;
        moonShaderData.Parameters.Radius = atmoRatio;

        moonShaderData.Apply();

        var size = new Vector2(TextureAssets.Moon[Main.moonType].Value.Width / atmoRatio);

        size *= scale;

        size /= texture.Size();

        var origin = texture.Size() * 0.5f;

        sb.Draw(texture, position, null, color, rotation, origin, size, SpriteEffects.None, 0f);
    }

#region Styles
    private static bool DrawMoon2Extras(SpriteBatch sb, GraphicsDevice device, Vector2 position, Color color, float rotation, float scale)
    {
        sb.End(out var ss);
        sb.Begin(ss with { SortMode = SpriteSortMode.Immediate });

        Vector2 ringSize = new(0.28f, 0.07f);

        const float ring_rotation = -0.13f;

        const bool flip = false;

        var skyColor = Main.ColorOfTheSkies.MultiplyRGB(moon_sky_color);

        var rings = Assets.Sky.CelestialBodies.Moon2Rings.Asset.Value;

        var shadowRotation = Main.moonPhase / 8f * MathHelper.TwoPi;

        if (!flip)
        {
            shadowRotation = MathHelper.Pi - shadowRotation;
        }

        DrawRing(!flip);

        var moon = Assets.Sky.CelestialBodies.Moon2.Asset.Value;

        DrawBody(sb, moon, position, color, rotation, scale);

        DrawRing(flip);

        sb.Restart(in ss);

        return false;

        void DrawRing(bool upper)
        {
            Debug.Assert(ringShaderData is not null);

            ringShaderData.Parameters.ShadowRotation = shadowRotation;
            ringShaderData.Parameters.ShadowColor = skyColor.ToVector4();

            ringShaderData.Apply();

            sb.Draw(
                rings,
                position,
                rings.Frame(1, 2, 0, upper ? 0 : 1),
                color,
                rotation + ring_rotation,
                new Vector2(rings.Width * 0.5f, upper ? (rings.Height * 0.5f) : 0f),
                ringSize * scale,
                SpriteEffects.None,
                0f
            );
        }
    }
#endregion
}
