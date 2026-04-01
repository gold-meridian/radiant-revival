using System;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Terraria;

namespace RadiantRevival.Common.SmoothLighting;

internal static class CommonEntitySmoothLighting
{
    [OnLoad]
    private static void ApplyHooks()
    {
        NpcRendering();
        ProjectileRendering();
        ItemRendering();
    }

    private static void NpcRendering()
    {
        On_Main.DrawNPCs += (orig, self, behindTiles) =>
        {
            Scope(() => orig(self, behindTiles));
        };

        On_Main.DrawCachedNPCs += (orig, self, npcCache, behindTiles) =>
        {
            Scope(() => orig(self, npcCache, behindTiles));
        };

        On_Main.DrawWoF += (orig, self) =>
        {
            Scope(() => orig(self));
        };
    }

    private static void ProjectileRendering()
    {
        On_Main.DrawProjectiles += (orig, self) =>
        {
            Scope(() => orig(self));
        };

        On_Main.DrawCachedProjs += (orig, self, projCache, startSpriteBatch) =>
        {
            Scope(() => orig(self, projCache, startSpriteBatch), endSpriteBatch: !startSpriteBatch);
        };

        // Niche, but funny
        On_Main.DrawWallOfStars += orig =>
        {
            Scope(() => orig(), endSpriteBatch: false);
        };
    }

    private static void ItemRendering()
    {
        On_Main.DrawItems += (orig, self) =>
        {
            Scope(() => orig(self));
        };
    }

    private static void Scope(Action callback, bool endSpriteBatch = true)
    {
        using var _ = new ScopeStateCapture<bool>(ref Main.gameMenu);
        Main.gameMenu = true;

        var ss = default(SpriteBatchSnapshot);
        if (endSpriteBatch)
        {
            Main.spriteBatch.End(out ss);
        }

        using (SmoothLightingRenderer.BeginScope())
        {
            Main.spriteBatch.Begin(ss);
            callback();
            Main.spriteBatch.End();
        }

        if (endSpriteBatch)
        {
            Main.spriteBatch.Begin(ss);
        }
    }
}
