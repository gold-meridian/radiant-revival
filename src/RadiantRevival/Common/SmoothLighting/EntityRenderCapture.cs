using System;
using System.Collections.Generic;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Terraria;

namespace RadiantRevival.Common.SmoothLighting;

internal static class EntityRenderCapture
{
    [OnLoad]
    private static void ApplyHooks()
    {
        On_Main.DrawNPCs += DrawNpcs_Scope;
        On_Main.DrawCachedNPCs += DrawCachedNpcs_Scope;
    }

    private static void DrawNpcs_Scope(On_Main.orig_DrawNPCs orig, Main self, bool behindTiles)
    {
        Scope(() => orig(self, behindTiles));
    }

    private static void DrawCachedNpcs_Scope(On_Main.orig_DrawCachedNPCs orig, Main self, List<int> npcCache, bool behindTiles)
    {
        Scope(() => orig(self, npcCache, behindTiles));
    }

    private static void Scope(Action callback)
    {
        using var _ = new ScopeStateCapture<bool>(ref Main.gameMenu);
        Main.gameMenu = true;

        Main.spriteBatch.End(out var ss);
        {
            using (SmoothLightingRenderer.BeginScope())
            {
                Main.spriteBatch.Begin(ss);
                callback();
                Main.spriteBatch.End();
            }
        }
        Main.spriteBatch.Begin(ss);
    }
}
