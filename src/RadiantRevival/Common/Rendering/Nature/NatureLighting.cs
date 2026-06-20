using Daybreak.Common.Features.Hooks;
using MonoMod.Cil;
using Terraria.GameContent.Drawing;

namespace RadiantRevival.Common;

public static class NatureLighting
{
    [OnLoad]
    private static void Load()
    {
        IL_NextNatureRenderer.DrawAfterAllObjects += DrawAfterAllObjects_RemoveGlowmask;
    }

    private static void DrawAfterAllObjects_RemoveGlowmask(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(
            MoveType.Before,
            i => i.MatchLdcR4(0f),
            i => i.MatchBneUn(out _)
        );

        c.EmitPop();

        c.EmitLdcR4(0f);
    }
}

