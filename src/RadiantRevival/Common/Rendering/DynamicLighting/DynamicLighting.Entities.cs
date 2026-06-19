using Daybreak.Common.Features.Hooks;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.Utilities;

namespace RadiantRevival.Common;

public static class CommonEntityDynamicLighting
{
    [OnLoad]
    private static void Load()
    {
        On_Projectile.Update += (orig, self, i) =>
        {
            DynamicLighting.Scope(() => orig(self, i));
        };

        On_Player.Update += (orig, self, i) =>
        {
            DynamicLighting.Scope(() => orig(self, i));
        };

        On_TileLightScanner.ApplyTileLight += ApplyTileLight_DynamicLighting;
    }

    private static void ApplyTileLight_DynamicLighting(On_TileLightScanner.orig_ApplyTileLight orig, TileLightScanner self, Tile tile, int x, int y, ref FastRandom localRandom, ref Vector3 lightColor)
    {
        var type = tile.TileType;

        if (TileID.Sets.Torches[type] ||
            TileID.Sets.RoomNeeds.CountsAsTorch[type] ||
            // TileID.Sets.RoomNeeds_Vanilla.CountsAsTorch[type] || Unused?
            TileID.Sets.Campfires[type]
        )
        {
            using (DynamicLighting.BeginScope())
            {
                orig(self, tile, x, y, ref localRandom, ref lightColor);
            }

            return;
        }

        orig(self, tile, x, y, ref localRandom, ref lightColor);
    }
}
