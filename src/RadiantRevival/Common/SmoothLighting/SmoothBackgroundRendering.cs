using Daybreak.Common.Features.Hooks;
using Microsoft.Xna.Framework;
using Terraria;

namespace RadiantRevival.Common.SmoothLighting;

/// <summary>
///     Rewrites background rendering to use smooth lighting through a shader.
///     <br />
///     Ironically, this is an optimization over the vanilla rendering.
/// </summary>
internal static class SmoothBackgroundRendering
{
    [OnLoad]
    private static void ApplyHooks()
    {
        On_Main.DrawBackground_SurfaceTransitionBackground += SurfaceTransitionBackground;
        On_Main.DrawBackground_DirtBackground += DirtBackground;
        On_Main.DrawBackground_DrawUnderworldBlackBox += DrawUnderworldBlackBox;
        On_Main.DrawBackground_DrawRockLayer += DrawRockLayer;
        On_Main.DrawBackground_DrawMagmaLayer += DrawMagmaLayer;
    }

    private static void DirtBackground(On_Main.orig_DrawBackground_DirtBackground orig, Main self, float localShimmerAlpha, int q1, int q2, ref Vector2 drawOffset, ref Vector3 backgroundColor) { }

    private static void SurfaceTransitionBackground(On_Main.orig_DrawBackground_SurfaceTransitionBackground orig, Main self, float localShimmerAlpha, ref Vector2 drawOffset, ref Vector3 backgroundColor) { }

    private static void DrawUnderworldBlackBox(On_Main.orig_DrawBackground_DrawUnderworldBlackBox orig, Main self, double magmaLayer, Vector2 drawOffset) { }

    private static void DrawRockLayer(On_Main.orig_DrawBackground_DrawRockLayer orig, Main self, float localShimmerAlpha, double magmaLayer, int q1, int q2, ref Vector2 drawOffset, ref Vector3 backgroundColor) { }

    private static void DrawMagmaLayer(On_Main.orig_DrawBackground_DrawMagmaLayer orig, Main self, double magmaLayer, int q1, int q2, ref Vector2 drawOffset, ref Vector3 backgroundColor) { }
}
