using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Mathematics;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

public static class NatureLighting
{
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.Nature.NatureLighting.Parameters> NatureLightingShader { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    NatureLightingShader = Assets.Nature.NatureLighting.CreateNatureLightingShader(),
                }
            ).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data)
        { }
    }

    private record struct NatureData(
        DrawParameters DrawData,
        Texture2D? UnpaintedTexture,
        TreePaintingSettings? TreeSettings,
        (float Base, float Multiplier)? ContrastRange,
        bool IgnoreLighting
    );

    private sealed class NatureRenderer : INatureRenderer
    {
        private readonly List<NatureData> drawData = [];

        public void DrawNature(Texture2D texture, Vector2 position, Rectangle sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth, SideFlags seams = SideFlags.None)
        {
            var data = new NatureData(
                new DrawParameters(texture)
                {
                    Position = position,
                    Source = sourceRectangle,
                    Color = color,
                    Rotation = Angle.FromRadians(rotation),
                    Origin = origin,
                    Scale = new Vector2(scale),
                    Effects = effects,
                },
                baseTexture,
                priorSettings,
                contrastRange,
                false
            );

            drawData.Add(data);

            baseTexture = null;
            priorSettings = null;
            contrastRange = null;
        }

        public void DrawGlowmask(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            var data = new NatureData(
                new DrawParameters(texture)
                {
                    Position = position,
                    Source = sourceRectangle,
                    Color = color,
                    Rotation = Angle.FromRadians(rotation),
                    Origin = origin,
                    Scale = new Vector2(scale),
                    Effects = effects,
                },
                null,
                null,
                null,
                true
            );

            drawData.Add(data);
        }

        public void DrawAfterAllObjects(SpriteBatchBeginner beginner)
        {
            DrawNatureData(drawData);

            drawData.Clear();
        }
    }

    // The shader we have seems unable to accurately mask based on the vanilla hue/sat limiters.
    private static readonly Dictionary<(int[] Indices, int[] Styles), TreePaintingSettings> tree_settings_overrides = new()
    {
        {
            ([15, 21], [0, 4]), // PalmTreePurity
            new TreePaintingSettings
            {
                UseSpecialGroups = true,
                SpecialGroupMinimalHueValue = 11f / 72f,
                SpecialGroupMaximumHueValue = 0.25f,
                SpecialGroupMinimumSaturationValue = 0.3f,
                SpecialGroupMaximumSaturationValue = 1f,
            }
        },

        {
            ([15, 21], [3, 7]), // PalmTreeCorruption
            new TreePaintingSettings
            {
                UseSpecialGroups = true,
                SpecialGroupMinimalHueValue = 0.5f,
                SpecialGroupMaximumHueValue = 0.7f,
                SpecialGroupMinimumSaturationValue = 0.19f,
                SpecialGroupMaximumSaturationValue = 1f,
            }
        },

        {
            ([15, 21], [1, 5]), // PalmTreeCrimson
            new TreePaintingSettings
            {
                UseSpecialGroups = true,
                SpecialGroupMinimalHueValue = 0f,
                SpecialGroupMaximumHueValue = 0.2f,
                SpecialGroupMinimumSaturationValue = 0.19f,
                SpecialGroupMaximumSaturationValue = 1f,
            }
        },

        {
            ([1], []),
            new TreePaintingSettings
            {
                UseSpecialGroups = true,
                SpecialGroupMinimalHueValue = 0.5f,
                SpecialGroupMaximumHueValue = 1f,
                SpecialGroupMinimumSaturationValue = 0.2f,
                SpecialGroupMaximumSaturationValue = 1f,
            }
        },

        {
            ([3, 19, 20], []), // WoodHallow
            new TreePaintingSettings
            {
                UseSpecialGroups = true,
                SpecialGroupMinimalHueValue = 0f,
                SpecialGroupMaximumHueValue = 1f,
                SpecialGroupMinimumSaturationValue = 0f,
                SpecialGroupMaximumSaturationValue = 0.38f,
                InvertSpecialGroupResult = true,
            }
        },

        {
            ([29], []), // VanityCherry
            new TreePaintingSettings
            {
                UseSpecialGroups = true,
                SpecialGroupMinimalHueValue = 0.02f,
                SpecialGroupMaximumHueValue = 0.7f,
                SpecialGroupMinimumSaturationValue = 0f,
                SpecialGroupMaximumSaturationValue = 1f,
                InvertSpecialGroupResult = true,
            }
        },
    };

    private static readonly Dictionary<(int[] Indices, int[] Styles), (float Base, float Multiplier)> contrast_overrides = new()
    {
        {
            ([1], []), // WoodCorruption
            (0.28f, 1.3f)
        },

        {
            ([5], []), // WoodCrimson
            (0.34f, 0.95f)
        },

        {
            ([3, 19, 20], []), // WoodHallow
            (0.27f, 1.5f)
        },

        {
            ([2, 11, 13], []), // WoodJungle
            (0.13f, 1.24f)
        },

        {
            ([15, 21], [0, 4]), // PalmTreePurity
            (0.18f, 1.3f)
        },

        {
            ([15, 21], [3, 7]), // PalmTreeCorruption
            (0.23f, 1.2f)
        },

        {
            ([15, 21], [2, 6]), // PalmTreeHallow
            (0.2f, 0.7f)
        },

        {
            ([29], []), // VanityCherry
            (0.47f, 0.5f)
        },

        {
            ([30], []), // VanityYellowWillow
            (0.5f, 1.15f)
        },
    };

    private static Texture2D? baseTexture;
    private static TreePaintingSettings? priorSettings;
    private static (float Base, float Multiplier)? contrastRange;

    [OnLoad]
    private static void Load()
    {
        Main.instance.TilesRenderer._natureRenderer = new NatureRenderer();

        On_TileDrawing.GetTreeTopTexture += GetTreeTopTexture_GetBaseTexture;
        On_TileDrawing.GetTreeBranchTexture += GetTreeBranchTexture_GetBaseTexture;

        On_TileDrawing.GetTileDrawTexture_TileVariationkey += GetTileDrawTexture_GetBaseTexture;
    }

    private static Texture2D GetTileDrawTexture_GetBaseTexture(On_TileDrawing.orig_GetTileDrawTexture_TileVariationkey orig, TileDrawing self, TilePaintSystemV2.TileVariationkey key)
    {
        // Exclude vines as they don't play nicely with this effect.
        if (TileID.Sets.IsVine[key.TileType]
         || TileID.Sets.VineThreads[key.TileType]
         || TileID.Sets.ReverseVineThreads[key.TileType])
        {
            return orig(self, key);
        }

        baseTexture = TextureAssets.Tile[key.TileType].Value;

        return orig(self, key);
    }

    private static Texture2D GetTreeBranchTexture_GetBaseTexture(On_TileDrawing.orig_GetTreeBranchTexture orig, TileDrawing self, int treeTextureIndex, int treeTextureStyle, byte tileColor)
    {
        baseTexture = TextureAssets.TreeBranch[treeTextureIndex].Value;
        priorSettings = TreePaintSystemData.GetTreeFoliageSettings(treeTextureIndex, treeTextureStyle);

        var settingsKey = tree_settings_overrides.Keys
                                                 .FirstOrDefault(
                                                      key => key.Indices.Contains(treeTextureIndex)
                                                          && (key.Styles.Contains(treeTextureStyle) || key.Styles.Length <= 0)
                                                  );

        var contrastKey = contrast_overrides.Keys
                                            .FirstOrDefault(
                                                 key => key.Indices.Contains(treeTextureIndex)
                                                     && (key.Styles.Contains(treeTextureStyle) || key.Styles.Length <= 0)
                                             );

        if (tree_settings_overrides.TryGetValue(settingsKey, out var settingsOverride))
        {
            priorSettings = settingsOverride;
        }

        if (contrast_overrides.TryGetValue(contrastKey, out var range))
        {
            contrastRange = range;
        }

        return orig(self, treeTextureIndex, treeTextureStyle, tileColor);
    }

    private static Texture2D GetTreeTopTexture_GetBaseTexture(On_TileDrawing.orig_GetTreeTopTexture orig, TileDrawing self, int treeTextureIndex, int treeTextureStyle, byte tileColor)
    {
        baseTexture = TextureAssets.TreeTop[treeTextureIndex].Value;
        priorSettings = TreePaintSystemData.GetTreeFoliageSettings(treeTextureIndex, treeTextureStyle);

        var settingsKey = tree_settings_overrides.Keys
                                                 .FirstOrDefault(
                                                      key => key.Indices.Contains(treeTextureIndex)
                                                          && (key.Styles.Contains(treeTextureStyle) || key.Styles.Length <= 0)
                                                  );

        var contrastKey = contrast_overrides.Keys
                                            .FirstOrDefault(
                                                 key => key.Indices.Contains(treeTextureIndex)
                                                     && (key.Styles.Contains(treeTextureStyle) || key.Styles.Length <= 0)
                                             );

        if (tree_settings_overrides.TryGetValue(settingsKey, out var settingsOverride))
        {
            priorSettings = settingsOverride;
        }

        if (contrast_overrides.TryGetValue(contrastKey, out var range))
        {
            contrastRange = range;
        }

        return orig(self, treeTextureIndex, treeTextureStyle, tileColor);
    }

    private static void DrawNatureData(IEnumerable<NatureData> data)
    {
        var sb = Main.spriteBatch;

        using var _ = sb.Scope();

        var effect = Data.Instance.NatureLightingShader;

        HorizonHelper.GetCelestialBodyColors(out var sunColor, out var _);

        sunColor = sunColor.MultiplyRGB(Color.Khaki * 0.9f);

        NextHorizonRenderer.GetVisibilities(out var sunsetVisibility, out var sunriseVisibility, out var celestialVisibility);

        var color = sunColor;

        var num = Math.Max(sunsetVisibility, sunriseVisibility) * celestialVisibility;

        color *= num;

        var skyColor = Main.ColorOfTheSkies;

        var screenSize = new Vector2(Main.screenWidth, Main.screenHeight);

        var lightPosition = Main.LastCelestialBodyPosition * screenSize;

        if (Main.GameViewMatrix.Effects.HasFlag(SpriteEffects.FlipVertically))
        {
            lightPosition.Y = screenSize.Y - lightPosition.Y;
        }

        if (Main.screenPosition.Y >= Main.worldSurface * 16.0 + 16.0
         || !Main.ShouldDrawSurfaceBackground()
         || !Main.HorizonHelper.SunVisibilityEnabled
         || !Main.ForegroundSunlightEffects)
        {
            SimpleDraw();

            return;
        }

        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        {
            foreach (var (drawData, unpainted, treeSettings, contrast, ignoreLighting) in data)
            {
                if (ignoreLighting
                 || unpainted is null
                 || color is { R: <= 0, G: <= 0, B: <= 0 })
                {
                    sb.spriteEffect.CurrentTechnique.Passes[0].Apply();

                    sb.Draw(drawData);

                    continue;
                }

                effect.Parameters.UnpaintedTexture = new HlslSampler2D
                {
                    Sampler = SamplerState.PointClamp,
                    Texture = unpainted,
                };

                var usesGroup = treeSettings?.UseSpecialGroups is true;

                var invert = treeSettings?.InvertSpecialGroupResult ?? false;

                var minHue = treeSettings?.SpecialGroupMinimalHueValue ?? 0f;
                var maxHue = treeSettings?.SpecialGroupMaximumHueValue ?? 1f;

                if (!usesGroup)
                {
                    minHue = 0f;
                    maxHue = 1f;
                }

                effect.Parameters.MinHue = minHue;
                effect.Parameters.MaxHue = maxHue;
                effect.Parameters.InvertHue = invert && (minHue > 0f || maxHue < 1f);
                effect.Parameters.HueOffset = treeSettings?.HueTestOffset ?? 0;

                var minSat = treeSettings?.SpecialGroupMinimumSaturationValue ?? 0f;
                var maxSat = treeSettings?.SpecialGroupMaximumSaturationValue ?? 1f;

                if (!usesGroup)
                {
                    minSat = 0f;
                    maxSat = 1f;
                }

                effect.Parameters.MinSat = minSat;
                effect.Parameters.MaxSat = maxSat;
                effect.Parameters.InvertSat = invert && (minSat > 0f || maxSat < 1f);

                effect.Parameters.Contrast = new Vector2(contrast?.Base ?? 0.2f, contrast?.Multiplier ?? 1.5f);

                var baseColor = drawData.Color;

                var lightColor = color * (1f - MathF.Pow(1f - skyColor.Lightness, 3f));



                lightColor *= Utils.Remap(baseColor.Lightness, 0f, skyColor.Lightness, 0f, 1f);

                effect.Parameters.LightColor = lightColor.ToVector4();

                effect.Parameters.LightPosition = lightPosition;

                var position = drawData.Position - drawData.Origin;

                effect.Parameters.Destination = new Vector4(drawData.Size, position.X, position.Y);

                var source = drawData.Source ?? drawData.Texture.Bounds;

                effect.Parameters.Source = new Vector4(source.Size() / drawData.Texture.Size(), source.X / (float)drawData.Texture.Width, source.Y / (float)drawData.Texture.Height);

                effect.Parameters.DrawZoom = 1f / Main.GameZoomTarget;

                effect.Apply();

                sb.Draw(drawData);
            }
        }
        sb.End();

        return;

        void SimpleDraw()
        {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            {
                foreach (var (drawData, _, _, _, _) in data)
                {
                    sb.Draw(drawData);
                }
            }
            sb.End();
        }
    }
}

