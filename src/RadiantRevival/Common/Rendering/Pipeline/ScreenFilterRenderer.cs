using Daybreak.Common.CIL;
using Daybreak.Common.Features.Hooks;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

public readonly record struct ScreenFilterRendererContext(
    RenderTarget2D ScreenTarget,
    RenderTarget2D ScreenTargetSwap,
    Color Color
);

public static class ScreenFilterRenderer
{
    private static readonly Dictionary<EffectPriority, List<IScreenFilterStep>> filters_by_priority = [];

    [OnLoad]
    private static void Load(Mod mod)
    {
        IL_FilterManager.EndCapture_RenderTarget2D_RenderTarget2D_RenderTarget2D_Vector2_Vector2_Vector2 += EndCapture_ScreenFilters;
        On_FilterManager.CanCapture += (_, _) => true;
        IL_Main.DoDraw += _ => { };
    }

    public static void Register(IScreenFilterStep step)
    {
        filters_by_priority.TryAdd(step.Priority, []);
        filters_by_priority[step.Priority].Add(step);
    }

    private static void EndCapture_ScreenFilters(ILContext il)
    {
        var c = new ILCursor(il);

        var priorPriority = c.AddVariable<EffectPriority?>();

        var tIndex = -1;  // loc
        var t2Index = -1; // loc

        var value2Index = -1; // loc

        c.GotoNext(
            MoveType.After,
            i => i.MatchLdloca(out t2Index),
            i => i.MatchLdloca(out tIndex),
            i => i.MatchCall(typeof(Utils), nameof(Utils.Swap))
        );

        c.GotoNext(
            MoveType.After,
            i => i.MatchLdloc(out _),
            i => i.MatchCallvirt<LinkedListNode<Filter>>($"get_{nameof(LinkedListNode<>.Value)}"),
            i => i.MatchStloc(out value2Index)
        );

        c.EmitLdloca(tIndex);
        c.EmitLdloca(t2Index);

        c.EmitLdloca(priorPriority);

        c.EmitLdloc(value2Index);

        c.EmitDelegate(
            static (ref RenderTarget2D target, ref RenderTarget2D target2, ref EffectPriority? prior, Filter nextFilter) =>
            {
                if (prior != null && prior == nextFilter.Priority)
                {
                    return;
                }

                prior = nextFilter.Priority;

                var color = Lighting.UpdateEveryFrame ? Color.White : Main.ColorOfTheSkies;

                foreach (var step in filters_by_priority[prior.Value])
                {
                    if (step.Apply(new ScreenFilterRendererContext(target, target2, color)))
                    {
                        Utils.Swap(ref target2, ref target);
                    }
                }
            }
        );
    }
}
