using System;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace RadiantRevival.Common.SmoothLighting;

/// <summary>
///     Rewrites background rendering to use smooth lighting through a shader.
///     <br />
///     Ironically, this is an optimization over the vanilla rendering.
/// </summary>
internal static class SmoothBackgroundRendering
{
#region Vanilla rendering replacement
    [OnLoad]
    private static void ApplyHooks()
    {
        On_Main.DrawBackground += (orig, self) =>
        {
            using var _ = SmoothLightingRenderer.BeginScope();

            Main.spriteBatch.End(out var ss);
            Main.spriteBatch.Begin(in ss);
            orig(self);
            Main.spriteBatch.Restart(in ss);
        };

        On_Main.DrawBackground_SurfaceTransitionBackground += SurfaceTransitionBackground;
        On_Main.DrawBackground_DirtBackground += DirtBackground;
        On_Main.DrawBackground_DrawUnderworldBlackBox += DrawUnderworldBlackBox;
        On_Main.DrawBackground_DrawRockLayer += DrawRockLayer;
        On_Main.DrawBackground_DrawMagmaTransition += DrawMagmaTransition;
        On_Main.DrawBackground_DrawMagmaLayer += DrawMagmaLayer;
    }

    private static void SurfaceTransitionBackground(
        On_Main.orig_DrawBackground_SurfaceTransitionBackground orig,
        Main self,
        float localShimmerAlpha,
        ref Vector2 drawOffset,
        ref Vector3 backgroundColor
    )
    {
        var tint = ColorFromBg(backgroundColor, localShimmerAlpha);

        var texNew = TextureAssets.Background[self._drawBackground_backTexture[0]];
        var tileWidth = texNew.Width() - 32;

        self.bgParallax = Main.caveParallax;
        self.bgStartX = CalcStartX(tileWidth, drawOffset);
        self.bgLoops = CalcLoops(tileWidth, drawOffset);
        self.bgTopY = (float)Main.worldSurface * 16f - 16f - Main.screenPosition.Y + 16f;

        var diff = CalcDiff(self.bgStartX);
        DrawStripX(
            texNew.Value,
            self.bgStartX,
            self.bgLoops,
            tileWidth,
            diff,
            self.bgTopY,
            drawOffset,
            tint
        );

        if (Main.ugBackTransition <= 0f)
        {
            return;
        }

        var texOld = TextureAssets.Background[self._drawBackground_oldBackTexture[0]];
        tileWidth = texOld.Width() - 32;

        self.bgStartX = CalcStartX(tileWidth, drawOffset);
        self.bgLoops = CalcLoops(tileWidth, drawOffset);

        diff = CalcDiff(self.bgStartX);
        DrawStripX(
            texOld.Value,
            self.bgStartX,
            self.bgLoops,
            tileWidth,
            diff,
            self.bgTopY,
            drawOffset,
            tint * Main.ugBackTransition
        );
    }

