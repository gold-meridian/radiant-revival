using Daybreak.Common.CIL;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using Terraria;

namespace RadiantRevival.Common.SmoothLighting;

public static class AmbientOcclusion
{
    [OnLoad]
    private static void Load()
    {
        IL_Main.DoDraw_WallsAndBlacks += WallsAndBlacks_Occlusion;
    }

    private static void WallsAndBlacks_Occlusion(ILContext il)
    {
        var c = new ILCursor(il);

        var snapshotDef = il.AddVariable<SpriteBatchSnapshot>();

        c.GotoNext(
            MoveType.After,
            i => i.MatchCallvirt<SpriteBatch>(nameof(SpriteBatch.Begin)),
            i => i.MatchBr(out _)
        );

        c.MoveAfterLabels();

        c.EmitLdloca(snapshotDef);

        c.EmitDelegate(
            static (ref SpriteBatchSnapshot ss) =>
            {
                Main.spriteBatch.End(out ss);
                Main.spriteBatch.Begin(in ss);

                // APPLY SHADER HERE
            }
        );

        c.GotoNext(
            MoveType.After,
            i => i.MatchCallvirt<SpriteBatch>(nameof(SpriteBatch.Draw))
        );

        c.EmitLdloca(snapshotDef);

        c.EmitDelegate(
            static (ref SpriteBatchSnapshot ss) =>
            {
                Main.spriteBatch.Restart(in ss);
            }
        );
    }
}
