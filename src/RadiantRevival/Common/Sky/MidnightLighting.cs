using Daybreak.Common.Features.Hooks;
using MonoMod.Cil;
using System;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;

namespace RadiantRevival.Common;

// TODO: Config
public sealed class MidnightLighting
{
    [OnLoad]
    private static void Load()
    {
        IL_TileDrawing.DrawSingleTile += DrawSingleTile_DrawBlackEdges;

        IL_Main.SetBackColor += SetBackColor_PitchBlack;
    }

    private static void DrawSingleTile_DrawBlackEdges(ILContext il)
    {
        var c = new ILCursor(il);

        int iIndex = -1; // arg
        int jIndex = -1; // arg

        int skipDrawIndex = -1; // loc

        c.GotoNext(
            i => i.MatchLdarg(out iIndex),
            i => i.MatchLdarg(out jIndex),
            i => i.MatchCall<DrawBlackHelper>(nameof(DrawBlackHelper.DrawBlack))
        );

        c.GotoPrev(
            MoveType.Before,
            i => i.MatchLdloc(out _),
            i => i.MatchAnd()
        );

        c.GotoPrev(
            MoveType.After,
            i => i.MatchLdloc(out skipDrawIndex)
        );

        c.EmitLdarg(iIndex);
        c.EmitLdarg(jIndex);

        c.EmitDelegate(EdgeTile);

        c.EmitOr();

        c.EmitStloc(skipDrawIndex);
        c.EmitLdloc(skipDrawIndex);
    }

    private static bool EdgeTile(int i, int j)
    {
        Tile center = Main.tile[i, j];

        if (!BlocksLight(center))
        {
            return true;
        }

        Tile[] neighbors = [
            Main.tile[Math.Min(i + 1, Main.tile.Width), j],
            Main.tile[Math.Max(i - 1, 0), j],
            Main.tile[i, Math.Min(j + 1, Main.tile.Height)],
            Main.tile[i, Math.Max(j - 1, 0)]
        ];

        return neighbors.Any(t => !BlocksLight(t));

        static bool BlocksLight(Tile tile)
        {
            if (tile.HasTile
             && Main.tileBlockLight[tile.type]
             && Main.tileSolid[tile.type]
             && tile.BlockType == BlockType.Solid)
            {
                return true;
            }

            if (tile.WallType != WallID.None
             && !Main.wallLight[tile.WallType]
             && !WallID.Sets.Transparent[tile.WallType])
            {
                return true;
            }

            return false;
        }
    }

    private static void SetBackColor_PitchBlack(ILContext il)
    {
        var c = new ILCursor(il);

        int minimalLightIndex = -1;

        ILLabel jumpMinimalLightTarget = c.DefineLabel();

        ReplaceAddition(25, 1);
        ReplaceAddition(35, 6);

        ReplaceAddition(15, 2);

        ReplaceAddition(35, 1);
        ReplaceAddition(15, 2);

        ReplaceAddition(5, 6);

        // Menu
        ReplaceValue(35, 3);

        while (c.TryGotoNext(
                   MoveType.After,
                   i => i.MatchLdcI4(15)
               ))
        {
            c.EmitPop();

            c.EmitLdcI4(0);
        }

        c.Index = 0;

        c.GotoNext(
            i => i.MatchLdloca(out minimalLightIndex),
            i => i.MatchCall<DontStarveSeed>(nameof(DontStarveSeed.ModifyMinimumLightColorAtNight))
        );

        c.GotoPrev(
            MoveType.Before,
            i => i.MatchCall<Main>(nameof(Main.GetMoonPhase))
        );

        c.EmitLdcI4(0);

        c.EmitStloc(minimalLightIndex);

        c.EmitBr(jumpMinimalLightTarget);

        c.GotoNext(
            MoveType.After,
            i => i.MatchLdloca(out minimalLightIndex),
            i => i.MatchCall<DontStarveSeed>(nameof(DontStarveSeed.ModifyMinimumLightColorAtNight))
        );

        c.MoveAfterLabels();

        c.MarkLabel(jumpMinimalLightTarget);

        while (c.TryGotoNext(
                   MoveType.After,
                   i => i.MatchLdcI4(25)
               ))
        {
            c.EmitPop();

            c.EmitLdcI4(0);
        }

        void ReplaceAddition(float value, int loops)
        {
            for (int i = 0; i < loops; i++)
            {
                c.GotoNext(
                    MoveType.Before,
                    i => i.MatchLdcR4(value),
                    i => i.MatchAdd()
                );

                c.Index++;

                c.EmitPop();

                c.EmitLdcR4(0);
            }
        }

        void ReplaceValue(int value, int loops)
        {
            for (int i = 0; i < loops; i++)
            {
                c.GotoNext(
                    MoveType.After,
                    i => i.MatchLdcI4(value)
                );

                c.EmitPop();

                c.EmitLdcI4(0);
            }
        }
    }
}