    private static void DirtBackground(On_Main.orig_DrawBackground_DirtBackground orig, Main self, float localShimmerAlpha, int q1, int q2, ref Vector2 drawOffset, ref Vector3 backgroundColor)
    {
        self.bgTopY = (float)Main.worldSurface * 16f - Main.screenPosition.Y + 16f;

        if (!(Main.worldSurface * 16.0 <= Main.screenPosition.Y + Main.screenHeight + Main.offScreenRange))
        {
            return;
        }

        var rockTransitionPoint = self.GetRockTransitionPoint();
        var tint = ColorFromBg(backgroundColor, localShimmerAlpha);

        self.bgParallax = Main.caveParallax;

        var texNew = TextureAssets.Background[self._drawBackground_backTexture[1]];
        var tileWidth = texNew.Width() - 32;

        self.bgStartX = CalcStartX(tileWidth, drawOffset);
        self.bgLoops = CalcLoops(tileWidth, drawOffset);
        CalcStartYAndLoopsY(self, rockTransitionPoint, drawOffset, out var hitRockTransition);

        var diff = CalcDiff(self.bgStartX);
        var bgHeight = Main.backgroundHeight[self._drawBackground_backTexture[1]];

        DrawRegion(
            texNew.Value,
            self.bgStartX,
            self.bgLoops,
            self.bgStartY,
            self.bgLoopsY,
            tileWidth,
            diff,
            bgHeight,
            drawOffset,
            tint
        );

        if (Main.ugBackTransition > 0f)
        {
            var texOld = TextureAssets.Background[self._drawBackground_oldBackTexture[1]];
            var tileWidthOld = texOld.Width() - 32;
            var startXOld = CalcStartX(tileWidthOld, drawOffset);
            var loopsOld = CalcLoops(tileWidthOld, drawOffset);
            var diffOld = CalcDiff(startXOld);
            var bgHeightOld = Main.backgroundHeight[self._drawBackground_oldBackTexture[1]];
            var a = (byte)(255f * Main.ugBackTransition);

            for (var i = 0; i < loopsOld; i++)
            for (var j = 0; j < self.bgLoopsY; j++)
            for (var k = 0; k < tileWidthOld / 16; k++)
            for (var l = 0; l < 6; l++)
            {
                float tileWorldY = self.bgStartY + j * 96 + l * 16 + 8;
                var tileX = (int)((startXOld + tileWidthOld * i + k * 16 + 8 + Main.screenPosition.X) / 16f);
                var tileY = (int)((tileWorldY + Main.screenPosition.Y) / 16f);

                if (!WorldGen.InWorld(tileX, tileY))
                {
                    continue;
                }

                var c = Lighting.GetColor(tileX, tileY);
                if (c is { R: 0, G: 0, B: 0 })
                {
                    continue;
                }

                Lighting.GetCornerColors(tileX, tileY, out var vertices, Main.ugBackTransition);
                vertices.BottomLeftColor.A = a;
                vertices.BottomRightColor.A = a;
                vertices.TopLeftColor.A = a;
                vertices.TopRightColor.A = a;

                Main.tileBatch.Draw(
                    texOld.Value,
                    new Vector2(
                        startXOld + tileWidthOld * i + 16 * k + diffOld,
                        self.bgStartY + bgHeightOld * j + 16 * l
                    ) + drawOffset,
                    new Rectangle(16 * k + diffOld + 16, 16 * l, 16, 16),
                    vertices,
                    Vector2.Zero,
                    1f,
                    SpriteEffects.None
                );
            }
        }

        if (!hitRockTransition)
        {
            return;
        }

        const int strip_width = 128;

        self.bgParallax = Main.caveParallax;
        self.bgStartX = CalcStartX(strip_width, drawOffset);
        self.bgLoops = CalcLoops(strip_width, drawOffset);
        self.bgTopY = rockTransitionPoint - Main.screenPosition.Y - 16f;
        diff = CalcDiff(self.bgStartX);

        if (!(self.bgTopY > -32f))
        {
            return;
        }

        var texStrip = TextureAssets.Background[self._drawBackground_backTexture[2]];
        DrawStripX(
            texStrip.Value,
            self.bgStartX,
            self.bgLoops,
            strip_width,
            diff,
            self.bgTopY,
            drawOffset,
            tint
        );

        if (Main.ugBackTransition > 0f)
        {
            var texStripOld = TextureAssets.Background[self._drawBackground_oldBackTexture[2]];
            var transitionTint = tint * Main.ugBackTransition;
            DrawStripX(
                texStripOld.Value,
                self.bgStartX,
                self.bgLoops,
                strip_width,
                diff,
                self.bgTopY,
                drawOffset,
                transitionTint
            );
        }
    }

    private static void DrawUnderworldBlackBox(On_Main.orig_DrawBackground_DrawUnderworldBlackBox orig, Main self, double magmaLayer, Vector2 drawOffset)
    {
        if (!(magmaLayer * 16.0 <= Main.screenPosition.Y + Main.screenHeight) || (Main.remixWorld && Main.maxTilesX < 5000))
        {
            return;
        }

        var y = 0;
        var x = 0;
        var num = Main.screenHeight + 200;
        var width = Main.screenWidth + 100;
        if (Main.UnderworldLayer * 16f < Main.screenPosition.Y + Main.screenHeight)
        {
            var num2 = (int)(self.hellBlackBoxBottom - Main.screenPosition.Y + drawOffset.Y);
            if (num > num2)
            {
                num = num2;
            }
        }

        Main.spriteBatch.Draw(TextureAssets.BlackTile.Value, new Rectangle(x, y, width, num), new Color(0, 0, 0));
    }

