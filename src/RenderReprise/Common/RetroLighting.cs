using Daybreak.Common.CIL;
using Daybreak.Common.Features.Hooks;
using MonoMod.Cil;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Rocks;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Liquid;
using Terraria.Graphics.Capture;
using Terraria.ModLoader;

namespace RenderReprise.Common;

// TODO: Failsafe in-case the game fails to create the needed targets repeatedly? (vanilla issue)
// TODO: Config
// TODO: Fix DrawCapture in general as it doesn't use any targets for rendering
public static class RetroLighting
{
    [OnLoad]
    private static void Load()
    {
        MonoModHooks.Modify(
            typeof(Main).GetProperty(
                nameof(Main.RenderTargetsRequired),
                BindingFlags.Public | BindingFlags.Static
            )!.GetMethod,
            get_RenderTargetsRequired_ForceCapture
        );

        MonoModHooks.Modify(
            typeof(Lighting).GetProperty(
                nameof(Lighting.UpdateEveryFrame),
                BindingFlags.Public | BindingFlags.Static
            )!.GetMethod,
            _ => { }
        );

        IL_Main.DoDraw += DoDraw_DontCaptureMenuUI;
        IL_Main.DoDraw += DoDraw_CaptureRetroLighting;

        IL_CaptureCamera.EndDrawCapture += EndDrawCapture_AllowCapturing;
        IL_Main.DrawCapture += _ => { };

        IL_Main.DrawLiquid += DrawLiquid_UseNewRendering;

        /*
        if (!LiquidEdgeRenderer.Enabled)
        {
            return;
        }

        MonoModHooks.Modify(
            typeof(LiquidEdgeRenderer).GetProperty(
                nameof(LiquidEdgeRenderer.Active),
                BindingFlags.Public | BindingFlags.Static
            )!.GetMethod,
            get_Active_AllowLiquidEdges
        );

        IL_Main.DrawWaters += _ => { };
        IL_Main.DrawLiquid += _ => { };
        IL_Main.DrawBlack += _ => { };
        IL_LiquidRenderer.InternalPrepareDraw += _ => { };
        IL_TileDrawing.Draw += _ => { };

        IL_Main.oldDrawWater += oldDrawWater_UseLiquidCache;
        */
    }

    private static void get_RenderTargetsRequired_ForceCapture(ILContext il)
    {
        var c = new ILCursor(il);

        c.EmitLdcI4(1);
        c.EmitRet();
    }

    private static void DoDraw_DontCaptureMenuUI(ILContext il)
    {
        var c = new ILCursor(il);

        var jumpDrawMenuTarget = c.DefineLabel();
        var drawMenuTarget = c.DefineLabel();

        c.GotoNext(
            MoveType.Before,
            i => i.MatchCall<Main>(nameof(Main.DrawMenu))
        );

        var c2 = c.Clone();

        c2.GotoNext(
            MoveType.Before,
            i => i.MatchLdsfld<Main>(nameof(Main.HorizonHelper)),
            i => i.MatchLdloc(out _)
        );

        c2.EmitRet();
        c2.MarkLabel(jumpDrawMenuTarget);

        c2.GotoNext(
            MoveType.Before,
            i => i.MatchRet()
        );

        c2.MoveAfterLabels();
        c2.EmitBr(drawMenuTarget);

        c.GotoPrev(
            MoveType.After,
            i => i.MatchCall<Main>(nameof(Main.DrawLensFlare))
        );

        c.EmitBr(jumpDrawMenuTarget);
        c.MarkLabel(drawMenuTarget);
    }

    private static void DoDraw_CaptureRetroLighting(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(i => i.MatchLdstr("Sepia"));

        c.GotoNext(
            MoveType.After,
            i => i.MatchCall<Lighting>($"get_{nameof(Lighting.NotRetro)}")
        );

        c.EmitPop();
        c.EmitDelegate(static () => Main.targetSet);
    }

    private static void EndDrawCapture_AllowCapturing(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(
            MoveType.After,
            i => i.MatchCall<Lighting>($"get_{nameof(Lighting.NotRetro)}")
        );

        c.EmitPop();
        c.EmitLdcI4(1);
    }

    private static void DrawLiquid_UseNewRendering(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(
            MoveType.After,
            i => i.MatchCall<Lighting>($"get_{nameof(Lighting.NotRetro)}")
        );

        c.EmitPop();
        c.EmitLdcI4(1);
    }


