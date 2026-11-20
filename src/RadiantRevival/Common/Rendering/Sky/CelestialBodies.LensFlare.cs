using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;

namespace RadiantRevival.Common;

public static class SunLensFlare
{
    [OnLoad]
    private static void Load()
    {
        On_Main.DrawLensFlare += DrawLensFlare_Cancel;
        IL_Main.DoDraw += _ => { };
    }

    public static bool ShouldDrawFlare { get; private set; }

    private static void DrawLensFlare_Cancel(On_Main.orig_DrawLensFlare orig)
    {
        if (ShouldDrawFlare)
        {
            orig();
        }
    }

    [ScreenFilter(EffectPriority.Medium)]
    private static bool ApplyShader(
        SpriteBatch sb,
        GraphicsDevice device,
        RenderTarget2D screen,
        RenderTarget2D screenSwap
    )
    {
        ShouldDrawFlare = true;
        {
            Main.DrawLensFlare();
        }
        ShouldDrawFlare = false;

        return false;
    }
}