    private static void DrawRockLayer(On_Main.orig_DrawBackground_DrawRockLayer orig, Main self, float localShimmerAlpha, double magmaLayer, int q1, int q2, ref Vector2 drawOffset, ref Vector3 backgroundColor)
    {
        var rockTransitionPoint = self.GetRockTransitionPoint();
        var magmaTransition = rockTransitionPoint <= Main.screenPosition.Y + Main.screenHeight
                           && magmaLayer * 16.0 < Main.screenPosition.Y + Main.screenHeight;

        self.bgTopY = rockTransitionPoint - Main.screenPosition.Y;

        if (!(rockTransitionPoint <= Main.screenPosition.Y + Main.screenHeight))
        {
            return;
        }

        const int tile_width = 128;

        self.bgParallax = Main.caveParallax;
        self.bgStartX = CalcStartX(tile_width, drawOffset);
        self.bgLoops = CalcLoops(tile_width, drawOffset);

        if (!(rockTransitionPoint <= Main.screenPosition.Y + Main.screenHeight))
        {
            return;
        }

        if (rockTransitionPoint + Main.screenHeight < Main.screenPosition.Y - 16f)
        {
            self.bgStartY = (int)(Math.IEEERemainder(self.bgTopY, Main.backgroundHeight[3]) - Main.backgroundHeight[3]);
            self.bgLoopsY = (Main.screenHeight - self.bgStartY + (int)drawOffset.Y * 2) / Main.backgroundHeight[2] + 1;
        }
        else
        {
            self.bgStartY = (int)self.bgTopY;
            self.bgLoopsY = (int)(Main.screenHeight - self.bgTopY + drawOffset.Y * 2f) / Main.backgroundHeight[2] + 1;
        }

        var num2 = magmaLayer * 16.0 + 600.0;
        if (magmaLayer * 16.0 < Main.screenPosition.Y + Main.screenHeight)
        {
            self.bgLoopsY = (int)(num2 - self.bgStartY - Main.screenPosition.Y) / Main.backgroundHeight[2];
        }

        var diff = CalcDiff(self.bgStartX);
        var texNew = TextureAssets.Background[self._drawBackground_backTexture[3]];
        var bgHeight = Main.backgroundHeight[self._drawBackground_backTexture[3]];
        var tint = ColorFromBg(backgroundColor, localShimmerAlpha);

        DrawRegion(
            texNew.Value,
            self.bgStartX,
            self.bgLoops,
            self.bgStartY,
            self.bgLoopsY,
            tile_width,
            diff,
            bgHeight,
            drawOffset,
            tint
        );

        if (Main.ugBackTransition > 0f)
        {
            var texOld = TextureAssets.Background[self._drawBackground_oldBackTexture[3]];
            DrawRegion(
                texOld.Value,
                self.bgStartX,
                self.bgLoops,
                self.bgStartY,
                self.bgLoopsY,
                tile_width,
                diff,
                Main.backgroundHeight[self._drawBackground_oldBackTexture[3]],
                drawOffset,
                tint * Main.ugBackTransition
            );
        }

        var refWidth = tile_width;
        self.DrawBackground_DrawMagmaTransition(
            ref drawOffset,
            magmaTransition,
            ref backgroundColor,
            ref refWidth,
            diff
        );
    }

