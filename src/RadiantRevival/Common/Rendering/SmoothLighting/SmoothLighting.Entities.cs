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
        GoreRendering();
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
            Scope(() => orig(self), begin: false);
        };

        On_Main.DrawCachedProjs += (orig, self, projCache, startSpriteBatch) =>
        {
            Scope(() => orig(self, projCache, startSpriteBatch), begin: !startSpriteBatch);
        };

        // Niche, but funny
        On_Main.DrawWallOfStars += orig =>
        {
            Scope(() => orig(), begin: false);
        };
    }

    private static void ItemRendering()
    {
        On_Main.DrawItems += (orig, self) =>
        {
            Scope(() => orig(self));
        };
    }

    private static void GoreRendering()
    {
        On_Main.DrawGore += (orig, self) =>
        {
            Scope(() => orig(self));
        };

        On_Main.DrawGoreBehind += (orig, self) =>
        {
            Scope(() => orig(self));
        };
    }

    private static void Scope(Action callback, bool begin = true)
    {
        using var _ = new ScopeStateCapture<bool>(ref Main.gameMenu);
        Main.gameMenu = true;

        if (begin)
        {
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
        else
        {
            using (SmoothLightingRenderer.BeginScope())
            {
                callback();
            }
        }
    }
}
