using System;
using Daybreak.Common.Features.Hooks;
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
    [OnLoad]
    private static void ApplyHooks()
    {
        On_Main.DrawBackground_SurfaceTransitionBackground += SurfaceTransitionBackground;
        On_Main.DrawBackground_DirtBackground += DirtBackground;
        On_Main.DrawBackground_DrawUnderworldBlackBox += DrawUnderworldBlackBox;
        On_Main.DrawBackground_DrawRockLayer += DrawRockLayer;
        On_Main.DrawBackground_DrawMagmaTransition += DrawMagmaTransition;
        On_Main.DrawBackground_DrawMagmaLayer += DrawMagmaLayer;
    }

    private static void SurfaceTransitionBackground(On_Main.orig_DrawBackground_SurfaceTransitionBackground orig, Main self, float localShimmerAlpha, ref Vector2 drawOffset, ref Vector3 backgroundColor)
    {
        var num = TextureAssets.Background[self._drawBackground_backTexture[0]].Width() - 32;
        self.bgParallax = Main.caveParallax;
        self.bgStartX = (int)(0.0 - Math.IEEERemainder(num + Main.screenPosition.X * self.bgParallax, num) - num / 2) - (int)drawOffset.X;
        self.bgLoops = (Main.screenWidth + (int)drawOffset.X * 2) / num + 2;
        self.bgTopY = (float)Main.worldSurface * 16f - 16f - Main.screenPosition.Y + 16f;
        var x = backgroundColor.X;
        var y = backgroundColor.Y;
        var z = backgroundColor.Z;
        for (var i = 0; i < self.bgLoops; i++)
        {
            for (var j = 0; j < num / 16; j++)
            {
                var num2 = (int)(float)Math.Round(0f - (float)Math.IEEERemainder(self.bgStartX + Main.screenPosition.X, 16.0));
                if (num2 == -8)
                {
                    num2 = 8;
                }

                float num3 = self.bgStartX + num * i + j * 16 + 8;
                var num4 = self.bgTopY;
                var color = Lighting.GetColor((int)((num3 + Main.screenPosition.X) / 16f), (int)((Main.screenPosition.Y + num4) / 16f));
                color.R = (byte)(color.R * x);
                color.G = (byte)(color.G * y);
                color.B = (byte)(color.B * z);
                if (localShimmerAlpha > 0f)
                {
                    color *= 1f - localShimmerAlpha;
                }

                Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[0]].Value, new Vector2(self.bgStartX + num * i + 16 * j + num2, self.bgTopY) + drawOffset, new Rectangle(16 * j + num2 + 16, 0, 16, 16), color);
            }
        }

        if (!(Main.ugBackTransition > 0f))
        {
            return;
        }

        num = TextureAssets.Background[self._drawBackground_oldBackTexture[0]].Width() - 32;
        self.bgStartX = (int)(0.0 - Math.IEEERemainder(num + Main.screenPosition.X * self.bgParallax, num) - num / 2) - (int)drawOffset.X;
        self.bgLoops = (Main.screenWidth + (int)drawOffset.X * 2) / num + 2;
        for (var k = 0; k < self.bgLoops; k++)
        {
            for (var l = 0; l < num / 16; l++)
            {
                var num5 = (int)(float)Math.Round(0f - (float)Math.IEEERemainder(self.bgStartX + Main.screenPosition.X, 16.0));
                if (num5 == -8)
                {
                    num5 = 8;
                }

                float num6 = self.bgStartX + num * k + l * 16 + 8;
                var num7 = self.bgTopY;
                var color2 = Lighting.GetColor((int)((num6 + Main.screenPosition.X) / 16f), (int)((Main.screenPosition.Y + num7) / 16f));
                color2.R = (byte)(color2.R * x);
                color2.G = (byte)(color2.G * y);
                color2.B = (byte)(color2.B * z);
                var color3 = color2;
                color3.R = (byte)(color3.R * Main.ugBackTransition);
                color3.G = (byte)(color3.G * Main.ugBackTransition);
                color3.B = (byte)(color3.B * Main.ugBackTransition);
                color3.A = (byte)(color3.A * Main.ugBackTransition);
                Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[0]].Value, new Vector2(self.bgStartX + num * k + 16 * l + num5, self.bgTopY) + drawOffset, new Rectangle(16 * l + num5 + 16, 0, 16, 16), color3);
            }
        }
    }

    private static void DirtBackground(On_Main.orig_DrawBackground_DirtBackground orig, Main self, float localShimmerAlpha, int q1, int q2, ref Vector2 drawOffset, ref Vector3 backgroundColor)
    {
        var rockTransitionPoint = self.GetRockTransitionPoint();
        var x = backgroundColor.X;
        var y = backgroundColor.Y;
        var z = backgroundColor.Z;
        self.bgTopY = (float)Main.worldSurface * 16f - Main.screenPosition.Y + 16f;
        var flag = false;
        if (!(Main.worldSurface * 16.0 <= Main.screenPosition.Y + Main.screenHeight + Main.offScreenRange))
        {
            return;
        }

        self.bgParallax = Main.caveParallax;
        var num = TextureAssets.Background[self._drawBackground_backTexture[1]].Width() - 32;
        self.bgStartX = (int)(0.0 - Math.IEEERemainder(num + Main.screenPosition.X * self.bgParallax, num) - num / 2) - (int)drawOffset.X;
        self.bgLoops = (Main.screenWidth + (int)drawOffset.X * 2) / num + 2;
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
            flag = true;
        }

        var num2 = (int)(float)Math.Round(0f - (float)Math.IEEERemainder(self.bgStartX + Main.screenPosition.X, 16.0));
        if (num2 == -8)
        {
            num2 = 8;
        }

        for (var i = 0; i < self.bgLoops; i++)
        {
            for (var j = 0; j < self.bgLoopsY; j++)
            {
                for (var k = 0; k < num / 16; k++)
                {
                    for (var l = 0; l < 6; l++)
                    {
                        float num3 = self.bgStartY + j * 96 + l * 16 + 8;
                        var num4 = (int)((self.bgStartX + num * i + k * 16 + 8 + Main.screenPosition.X) / 16f);
                        var num5 = (int)((num3 + Main.screenPosition.Y) / 16f);
                        var color = Lighting.GetColor(num4, num5);
                        if (!WorldGen.InWorld(num4, num5))
                        {
                            continue;
                        }

                        if (localShimmerAlpha == 0f && (color.R > 0 || color.G > 0 || color.B > 0))
                        {
                            if (!Main.drawToScreen)
                            {
                                Lighting.GetCornerColors(num4, num5, out var vertices);
                                vertices.BottomLeftColor = new Color(vertices.BottomLeftColor.ToVector3() * backgroundColor);
                                vertices.BottomRightColor = new Color(vertices.BottomRightColor.ToVector3() * backgroundColor);
                                Main.tileBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[1]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num2, self.bgStartY + Main.backgroundHeight[self._drawBackground_backTexture[1]] * j + 16 * l) + drawOffset, new Rectangle(16 * k + num2 + 16, 16 * l, 16, 16), vertices, Vector2.Zero, 1f, SpriteEffects.None);
                            }
                            else if ((color.R > q1 || color.G > q1 * 1.1 || color.B > q1 * 1.2) && !Main.tile[num4, num5].active() && Main.WallLightAt(num4, num5) && Main.ugBackTransition == 0f)
                            {
                                Lighting.GetColor9Slice(num4, num5, ref self._drawbackground_colorSlices);
                                try
                                {
                                    for (var m = 0; m < 9; m++)
                                    {
                                        var num6 = 0;
                                        var num7 = 0;
                                        var width = 4;
                                        var height = 4;
                                        var color2 = color;
                                        var color3 = color;
                                        switch (m)
                                        {
                                            case 0:
                                                if (!Main.tile[num4 - 1, num5 - 1].active())
                                                {
                                                    color3 = self._drawbackground_colorSlices[m];
                                                }

                                                break;

                                            case 1:
                                                width = 8;
                                                num6 = 4;
                                                if (!Main.tile[num4, num5 - 1].active())
                                                {
                                                    color3 = self._drawbackground_colorSlices[m];
                                                }

                                                break;

                                            case 2:
                                                num6 = 12;
                                                if (!Main.tile[num4 + 1, num5 - 1].active())
                                                {
                                                    color3 = self._drawbackground_colorSlices[m];
                                                }

                                                break;

                                            case 3:
                                                height = 8;
                                                num7 = 4;
                                                if (!Main.tile[num4 - 1, num5].active())
                                                {
                                                    color3 = self._drawbackground_colorSlices[m];
                                                }

                                                break;

                                            case 4:
                                                width = 8;
                                                height = 8;
                                                num6 = 4;
                                                num7 = 4;
                                                break;

                                            case 5:
                                                num6 = 12;
                                                num7 = 4;
                                                height = 8;
                                                if (!Main.tile[num4 + 1, num5].active())
                                                {
                                                    color3 = self._drawbackground_colorSlices[m];
                                                }

                                                break;

                                            case 6:
                                                num7 = 12;
                                                if (!Main.tile[num4 - 1, num5 + 1].active())
                                                {
                                                    color3 = self._drawbackground_colorSlices[m];
                                                }

                                                break;

                                            case 7:
                                                width = 8;
                                                height = 4;
                                                num6 = 4;
                                                num7 = 12;
                                                if (!Main.tile[num4, num5 + 1].active())
                                                {
                                                    color3 = self._drawbackground_colorSlices[m];
                                                }

                                                break;

                                            case 8:
                                                num6 = 12;
                                                num7 = 12;
                                                if (!Main.tile[num4 + 1, num5 + 1].active())
                                                {
                                                    color3 = self._drawbackground_colorSlices[m];
                                                }

                                                break;
                                        }

                                        color2.R = (byte)((color.R + color3.R) / 2);
                                        color2.G = (byte)((color.G + color3.G) / 2);
                                        color2.B = (byte)((color.B + color3.B) / 2);
                                        color2.R = (byte)(color2.R * x);
                                        color2.G = (byte)(color2.G * y);
                                        color2.B = (byte)(color2.B * z);
                                        Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[1]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num6 + num2, self.bgStartY + Main.backgroundHeight[self._drawBackground_backTexture[1]] * j + 16 * l + num7) + drawOffset, new Rectangle(16 * k + num6 + num2 + 16, 16 * l + num7, width, height), color2);
                                        if (Main.ugBackTransition > 0f)
                                        {
                                            var color4 = color2;
                                            color4.R = (byte)(color4.R * Main.ugBackTransition);
                                            color4.G = (byte)(color4.G * Main.ugBackTransition);
                                            color4.B = (byte)(color4.B * Main.ugBackTransition);
                                            color4.A = (byte)(color4.A * Main.ugBackTransition);
                                            Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[1]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num6 + num2, self.bgStartY + Main.backgroundHeight[self._drawBackground_oldBackTexture[1]] * j + 16 * l + num7) + drawOffset, new Rectangle(16 * k + num6 + num2 + 16, 16 * l + num7, width, height), color4);
                                        }
                                    }
                                }
                                catch
                                {
                                    color.R = (byte)(color.R * x);
                                    color.G = (byte)(color.G * y);
                                    color.B = (byte)(color.B * z);
                                    Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[1]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num2, self.bgStartY + Main.backgroundHeight[self._drawBackground_backTexture[1]] * j + 16 * l) + drawOffset, new Rectangle(16 * k + num2 + 16, 16 * l, 16, 16), color);
                                }
                            }
                            else if ((color.R > q2 || color.G > q2 * 1.1 || color.B > q2 * 1.2) && Main.ugBackTransition == 0f)
                            {
                                Lighting.GetColor4Slice(num4, num5, ref self._drawbackground_colorSlices);
                                for (var n = 0; n < 4; n++)
                                {
                                    var num8 = 0;
                                    var num9 = 0;
                                    var color5 = color;
                                    var color6 = self._drawbackground_colorSlices[n];
                                    switch (n)
                                    {
                                        case 1:
                                            num8 = 8;
                                            break;

                                        case 2:
                                            num9 = 8;
                                            break;

                                        case 3:
                                            num8 = 8;
                                            num9 = 8;
                                            break;
                                    }

                                    color5.R = (byte)((color.R + color6.R) / 2);
                                    color5.G = (byte)((color.G + color6.G) / 2);
                                    color5.B = (byte)((color.B + color6.B) / 2);
                                    color5.R = (byte)(color5.R * x);
                                    color5.G = (byte)(color5.G * y);
                                    color5.B = (byte)(color5.B * z);
                                    Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[1]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num8 + num2, self.bgStartY + Main.backgroundHeight[self._drawBackground_backTexture[1]] * j + 16 * l + num9) + drawOffset, new Rectangle(16 * k + num8 + num2 + 16, 16 * l + num9, 8, 8), color5);
                                    if (Main.ugBackTransition > 0f)
                                    {
                                        var color7 = color5;
                                        color7.R = (byte)(color7.R * Main.ugBackTransition);
                                        color7.G = (byte)(color7.G * Main.ugBackTransition);
                                        color7.B = (byte)(color7.B * Main.ugBackTransition);
                                        color7.A = (byte)(color7.A * Main.ugBackTransition);
                                        Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[1]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num8 + num2, self.bgStartY + Main.backgroundHeight[self._drawBackground_oldBackTexture[1]] * j + 16 * l + num9) + drawOffset, new Rectangle(16 * k + num8 + num2 + 16, 16 * l + num9, 8, 8), color7);
                                    }
                                }
                            }
                            else
                            {
                                color.R = (byte)(color.R * x);
                                color.G = (byte)(color.G * y);
                                color.B = (byte)(color.B * z);
                                Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[1]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num2, self.bgStartY + Main.backgroundHeight[self._drawBackground_backTexture[1]] * j + 16 * l) + drawOffset, new Rectangle(16 * k + num2 + 16, 16 * l, 16, 16), color);
                                if (Main.ugBackTransition > 0f)
                                {
                                    var color8 = color;
                                    color8.R = (byte)(color8.R * Main.ugBackTransition);
                                    color8.G = (byte)(color8.G * Main.ugBackTransition);
                                    color8.B = (byte)(color8.B * Main.ugBackTransition);
                                    color8.A = (byte)(color8.A * Main.ugBackTransition);
                                    Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[1]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num2, self.bgStartY + Main.backgroundHeight[self._drawBackground_oldBackTexture[1]] * j + 16 * l) + drawOffset, new Rectangle(16 * k + num2 + 16, 16 * l, 16, 16), color8);
                                }
                            }
                        }
                        else
                        {
                            color.R = (byte)(color.R * x);
                            color.G = (byte)(color.G * y);
                            color.B = (byte)(color.B * z);
                            if (localShimmerAlpha > 0f)
                            {
                                color *= 1f - localShimmerAlpha;
                            }

                            Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[1]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num2, self.bgStartY + Main.backgroundHeight[self._drawBackground_backTexture[1]] * j + 16 * l) + drawOffset, new Rectangle(16 * k + num2 + 16, 16 * l, 16, 16), color);
                        }
                    }
                }
            }
        }

        if (Main.ugBackTransition > 0f)
        {
            num = TextureAssets.Background[self._drawBackground_oldBackTexture[1]].Width() - 32;
            self.bgStartX = (int)(0.0 - Math.IEEERemainder(num + Main.screenPosition.X * self.bgParallax, num) - num / 2) - (int)drawOffset.X;
            self.bgLoops = (Main.screenWidth + (int)drawOffset.X * 2) / num + 2;
            num2 = (int)(float)Math.Round(0f - (float)Math.IEEERemainder(self.bgStartX + Main.screenPosition.X, 16.0));
            if (num2 == -8)
            {
                num2 = 8;
            }

            for (var num10 = 0; num10 < self.bgLoops; num10++)
            {
                for (var num11 = 0; num11 < self.bgLoopsY; num11++)
                {
                    for (var num12 = 0; num12 < num / 16; num12++)
                    {
                        for (var num13 = 0; num13 < 6; num13++)
                        {
                            float num14 = self.bgStartY + num11 * 96 + num13 * 16 + 8;
                            var num15 = (int)((self.bgStartX + num * num10 + num12 * 16 + 8 + Main.screenPosition.X) / 16f);
                            var num16 = (int)((num14 + Main.screenPosition.Y) / 16f);
                            if (WorldGen.InWorld(num15, num16))
                            {
                                var color9 = Lighting.GetColor(num15, num16);
                                if (color9.R > 0 || color9.G > 0 || color9.B > 0)
                                {
                                    Lighting.GetCornerColors(num15, num16, out var vertices2, Main.ugBackTransition);
                                    var a = (byte)(255f * Main.ugBackTransition);
                                    vertices2.BottomLeftColor.A = a;
                                    vertices2.BottomRightColor.A = a;
                                    vertices2.TopLeftColor.A = a;
                                    vertices2.TopRightColor.A = a;
                                    Main.tileBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[1]].Value, new Vector2(self.bgStartX + num * num10 + 16 * num12 + num2, self.bgStartY + Main.backgroundHeight[self._drawBackground_oldBackTexture[1]] * num11 + 16 * num13) + drawOffset, new Rectangle(16 * num12 + num2 + 16, 16 * num13, 16, 16), vertices2, Vector2.Zero, 1f, SpriteEffects.None);
                                }
                            }
                        }
                    }
                }
            }
        }

        num = 128;
        if (!flag)
        {
            return;
        }

        self.bgParallax = Main.caveParallax;
        self.bgStartX = (int)(0.0 - Math.IEEERemainder(num + Main.screenPosition.X * self.bgParallax, num) - num / 2) - (int)drawOffset.X;
        self.bgLoops = (Main.screenWidth + (int)drawOffset.X * 2) / num + 2;
        self.bgTopY = rockTransitionPoint - Main.screenPosition.Y - 16f;
        if (!(self.bgTopY > -32f))
        {
            return;
        }

        for (var num17 = 0; num17 < self.bgLoops; num17++)
        {
            for (var num18 = 0; num18 < num / 16; num18++)
            {
                float num19 = self.bgStartX + num * num17 + num18 * 16 + 8;
                var num20 = self.bgTopY;
                var color10 = Lighting.GetColor((int)((num19 + Main.screenPosition.X) / 16f), (int)((Main.screenPosition.Y + num20) / 16f));
                color10.R = (byte)(color10.R * x);
                color10.G = (byte)(color10.G * y);
                color10.B = (byte)(color10.B * z);
                if (localShimmerAlpha > 0f)
                {
                    color10 *= 1f - localShimmerAlpha;
                }

                Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[2]].Value, new Vector2(self.bgStartX + num * num17 + 16 * num18 + num2, self.bgTopY) + drawOffset, new Rectangle(16 * num18 + num2 + 16, 0, 16, 16), color10);
                if (Main.ugBackTransition > 0f)
                {
                    var color11 = color10;
                    color11.R = (byte)(color11.R * Main.ugBackTransition);
                    color11.G = (byte)(color11.G * Main.ugBackTransition);
                    color11.B = (byte)(color11.B * Main.ugBackTransition);
                    color11.A = (byte)(color11.A * Main.ugBackTransition);
                    Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[2]].Value, new Vector2(self.bgStartX + num * num17 + 16 * num18 + num2, self.bgTopY) + drawOffset, new Rectangle(16 * num18 + num2 + 16, 0, 16, 16), color11);
                }
            }
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
        var magmaTransition = rockTransitionPoint <= Main.screenPosition.Y + Main.screenHeight && magmaLayer * 16.0 < Main.screenPosition.Y + Main.screenHeight;
        var num = rockTransitionPoint;
        var num2 = magmaLayer * 16.0 + 600.0;
        self.bgTopY = num - Main.screenPosition.Y;
        var x = backgroundColor.X;
        var y = backgroundColor.Y;
        var z = backgroundColor.Z;
        var num3 = 128;
        if (!(rockTransitionPoint <= Main.screenPosition.Y + Main.screenHeight))
        {
            return;
        }

        self.bgParallax = Main.caveParallax;
        self.bgStartX = (int)(0.0 - Math.IEEERemainder(num3 + Main.screenPosition.X * self.bgParallax, num3) - num3 / 2) - (int)drawOffset.X;
        self.bgLoops = (Main.screenWidth + (int)drawOffset.X * 2) / num3 + 2;
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

        if (magmaLayer * 16.0 < Main.screenPosition.Y + Main.screenHeight)
        {
            self.bgLoopsY = (int)(num2 - self.bgStartY - Main.screenPosition.Y) / Main.backgroundHeight[2];
        }

        var num4 = (int)(float)Math.Round(0f - (float)Math.IEEERemainder(self.bgStartX + Main.screenPosition.X, 16.0));
        if (num4 == -8)
        {
            num4 = 8;
        }

        for (var i = 0; i < self.bgLoops; i++)
        {
            for (var j = 0; j < self.bgLoopsY; j++)
            {
                for (var k = 0; k < num3 / 16; k++)
                {
                    for (var l = 0; l < 6; l++)
                    {
                        float num5 = self.bgStartY + j * 96 + l * 16 + 8;
                        var num6 = (int)((self.bgStartX + num3 * i + k * 16 + 8 + Main.screenPosition.X) / 16f);
                        var num7 = (int)((num5 + Main.screenPosition.Y) / 16f);
                        if (!WorldGen.InWorld(num6, num7, 1))
                        {
                            continue;
                        }

                        var color = Lighting.GetColor(num6, num7);
                        if ((!Main.ShouldDrawBackgroundTileAt(num6, num7) && color.R != 0 && color.G != 0 && color.B != 0) || color is { R: <= 0, G: <= 0, B: <= 0 } || (!Main.WallLightAt(num6, num7) && Main.caveParallax == 0f))
                        {
                            continue;
                        }

                        if (localShimmerAlpha == 0f && Lighting.NotRetro && color is { R: < 230, G: < 230, B: < 230 } && Main.ugBackTransition == 0f)
                        {
                            if ((color.R > q1 || color.G > q1 * 1.1 || color.B > q1 * 1.2) && !Main.tile[num6, num7].active())
                            {
                                Lighting.GetColor9Slice(num6, num7, ref self._drawbackground_colorSlices);
                                for (var m = 0; m < 9; m++)
                                {
                                    var num8 = 0;
                                    var num9 = 0;
                                    var width = 4;
                                    var height = 4;
                                    var color2 = color;
                                    var color3 = color;
                                    switch (m)
                                    {
                                        case 0:
                                            if (!Main.tile[num6 - 1, num7 - 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 1:
                                            width = 8;
                                            num8 = 4;
                                            if (!Main.tile[num6, num7 - 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 2:
                                            num8 = 12;
                                            if (!Main.tile[num6 + 1, num7 - 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 3:
                                            height = 8;
                                            num9 = 4;
                                            if (!Main.tile[num6 - 1, num7].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 4:
                                            width = 8;
                                            height = 8;
                                            num8 = 4;
                                            num9 = 4;
                                            break;

                                        case 5:
                                            num8 = 12;
                                            num9 = 4;
                                            height = 8;
                                            if (!Main.tile[num6 + 1, num7].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 6:
                                            num9 = 12;
                                            if (!Main.tile[num6 - 1, num7 + 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 7:
                                            width = 8;
                                            height = 4;
                                            num8 = 4;
                                            num9 = 12;
                                            if (!Main.tile[num6, num7 + 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 8:
                                            num8 = 12;
                                            num9 = 12;
                                            if (!Main.tile[num6 + 1, num7 + 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;
                                    }

                                    color2.R = (byte)((color.R + color3.R) / 2);
                                    color2.G = (byte)((color.G + color3.G) / 2);
                                    color2.B = (byte)((color.B + color3.B) / 2);
                                    color2.R = (byte)(color2.R * x);
                                    color2.G = (byte)(color2.G * y);
                                    color2.B = (byte)(color2.B * z);
                                    Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[3]].Value, new Vector2(self.bgStartX + num3 * i + 16 * k + num8 + num4, self.bgStartY + Main.backgroundHeight[self._drawBackground_backTexture[3]] * j + 16 * l + num9) + drawOffset, new Rectangle(16 * k + num8 + num4 + 16, 16 * l + num9, width, height), color2);
                                    if (Main.ugBackTransition > 0f)
                                    {
                                        var color4 = color2;
                                        color4.R = (byte)(color4.R * Main.ugBackTransition);
                                        color4.G = (byte)(color4.G * Main.ugBackTransition);
                                        color4.B = (byte)(color4.B * Main.ugBackTransition);
                                        color4.A = (byte)(color4.A * Main.ugBackTransition);
                                        Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[3]].Value, new Vector2(self.bgStartX + num3 * i + 16 * k + num8 + num4, self.bgStartY + Main.backgroundHeight[self._drawBackground_oldBackTexture[3]] * j + 16 * l + num9) + drawOffset, new Rectangle(16 * k + num8 + num4 + 16, 16 * l + num9, width, height), color4);
                                    }
                                }
                            }
                            else if (color.R > q2 || color.G > q2 * 1.1 || color.B > q2 * 1.2)
                            {
                                Lighting.GetColor4Slice(num6, num7, ref self._drawbackground_colorSlices);
                                for (var n = 0; n < 4; n++)
                                {
                                    var num10 = 0;
                                    var num11 = 0;
                                    var color5 = color;
                                    var color6 = self._drawbackground_colorSlices[n];
                                    switch (n)
                                    {
                                        case 1:
                                            num10 = 8;
                                            break;

                                        case 2:
                                            num11 = 8;
                                            break;

                                        case 3:
                                            num10 = 8;
                                            num11 = 8;
                                            break;
                                    }

                                    color5.R = (byte)((color.R + color6.R) / 2);
                                    color5.G = (byte)((color.G + color6.G) / 2);
                                    color5.B = (byte)((color.B + color6.B) / 2);
                                    color5.R = (byte)(color5.R * x);
                                    color5.G = (byte)(color5.G * y);
                                    color5.B = (byte)(color5.B * z);
                                    Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[3]].Value, new Vector2(self.bgStartX + num3 * i + 16 * k + num10 + num4, self.bgStartY + Main.backgroundHeight[self._drawBackground_backTexture[3]] * j + 16 * l + num11) + drawOffset, new Rectangle(16 * k + num10 + num4 + 16, 16 * l + num11, 8, 8), color5);
                                    if (Main.ugBackTransition > 0f)
                                    {
                                        var color7 = color5;
                                        color7.R = (byte)(color7.R * Main.ugBackTransition);
                                        color7.G = (byte)(color7.G * Main.ugBackTransition);
                                        color7.B = (byte)(color7.B * Main.ugBackTransition);
                                        color7.A = (byte)(color7.A * Main.ugBackTransition);
                                        Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[3]].Value, new Vector2(self.bgStartX + num3 * i + 16 * k + num10 + num4, self.bgStartY + Main.backgroundHeight[self._drawBackground_oldBackTexture[3]] * j + 16 * l + num11) + drawOffset, new Rectangle(16 * k + num10 + num4 + 16, 16 * l + num11, 8, 8), color7);
                                    }
                                }
                            }
                            else
                            {
                                color.R = (byte)(color.R * x);
                                color.G = (byte)(color.G * y);
                                color.B = (byte)(color.B * z);
                                Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[3]].Value, new Vector2(self.bgStartX + num3 * i + 16 * k + num4, self.bgStartY + Main.backgroundHeight[self._drawBackground_backTexture[3]] * j + 16 * l) + drawOffset, new Rectangle(16 * k + num4 + 16, 16 * l, 16, 16), color);
                                if (Main.ugBackTransition > 0f)
                                {
                                    var color8 = color;
                                    color8.R = (byte)(color8.R * Main.ugBackTransition);
                                    color8.G = (byte)(color8.G * Main.ugBackTransition);
                                    color8.B = (byte)(color8.B * Main.ugBackTransition);
                                    color8.A = (byte)(color8.A * Main.ugBackTransition);
                                    Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[3]].Value, new Vector2(self.bgStartX + num3 * i + 16 * k + num4, self.bgStartY + Main.backgroundHeight[self._drawBackground_oldBackTexture[3]] * j + 16 * l) + drawOffset, new Rectangle(16 * k + num4 + 16, 16 * l, 16, 16), color8);
                                }
                            }
                        }
                        else
                        {
                            color.R = (byte)(color.R * x);
                            color.G = (byte)(color.G * y);
                            color.B = (byte)(color.B * z);
                            if (localShimmerAlpha > 0f)
                            {
                                color *= 1f - localShimmerAlpha;
                            }

                            Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[3]].Value, new Vector2(self.bgStartX + num3 * i + 16 * k + num4, self.bgStartY + Main.backgroundHeight[self._drawBackground_backTexture[3]] * j + 16 * l) + drawOffset, new Rectangle(16 * k + num4 + 16, 16 * l, 16, 16), color);
                            if (Main.ugBackTransition > 0f)
                            {
                                var color9 = color;
                                color9.R = (byte)(color9.R * Main.ugBackTransition);
                                color9.G = (byte)(color9.G * Main.ugBackTransition);
                                color9.B = (byte)(color9.B * Main.ugBackTransition);
                                color9.A = (byte)(color9.A * Main.ugBackTransition);
                                Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[3]].Value, new Vector2(self.bgStartX + num3 * i + 16 * k + num4, self.bgStartY + Main.backgroundHeight[self._drawBackground_oldBackTexture[3]] * j + 16 * l) + drawOffset, new Rectangle(16 * k + num4 + 16, 16 * l, 16, 16), color9);
                            }
                        }
                    }
                }
            }
        }

        self.DrawBackground_DrawMagmaTransition(ref drawOffset, magmaTransition, ref backgroundColor, ref num3, num4);
    }

    private static void DrawMagmaTransition(On_Main.orig_DrawBackground_DrawMagmaTransition orig, Main self, ref Vector2 drawOffset, bool magmaTransition, ref Vector3 backgroundColor, ref int backgroundWidth, int diff)
    {
        var x = backgroundColor.X;
        var y = backgroundColor.Y;
        var z = backgroundColor.Z;
        backgroundWidth = 128;
        if (!magmaTransition)
        {
            return;
        }

        self.bgParallax = Main.caveParallax;
        self.bgStartX = (int)(0.0 - Math.IEEERemainder(backgroundWidth + Main.screenPosition.X * self.bgParallax, backgroundWidth) - backgroundWidth / 2) - (int)drawOffset.X;
        self.bgLoops = (Main.screenWidth + (int)drawOffset.X * 2) / backgroundWidth + 2;
        self.bgTopY = self.bgStartY + self.bgLoopsY * Main.backgroundHeight[2];
        for (var i = 0; i < self.bgLoops; i++)
        {
            for (var j = 0; j < backgroundWidth / 16; j++)
            {
                float num = self.bgStartX + backgroundWidth * i + j * 16 + 8;
                var num2 = self.bgTopY;
                var color = Lighting.GetColor((int)((num + Main.screenPosition.X) / 16f), (int)((Main.screenPosition.Y + num2) / 16f));
                color.R = (byte)(color.R * x);
                color.G = (byte)(color.G * y);
                color.B = (byte)(color.B * z);
                Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[4]].Value, new Vector2(self.bgStartX + backgroundWidth * i + 16 * j + diff, self.bgTopY) + drawOffset, new Rectangle(16 * j + diff + 16, Main.magmaBGFrame * 16, 16, 16), color);
                if (Main.ugBackTransition > 0f)
                {
                    var color2 = color;
                    color2.R = (byte)(color2.R * Main.ugBackTransition);
                    color2.G = (byte)(color2.G * Main.ugBackTransition);
                    color2.B = (byte)(color2.B * Main.ugBackTransition);
                    color2.A = (byte)(color2.A * Main.ugBackTransition);
                    Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[4]].Value, new Vector2(self.bgStartX + backgroundWidth * i + 16 * j + diff, self.bgTopY) + drawOffset, new Rectangle(16 * j + diff + 16, Main.magmaBGFrame * 16, 16, 16), color2);
                }
            }
        }
    }

    private static void DrawMagmaLayer(On_Main.orig_DrawBackground_DrawMagmaLayer orig, Main self, double magmaLayer, int q1, int q2, ref Vector2 drawOffset, ref Vector3 backgroundColor)
    {
        var x = backgroundColor.X;
        var y = backgroundColor.Y;
        var z = backgroundColor.Z;
        self.bgTopY = (float)magmaLayer * 16f - Main.screenPosition.Y + 16f + 600f - 8f;
        var flag = false;
        var num = 128;
        if (!(magmaLayer * 16.0 <= Main.screenPosition.Y + Main.screenHeight))
        {
            return;
        }

        self.bgStartX = (int)(0.0 - Math.IEEERemainder(num + Main.screenPosition.X * self.bgParallax, num) - num / 2) - (int)drawOffset.X;
        self.bgLoops = (Main.screenWidth + (int)drawOffset.X * 2) / num + 2;
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

        if (Main.UnderworldLayer * 16f < Main.screenPosition.Y + Main.screenHeight)
        {
            self.bgLoopsY = (int)Math.Ceiling((Main.UnderworldLayer * 16f - Main.screenPosition.Y - self.bgStartY) / Main.backgroundHeight[2]);
            flag = true;
        }

        q1 = (int)(q1 * 1.5);
        q2 = (int)(q2 * 1.5);
        var num2 = (int)(float)Math.Round(0f - (float)Math.IEEERemainder(self.bgStartX + Main.screenPosition.X, 16.0));
        if (num2 == -8)
        {
            num2 = 8;
        }

        for (var i = 0; i < self.bgLoops; i++)
        {
            for (var j = 0; j < self.bgLoopsY; j++)
            {
                for (var k = 0; k < num / 16; k++)
                {
                    for (var l = 0; l < 6; l++)
                    {
                        float num3 = self.bgStartY + j * 96 + l * 16 + 8;
                        var num4 = (int)((self.bgStartX + num * i + k * 16 + 8 + Main.screenPosition.X) / 16f);
                        var num5 = (int)((num3 + Main.screenPosition.Y) / 16f);
                        if (!WorldGen.InWorld(num4, num5, 1))
                        {
                            continue;
                        }

                        var color = Lighting.GetColor(num4, num5);
                        if ((!Main.ShouldDrawBackgroundTileAt(num4, num5) && color.R != 0 && color.G != 0 && color.B != 0) || (color is { R: <= 0, G: <= 0, B: <= 0 } && num5 <= Main.maxTilesY - 300) || (!Main.WallLightAt(num4, num5) && Main.caveParallax == 0f))
                        {
                            continue;
                        }

                        if (Lighting.NotRetro && color is { R: < 230, G: < 230, B: < 230 })
                        {
                            if ((color.R > q1 || color.G > q1 * 1.1 || color.B > q1 * 1.2) && !Main.tile[num4, num5].active())
                            {
                                Lighting.GetColor9Slice(num4, num5, ref self._drawbackground_colorSlices);
                                for (var m = 0; m < 9; m++)
                                {
                                    var num6 = 0;
                                    var num7 = 0;
                                    var width = 4;
                                    var height = 4;
                                    var color2 = color;
                                    var color3 = color;
                                    switch (m)
                                    {
                                        case 0:
                                            if (!Main.tile[num4 - 1, num5 - 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 1:
                                            width = 8;
                                            num6 = 4;
                                            if (!Main.tile[num4, num5 - 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 2:
                                            num6 = 12;
                                            if (!Main.tile[num4 + 1, num5 - 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 3:
                                            height = 8;
                                            num7 = 4;
                                            if (!Main.tile[num4 - 1, num5].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 4:
                                            width = 8;
                                            height = 8;
                                            num6 = 4;
                                            num7 = 4;
                                            break;

                                        case 5:
                                            num6 = 12;
                                            num7 = 4;
                                            height = 8;
                                            if (!Main.tile[num4 + 1, num5].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 6:
                                            num7 = 12;
                                            if (!Main.tile[num4 - 1, num5 + 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 7:
                                            width = 8;
                                            height = 4;
                                            num6 = 4;
                                            num7 = 12;
                                            if (!Main.tile[num4, num5 + 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;

                                        case 8:
                                            num6 = 12;
                                            num7 = 12;
                                            if (!Main.tile[num4 + 1, num5 + 1].active())
                                            {
                                                color3 = self._drawbackground_colorSlices[m];
                                            }

                                            break;
                                    }

                                    color2.R = (byte)((color.R + color3.R) / 2);
                                    color2.G = (byte)((color.G + color3.G) / 2);
                                    color2.B = (byte)((color.B + color3.B) / 2);
                                    color2.R = (byte)(color2.R * x);
                                    color2.G = (byte)(color2.G * y);
                                    color2.B = (byte)(color2.B * z);
                                    Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[5]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num6 + num2, self.bgStartY + Main.backgroundHeight[2] * j + 16 * l + num7) + drawOffset, new Rectangle(16 * k + num6 + num2 + 16, 16 * l + Main.backgroundHeight[2] * Main.magmaBGFrame + num7, width, height), color2, 0f, default(Vector2), 1f, SpriteEffects.None, 0f);
                                }
                            }
                            else if (color.R > q2 || color.G > q2 * 1.1 || color.B > q2 * 1.2)
                            {
                                Lighting.GetColor4Slice(num4, num5, ref self._drawbackground_colorSlices);
                                for (var n = 0; n < 4; n++)
                                {
                                    var num8 = 0;
                                    var num9 = 0;
                                    var color4 = color;
                                    var color5 = self._drawbackground_colorSlices[n];
                                    switch (n)
                                    {
                                        case 1:
                                            num8 = 8;
                                            break;

                                        case 2:
                                            num9 = 8;
                                            break;

                                        case 3:
                                            num8 = 8;
                                            num9 = 8;
                                            break;
                                    }

                                    color4.R = (byte)((color.R + color5.R) / 2);
                                    color4.G = (byte)((color.G + color5.G) / 2);
                                    color4.B = (byte)((color.B + color5.B) / 2);
                                    color4.R = (byte)(color4.R * x);
                                    color4.G = (byte)(color4.G * y);
                                    color4.B = (byte)(color4.B * z);
                                    Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[5]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num8 + num2, self.bgStartY + Main.backgroundHeight[2] * j + 16 * l + num9) + drawOffset, new Rectangle(16 * k + num8 + num2 + 16, 16 * l + Main.backgroundHeight[2] * Main.magmaBGFrame + num9, 8, 8), color4, 0f, default(Vector2), 1f, SpriteEffects.None, 0f);
                                }
                            }
                            else
                            {
                                color.R = (byte)(color.R * x);
                                color.G = (byte)(color.G * y);
                                color.B = (byte)(color.B * z);
                                Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[5]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num2, self.bgStartY + Main.backgroundHeight[2] * j + 16 * l) + drawOffset, new Rectangle(16 * k + num2 + 16, 16 * l + Main.backgroundHeight[2] * Main.magmaBGFrame, 16, 16), color, 0f, default(Vector2), 1f, SpriteEffects.None, 0f);
                            }
                        }
                        else
                        {
                            color.R = (byte)(color.R * x);
                            color.G = (byte)(color.G * y);
                            color.B = (byte)(color.B * z);
                            Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[5]].Value, new Vector2(self.bgStartX + num * i + 16 * k + num2, self.bgStartY + Main.backgroundHeight[2] * j + 16 * l) + drawOffset, new Rectangle(16 * k + num2 + 16, 16 * l + Main.backgroundHeight[2] * Main.magmaBGFrame, 16, 16), color, 0f, default(Vector2), 1f, SpriteEffects.None, 0f);
                        }
                    }
                }
            }
        }

        if (!flag)
        {
            return;
        }

        self.bgParallax = Main.caveParallax;
        self.bgStartX = (int)(0.0 - Math.IEEERemainder(num + Main.screenPosition.X * self.bgParallax, num) - num / 2) - (int)drawOffset.X;
        self.bgLoops = (Main.screenWidth + (int)drawOffset.X * 2) / num + 2;
        self.bgTopY = self.bgStartY + self.bgLoopsY * Main.backgroundHeight[2];
        self.hellBlackBoxBottom = self.bgTopY + Main.screenPosition.Y;
        for (var num10 = 0; num10 < self.bgLoops; num10++)
        {
            for (var num11 = 0; num11 < num / 16; num11++)
            {
                float num12 = self.bgStartX + num * num10 + num11 * 16 + 8;
                var num13 = self.bgTopY;
                var color6 = Lighting.GetColor((int)((num12 + Main.screenPosition.X) / 16f), (int)((Main.screenPosition.Y + num13) / 16f));
                color6.R = (byte)(color6.R * x);
                color6.G = (byte)(color6.G * y);
                color6.B = (byte)(color6.B * z);
                Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_backTexture[6]].Value, new Vector2(self.bgStartX + num * num10 + 16 * num11 + num2, self.bgTopY) + drawOffset, new Rectangle(16 * num11 + num2 + 16, Main.magmaBGFrame * 16, 16, 16), color6);
                if (Main.ugBackTransition > 0f)
                {
                    var color7 = color6;
                    color7.R = (byte)(color7.R * Main.ugBackTransition);
                    color7.G = (byte)(color7.G * Main.ugBackTransition);
                    color7.B = (byte)(color7.B * Main.ugBackTransition);
                    color7.A = (byte)(color7.A * Main.ugBackTransition);
                    Main.spriteBatch.Draw(TextureAssets.Background[self._drawBackground_oldBackTexture[6]].Value, new Vector2(self.bgStartX + num * num10 + 16 * num11 + num2, self.bgTopY) + drawOffset, new Rectangle(16 * num11 + num2 + 16, Main.magmaBGFrame * 16, 16, 16), color7);
                }
            }
        }
    }
}