    private static void DrawMagmaTransition(On_Main.orig_DrawBackground_DrawMagmaTransition orig, Main self, ref Vector2 drawOffset, bool magmaTransition, ref Vector3 backgroundColor, ref int backgroundWidth, int diff)
    {
        self.bgTopY = self.bgStartY + self.bgLoopsY * Main.backgroundHeight[2];

        if (!magmaTransition)
        {
            return;
        }

        var tint = ColorFromBg(backgroundColor, 0f);
        backgroundWidth = 128;

        self.bgParallax = Main.caveParallax;
        self.bgStartX = CalcStartX(backgroundWidth, drawOffset);
        self.bgLoops = CalcLoops(backgroundWidth, drawOffset);

        var texNew = TextureAssets.Background[self._drawBackground_backTexture[4]];
        var frameY = Main.magmaBGFrame * 16;

        DrawStripX(
            texNew.Value,
            self.bgStartX,
            self.bgLoops,
            backgroundWidth,
            diff,
            self.bgTopY,
            drawOffset,
            tint,
            frameY
        );

        if (Main.ugBackTransition > 0f)
        {
            var texOld = TextureAssets.Background[self._drawBackground_oldBackTexture[4]];
            DrawStripX(
                texOld.Value,
                self.bgStartX,
                self.bgLoops,
                backgroundWidth,
                diff,
                self.bgTopY,
                drawOffset,
                tint * Main.ugBackTransition,
                frameY
            );
        }
    }

    private static void DrawMagmaLayer(On_Main.orig_DrawBackground_DrawMagmaLayer orig, Main self, double magmaLayer, int q1, int q2, ref Vector2 drawOffset, ref Vector3 backgroundColor)
    {
        self.bgTopY = (float)magmaLayer * 16f - Main.screenPosition.Y + 16f + 600f - 8f;

        if (!(magmaLayer * 16.0 <= Main.screenPosition.Y + Main.screenHeight))
        {
            return;
        }

        var x = backgroundColor.X;
        var y = backgroundColor.Y;
        var z = backgroundColor.Z;

        const int tile_width = 128;

        self.bgStartX = CalcStartX(tile_width, drawOffset);
        self.bgLoops = CalcLoops(tile_width, drawOffset);

        if (magmaLayer * 16.0 + Main.screenHeight < Main.screenPosition.Y - 16f)
        {
            self.bgStartY = (int)(Math.IEEERemainder(self.bgTopY, Main.backgroundHeight[2]) - Main.backgroundHeight[2]);
            self.bgLoopsY = (Main.screenHeight - self.bgStartY + (int)drawOffset.Y * 2) / Main.backgroundHeight[2] + 1;
        }
        else
        {
            self.bgStartY = (int)self.bgTopY;
            self.bgLoopsY = (int)(Main.screenHeight - self.bgTopY + drawOffset.Y * 2f) / Main.backgroundHeight[2] + 1;
        }

        var flag = false;
        if (Main.UnderworldLayer * 16f < Main.screenPosition.Y + Main.screenHeight)
        {
            self.bgLoopsY = (int)Math.Ceiling((Main.UnderworldLayer * 16f - Main.screenPosition.Y - self.bgStartY) / Main.backgroundHeight[2]);
            flag = true;
        }

        var diff = CalcDiff(self.bgStartX);
        var texNew = TextureAssets.Background[self._drawBackground_backTexture[5]];
        var tint = new Color(x, y, z);
        var frameYOffset = Main.backgroundHeight[2] * Main.magmaBGFrame;

        DrawRegion(
            texNew.Value,
            self.bgStartX,
            self.bgLoops,
            self.bgStartY,
            self.bgLoopsY,
            tile_width,
            diff,
            Main.backgroundHeight[2],
            drawOffset,
            tint,
            frameYOffset
        );

        if (!flag)
        {
            return;
        }

        self.bgParallax = Main.caveParallax;
        self.bgStartX = CalcStartX(tile_width, drawOffset);
        self.bgLoops = CalcLoops(tile_width, drawOffset);
        self.bgTopY = self.bgStartY + self.bgLoopsY * Main.backgroundHeight[2];
        self.hellBlackBoxBottom = self.bgTopY + Main.screenPosition.Y;

        diff = CalcDiff(self.bgStartX);
        var texHell = TextureAssets.Background[self._drawBackground_backTexture[6]];
        var frameY = Main.magmaBGFrame * 16;
        DrawStripX(
            texHell.Value,
            self.bgStartX,
            self.bgLoops,
            tile_width,
            diff,
            self.bgTopY,
            drawOffset,
            tint,
            frameY
        );

        if (Main.ugBackTransition > 0f)
        {
            var texHellOld = TextureAssets.Background[self._drawBackground_oldBackTexture[6]];
            DrawStripX(
                texHellOld.Value,
                self.bgStartX,
                self.bgLoops,
                tile_width,
                diff,
                self.bgTopY,
                drawOffset,
                tint * Main.ugBackTransition,
                frameY
            );
        }
    }
#endregion

