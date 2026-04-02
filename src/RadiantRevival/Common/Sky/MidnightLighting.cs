using Daybreak.Common.Features.Hooks;
using MonoMod.Cil;
using Terraria;
using Terraria.GameContent;

namespace RadiantRevival.Common;

// TODO: Config
public sealed class MidnightLighting
{
    [OnLoad]
    private static void Load()
    {
        IL_Main.SetBackColor += SetBackColor_PitchBlack;
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
