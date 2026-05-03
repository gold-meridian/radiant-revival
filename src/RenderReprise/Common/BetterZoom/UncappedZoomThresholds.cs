using Daybreak.Common.Features.Hooks;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using Terraria;
using Terraria.Graphics;

namespace RenderReprise.Common;

internal static class UncappedZoomThresholds
{
    [OnLoad]
    private static void ApplyHooks()
    {
        IL_Main.DoDraw += il =>
        {
            var c = new ILCursor(il);

            c.GotoNext(MoveType.After, x => x.MatchLdsflda<Main>(nameof(Main.GameViewMatrix)));
            c.EmitDup();
            c.EmitDelegate(
                (ref SpriteViewMatrix gameViewMatrix) =>
                {
                    // No more clamping of GameZoomTarget.
                    gameViewMatrix.Zoom = new Vector2(Main.ForcedMinimumZoom * Main.GameZoomTarget);
                }
            );
        };
    }
}