    private static void DrawStripX(
        Texture2D texture,
        int bgStartX,
        int bgLoops,
        int tileWidth,
        int diff,
        float bgTopY,
        Vector2 drawOffset,
        Color tint,
        int frameY = 0
    )
    {
        var tilesPerUnit = tileWidth / 16;
        for (var i = 0; i < bgLoops; i++)
        for (var j = 0; j < tilesPerUnit; j++)
        {
            var destX = bgStartX + tileWidth * i + 16 * j + diff;
            var srcX = 16 * j + diff + 16;
            Main.spriteBatch.Draw(
                texture,
                new Vector2(destX, bgTopY) + drawOffset,
                new Rectangle(srcX, frameY, 16, 16),
                tint
            );
        }
    }

    private static void DrawRegion(
        Texture2D texture,
        int bgStartX,
        int bgLoops,
        int bgStartY,
        int bgLoopsY,
        int tileWidth,
        int diff,
        int backgroundHeight,
        Vector2 drawOffset,
        Color tint,
        int frameYOffset = 0
    )
    {
        var tilesPerUnit = tileWidth / 16;
        for (var i = 0; i < bgLoops; i++)
        for (var j = 0; j < bgLoopsY; j++)
        for (var k = 0; k < tilesPerUnit; k++)
        for (var l = 0; l < 6; l++)
        {
            var destX = bgStartX + tileWidth * i + 16 * k + diff;
            var destY = bgStartY + backgroundHeight * j + 16 * l;
            var srcX = 16 * k + diff + 16;
            var srcY = 16 * l + frameYOffset;
            Main.spriteBatch.Draw(
                texture,
                new Vector2(destX, destY) + drawOffset,
                new Rectangle(srcX, srcY, 16, 16),
                tint
            );
        }
    }

    private static Color ColorFromBg(Vector3 bgColor, float shimmerAlpha)
    {
        var c = new Color(bgColor.X, bgColor.Y, bgColor.Z);
        if (shimmerAlpha > 0f)
        {
            c *= 1f - shimmerAlpha;
        }

        return c;
    }

    private static int CalcStartX(int tileWidth, Vector2 drawOffset)
    {
        return (int)(0.0 - Math.IEEERemainder(tileWidth + Main.screenPosition.X * Main.caveParallax, tileWidth) - (int)(tileWidth / 2f)) - (int)drawOffset.X;
    }

    private static int CalcLoops(int tileWidth, Vector2 drawOffset)
    {
        return (Main.screenWidth + (int)drawOffset.X * 2) / tileWidth + 2;
    }

    private static int CalcDiff(int bgStartX)
    {
        var diff = (int)(float)Math.Round(0f - (float)Math.IEEERemainder(bgStartX + Main.screenPosition.X, 16));
        return diff == -8 ? 8 : diff;
    }

    private static void CalcStartYAndLoopsY(
        Main self,
        float rockTransitionPoint,
        Vector2 drawOffset,
        out bool hitRockTransition
    )
    {
        hitRockTransition = false;

        if (Main.worldSurface * 16.0 < Main.screenPosition.Y - 16f)
        {
            self.bgStartY = (int)(Math.IEEERemainder(self.bgTopY, Main.backgroundHeight[2]) - Main.backgroundHeight[2]);
            self.bgLoopsY = (Main.screenHeight - self.bgStartY + (int)drawOffset.Y * 2) / Main.backgroundHeight[2] + 1;
        }
        else
        {
            self.bgStartY = (int)self.bgTopY;
            self.bgLoopsY = (int)(Main.screenHeight - self.bgTopY + drawOffset.Y * 2f) / Main.backgroundHeight[2] + 1;
        }

        if (rockTransitionPoint < Main.Camera.ScaledPosition.Y + Main.screenHeight - 16f)
        {
            self.bgLoopsY = (int)(rockTransitionPoint - Main.screenPosition.Y - self.bgStartY) / Main.backgroundHeight[2];
            hitRockTransition = true;
        }
    }
}