    private static void get_Active_AllowLiquidEdges(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(
            MoveType.After,
            i => i.MatchLdsfld(typeof(LiquidEdgeRenderer), nameof(LiquidEdgeRenderer.Enabled))
        );

        c.EmitRet();
    }

    // Don't do this.
    private static unsafe void oldDrawWater_UseLiquidCache(ILContext il)
    {
        var c = new ILCursor(il);

        var cachePtrIndex = c.AddVariable(il.Import(typeof(LiquidRenderer.LiquidDrawCache*)).MakePinnedType());
        var cachePtr2Index = c.AddVariable(il.Import(typeof(LiquidRenderer.LiquidDrawCache*)));

        var skipLoopEscape = c.DefineLabel();

        var skipOldLoopStartTarget = c.DefineLabel();
        var skipOldLoopEndTarget = c.DefineLabel();

        var loopXStartTarget = c.DefineLabel();
        var loopYStartTarget = c.DefineLabel();

        var loopXEndTarget = c.DefineLabel();
        var loopYEndTarget = c.DefineLabel();

        var skipFramingTarget = c.DefineLabel();

        ILLabel? oldLoopYEndTarget = null;

        // oldDrawWater loops over Y before X making this edit ~9x more difficult
        var iIndex = -1; // Y, loc
        var jIndex = -1; // X, loc

        var positionIndex = -1;
        var sourceRectangleIndex = -1;

        var liquidTypeIndex = -1;

        // Loop start
        {
            c.GotoNext(
                MoveType.Before,
                i => i.MatchLdsflda<Main>(nameof(Main.tile))
            );

            c.MoveAfterLabels();

            c.FindPrev(
                out _,
                i => i.MatchStloc(out iIndex),
                i => i.MatchBr(out _),
                i => i.MatchStloc(out jIndex)
            );

            c.MarkLabel(skipOldLoopStartTarget);

            c.EmitLdsfld(
                il.Import(
                    typeof(LiquidRenderer).GetField(
                        nameof(LiquidRenderer.Instance),
                        BindingFlags.Public | BindingFlags.Static
                    )!
                )
            );
            c.EmitLdfld(
                il.Import(
                    typeof(LiquidRenderer).GetField(
                        nameof(LiquidRenderer._drawCache),
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )!
                )
            );
            c.EmitLdcI4(0);
            c.EmitLdelema(typeof(LiquidRenderer.LiquidDrawCache));
            c.EmitStloc(cachePtrIndex);

            c.EmitLdloc(cachePtrIndex);
            c.EmitConvU();
            c.EmitStloc(cachePtr2Index);

            c.EmitLdloca(jIndex);
            c.EmitDelegate(
                static (ref int x) =>
                {
                    x = LiquidRenderer.Instance._drawArea.X;
                }
            );

            c.EmitBr(loopXEndTarget);

            c.MarkLabel(loopXStartTarget);

            c.EmitLdloca(iIndex);
            c.EmitDelegate(
                static (ref int y) =>
                {
                    y = LiquidRenderer.Instance._drawArea.Y;
                }
            );

            c.EmitBr(loopYEndTarget);

            c.MarkLabel(loopYStartTarget);

            c.GotoPrev(
                MoveType.Before,
                i => i.MatchLdloc(out _),
                i => i.MatchStloc(iIndex),
                i => i.MatchBr(out oldLoopYEndTarget)
            );

            Debug.Assert(oldLoopYEndTarget is not null);

            c.EmitBr(skipOldLoopStartTarget);
        }

        // Skip drawing
        {
            c.GotoNext(
                MoveType.Before,
                i => i.MatchCall<Tile>($"get_{nameof(Tile.liquid)}")
            );

            c.GotoPrev(
                MoveType.Before,
                i => i.MatchLdsflda<Main>(nameof(Main.tile))
            );

            c.EmitBr(skipLoopEscape);

            c.GotoNext(
                MoveType.After,
                i => i.MatchLdarg(out _),
                i => i.MatchOr(),
                i => i.MatchBrfalse(out _)
            );

            c.MarkLabel(skipLoopEscape);

            c.EmitLdloc(cachePtr2Index);

            c.EmitLdfld(
                il.Import(
                    typeof(LiquidRenderer.LiquidDrawCache).GetField(
                        nameof(LiquidRenderer.LiquidDrawCache.IsVisible),
                        BindingFlags.Public | BindingFlags.Instance
                    )!
                )
            );

            c.EmitBrfalse(loopYEndTarget);
        }

        // Framing
        {
            c.GotoNext(
                MoveType.After,
                i => i.MatchCall<Main>(nameof(Main.DrawTileInWater))
            );

            c.FindPrev(
                out _,
                i => i.MatchLdloc(out liquidTypeIndex),
                i => i.MatchBeq(out _)
            );

            c.FindNext(
                out _,
                i => i.MatchLdloca(out positionIndex),
                i => i.MatchLdloca(out sourceRectangleIndex)
            );

            c.GotoNext(
                MoveType.After,
                i => i.MatchLdcI4(1),
                i => i.MatchStloc(out _)
            );

            c.EmitLdloc(cachePtr2Index);

            c.EmitLdloca(positionIndex);
            c.EmitLdloca(sourceRectangleIndex);

            c.EmitLdloc(jIndex);
            c.EmitLdloc(iIndex);

            c.EmitLdloca(liquidTypeIndex);

            c.EmitDelegate(
                static (LiquidRenderer.LiquidDrawCache* ptr, ref Vector2 position, ref Rectangle source, int x, int y, ref int type) =>
                {
                    type = ptr->Type;

                    source = ptr->SourceRectangle;

                    // Hack, TODO: Remove the logic proper at a later date
                    Main.wFrame = 0;

                    if (ptr->IsSurfaceLiquid)
                    {
                        source.Y = 1280;
                    }
                    else if (source.X == 16)
                    {
                        source.Y += LiquidRenderer.Instance._waterfallAnimationFrame * 80;
                    }
                    else
                    {
                        source.Y += LiquidRenderer.Instance._animationFrame * 80;
                    }

                    position = new Vector2(x << 4, y << 4) + ptr->LiquidOffset;
                }
            );

            c.EmitBr(skipFramingTarget);

            c.GotoNext(
                MoveType.Before,
                i => i.MatchLdloca(out _),
                i => i.MatchCall<Color>($"get_{nameof(Color.R)}")
            );

            c.MarkLabel(skipFramingTarget);
        }

        // Textures
        {
            var c2 = c.Clone();

            // Should we be messing with the textures retro water uses? Are we tarnishing the 'retro look'?
            while (c2.TryGotoNext(
                       MoveType.After,
                       i => i.MatchLdsfld(typeof(TextureAssets), nameof(TextureAssets.Liquid))
                   ))
            {
                c2.EmitPop();

                c2.EmitDelegate(static () => LiquidRenderer.Instance._liquidTextures);
            }
        }

        // Loop end
        {
            c.GotoLabel(oldLoopYEndTarget);

            c.GotoPrev(
                MoveType.Before,
                i => i.MatchLdloc(jIndex),
                i => i.MatchLdcI4(1),
                i => i.MatchAdd()
            );

            c.MoveAfterLabels();

            c.MarkLabel(loopYEndTarget);

            c.EmitLdloc(cachePtr2Index);
            c.EmitSizeof(typeof(LiquidRenderer.LiquidDrawCache));
            c.EmitAdd();
            c.EmitStloc(cachePtr2Index);

            c.EmitLdloca(iIndex);
            c.EmitDelegate(
                static (ref int y) =>
                {
                    var drawArea = LiquidRenderer.Instance._drawArea;

                    y++;
                    return y < drawArea.Y + drawArea.Height;
                }
            );

            c.EmitBrtrue(loopYStartTarget);

            c.MarkLabel(loopXEndTarget);

            c.EmitLdloca(jIndex);
            c.EmitDelegate(
                static (ref int x) =>
                {
                    var drawArea = LiquidRenderer.Instance._drawArea;

                    x++;
                    return x < drawArea.X + drawArea.Width;
                }
            );

            c.EmitBrtrue(loopXStartTarget);

            c.EmitBr(skipOldLoopEndTarget);

            c.GotoNext(
                MoveType.Before,
                i => i.MatchLdsfld<Main>(nameof(Main.drewLava))
            );

            c.MarkLabel(skipOldLoopEndTarget);
        }
    }
}
