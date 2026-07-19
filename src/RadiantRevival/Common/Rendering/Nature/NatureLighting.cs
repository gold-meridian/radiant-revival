using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Mathematics;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

public static class NatureLighting
{
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.Nature.NatureLighting.Parameters> NatureLightingShader { get; init; }

        public required WrapperShaderData<Assets.Nature.NaturePreprocessing.Parameters> NatureMaskShader { get; init; }

        public required WrapperShaderData<Assets.Nature.NaturePreprocessing.Parameters> NatureDistanceFieldShader { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    NatureLightingShader = Assets.Nature.NatureLighting.CreateNatureLightingShader(),
                    NatureMaskShader = Assets.Nature.NaturePreprocessing.CreateNatureMaskShader(),
                    NatureDistanceFieldShader = Assets.Nature.NaturePreprocessing.CreateNatureDistanceFieldShader(),
                }
            ).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data)
        { }
    }

    private record struct NatureData(
        DrawParameters DrawData,
        Texture2D? ProcessedTexture,
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
                currentProcessedTexture,
                false
            );

            drawData.Add(data);

            currentProcessedTexture = null;
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

    private static Texture2D? currentProcessedTexture;

    private static readonly (float Base, float Multiplier) default_contrast = (0.2f, 1.5f);

    [OnLoad]
    private static void Load()
    {
        Main.instance.TilesRenderer._natureRenderer = new NatureRenderer();

        On_TileDrawing.GetTreeTopTexture += GetTreeTopTexture_GetBaseTexture;
        On_TileDrawing.GetTreeBranchTexture += GetTreeBranchTexture_GetBaseTexture;
    }

    private static Texture2D[]? treeBranchProcessed;
    private static Texture2D[]? treeTopProcessed;

    [ModSystemHooks.ResizeArrays]
    private static void ResizeArrays()
    {
        Main.RunOnMainThread(ProcessTrees);
    }

    [OnUnload]
    private static void Unload()
    {
        if (treeBranchProcessed is null
         || treeTopProcessed is null)
        {
            return;
        }

        for (var i = 0; i < treeBranchProcessed.Length; i++)
        {
            treeBranchProcessed[i].Dispose();
            treeBranchProcessed[i] = null;
        }

        for (var i = 0; i < treeTopProcessed.Length; i++)
        {
            treeTopProcessed[i].Dispose();
            treeTopProcessed[i] = null;
        }
    }

    private static void ProcessTrees()
    {
        var device = Main.graphics.GraphicsDevice;
        var sb = Main.spriteBatch;

        var maskShader = Data.Instance.NatureMaskShader;
        var distanceFieldShader = Data.Instance.NatureDistanceFieldShader;

        using var _ = sb.Scope();

        Branches();
        Tops();

        return;

        void Tops()
        {
            treeTopProcessed = new Texture2D[TextureAssets.TreeTop.Length];

            for (var i = 0; i < treeTopProcessed.Length; i++)
            {
                TextureAssets.TreeTop[i].Wait();

                var original = TextureAssets.TreeTop[i].Value;

                var frameSize = GetSingleFrameSize(original, i);

                var target = new RenderTarget2D(device, original.Width, original.Height);
                using var swapTarget = RenderTargetPool.Shared.Rent(device, original.Width, original.Height);

                using (swapTarget.Scope(clearColor: Color.Transparent))
                {
                    for (var x = 0f; x < original.Width; x += frameSize.X)
                    {
                        for (var y = 0f; y < original.Height; y += frameSize.Y)
                        {
                            var style = (int)(y / frameSize.Y);

                            GetNatureSettings(i, style, out var settings, out var contrast);

                            var source = new Rectangle((int)x, (int)y, (int)frameSize.X, (int)frameSize.Y);

                            DrawFrame(original, source, settings, contrast ?? default_contrast);
                        }
                    }
                }

                RenderDistanceField(original, target, swapTarget.Target, frameSize);

                treeTopProcessed[i] = target;
            }

            return;

            static Vector2 GetSingleFrameSize(Texture2D texture, int index)
            {
                // TODO: Impl that better supports ModTrees?
                return index switch
                {
                    // Hallow trees
                    3 or 19 => new Vector2(texture.Width / 9f, texture.Height),
                    20 => new Vector2(texture.Width / 18f, texture.Height),
                    // Palm trees
                    15 or 21 => new Vector2(texture.Width / 3f, texture.Height / 4f),
                    _ => new Vector2(texture.Width / 3f, texture.Height),
                };
            }
        }

        void Branches()
        {
            treeBranchProcessed = new Texture2D[TextureAssets.TreeBranch.Length];

            var branchFrame = new Vector2(42, 42);

            for (var i = 0; i < treeBranchProcessed.Length; i++)
            {
                TextureAssets.TreeBranch[i].Wait();

                var original = TextureAssets.TreeBranch[i].Value;

                var target = new RenderTarget2D(device, original.Width, original.Height);
                using var swapTarget = RenderTargetPool.Shared.Rent(device, original.Width, original.Height);

                using (swapTarget.Scope(clearColor: Color.Transparent))
                {
                    for (var x = 0f; x < original.Width; x += branchFrame.X)
                    {
                        for (var y = 0f; y < original.Height; y += branchFrame.Y)
                        {
                            var style = (int)(y / (branchFrame.Y * 3));

                            GetNatureSettings(i, style, out var settings, out var contrast);

                            var source = new Rectangle((int)x, (int)y, (int)branchFrame.X, (int)branchFrame.Y);

                            DrawFrame(original, source, settings, contrast ?? default_contrast);
                        }
                    }
                }

                RenderDistanceField(original, target, swapTarget.Target, branchFrame);

                treeBranchProcessed[i] = target;
            }
        }

        void DrawFrame(Texture2D texture, Rectangle rect, TreePaintingSettings settings, (float Base, float Multiplier) contrast)
        {
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            {
                var usesGroup = settings.UseSpecialGroups;

                var invert = settings.InvertSpecialGroupResult;

                var minHue = settings.SpecialGroupMinimalHueValue;
                var maxHue = settings.SpecialGroupMaximumHueValue;

                if (!usesGroup)
                {
                    minHue = 0f;
                    maxHue = 1f;
                }

                maskShader.Parameters.MinHue = minHue;
                maskShader.Parameters.MaxHue = maxHue;
                maskShader.Parameters.InvertHue = invert && (minHue > 0f || maxHue < 1f);
                maskShader.Parameters.HueOffset = settings.HueTestOffset;

                var minSat = settings.SpecialGroupMinimumSaturationValue;
                var maxSat = settings.SpecialGroupMaximumSaturationValue;

                if (!usesGroup)
                {
                    minSat = 0f;
                    maxSat = 1f;
                }

                maskShader.Parameters.MinSat = minSat;
                maskShader.Parameters.MaxSat = maxSat;
                maskShader.Parameters.InvertSat = invert && (minSat > 0f || maxSat < 1f);

                maskShader.Parameters.Contrast = new Vector2(contrast.Base, contrast.Multiplier);

                maskShader.Parameters.NatureTexture = new HlslSampler2D
                {
                    Sampler = SamplerState.LinearClamp,
                    Texture = texture,
                };

                maskShader.Apply();

                sb.Draw(texture, rect, rect, Color.White);
            }
            sb.End();
        }

        void RenderDistanceField(Texture2D texture, RenderTarget2D target, RenderTarget2D swapTarget, Vector2 frameSize)
        {
            using (target.Scope(clearColor: Color.Transparent))
            {
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

                distanceFieldShader.Parameters.FrameSize = frameSize;

                distanceFieldShader.Parameters.NatureTexture = new HlslSampler2D
                {
                    Sampler = SamplerState.PointClamp,
                    Texture = texture,
                };

                distanceFieldShader.Parameters.MaskTexture = new HlslSampler2D
                {
                    Sampler = SamplerState.LinearClamp,
                    Texture = swapTarget,
                };

                distanceFieldShader.Apply();

                sb.Draw(texture, Vector2.Zero, Color.White);

                sb.End();
            }
        }
    }

    private static Texture2D GetTreeBranchTexture_GetBaseTexture(On_TileDrawing.orig_GetTreeBranchTexture orig, TileDrawing self, int treeTextureIndex, int treeTextureStyle, byte tileColor)
    {
        currentProcessedTexture = treeBranchProcessed![treeTextureIndex];

        return orig(self, treeTextureIndex, treeTextureStyle, tileColor);
    }

    private static Texture2D GetTreeTopTexture_GetBaseTexture(On_TileDrawing.orig_GetTreeTopTexture orig, TileDrawing self, int treeTextureIndex, int treeTextureStyle, byte tileColor)
    {
        currentProcessedTexture = treeTopProcessed![treeTextureIndex];

        return orig(self, treeTextureIndex, treeTextureStyle, tileColor);
    }

    private static void GetNatureSettings(
        int treeTextureIndex,
        int treeTextureStyle,
        out TreePaintingSettings settings,
        out (float Base, float Multiplier)? contrast
    )
    {
        settings = TreePaintSystemData.GetTreeFoliageSettings(treeTextureIndex, treeTextureStyle);
        contrast = null;

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
            settings = settingsOverride;
        }

        if (contrast_overrides.TryGetValue(contrastKey, out var range))
        {
            contrast = range;
        }
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
            foreach (var (drawData, processed, ignoreLighting) in data)
            {
                if (ignoreLighting
                 || processed is null
                 || color is { R: <= 0, G: <= 0, B: <= 0 })
                {
                    sb.spriteEffect.CurrentTechnique.Passes[0].Apply();

                    sb.Draw(drawData);

                    continue;
                }

                effect.Parameters.ProcessedTexture = new HlslSampler2D
                {
                    Sampler = SamplerState.PointClamp,
                    Texture = processed,
                };

                var baseColor = drawData.Color;

                var lightColor = color * (1f - MathF.Pow(1f - skyColor.Lightness, 3f));

                lightColor *= Utils.Remap(baseColor.Lightness, 0f, skyColor.Lightness, 0f, 1f);

                effect.Parameters.LightColor = lightColor.ToVector4();
                effect.Parameters.LightPosition = lightPosition;

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
                foreach (var (drawData, _, _) in data)
                {
                    sb.Draw(drawData);
                }
            }
            sb.End();
        }
    }
}

