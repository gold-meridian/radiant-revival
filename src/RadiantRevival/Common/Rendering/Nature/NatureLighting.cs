using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Mathematics;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using System.Collections.Generic;
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
                false
            );

            drawData.Add(data);

            baseTexture = null;
            priorSettings = null;
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

    private static Texture2D? baseTexture;
    private static TreePaintingSettings? priorSettings;

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
        baseTexture = TextureAssets.Tile[key.TileType].Value;

        return orig(self, key);
    }

    private static Texture2D GetTreeBranchTexture_GetBaseTexture(On_TileDrawing.orig_GetTreeBranchTexture orig, TileDrawing self, int treeTextureIndex, int treeTextureStyle, byte tileColor)
    {
        baseTexture = TextureAssets.TreeBranch[treeTextureIndex].Value;
        priorSettings = TreePaintSystemData.GetTreeFoliageSettings(treeTextureIndex, treeTextureStyle);

        return orig(self, treeTextureIndex, treeTextureStyle, tileColor);
    }

    private static Texture2D GetTreeTopTexture_GetBaseTexture(On_TileDrawing.orig_GetTreeTopTexture orig, TileDrawing self, int treeTextureIndex, int treeTextureStyle, byte tileColor)
    {
        baseTexture = TextureAssets.TreeTop[treeTextureIndex].Value;
        priorSettings = TreePaintSystemData.GetTreeFoliageSettings(treeTextureIndex, treeTextureStyle);

        return orig(self, treeTextureIndex, treeTextureStyle, tileColor);
    }

    private static void DrawNatureData(IEnumerable<NatureData> data)
    {
        var sb = Main.spriteBatch;

        using var _ = sb.Scope();

        var effect = Data.Instance.NatureLightingShader;

        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        {
            foreach (var (drawData, unpainted, treeSettings, ignoreLighting) in data)
            {
                if (ignoreLighting ||
                    unpainted is null)
                {
                    sb.spriteEffect.CurrentTechnique.Passes[0].Apply();

                    sb.Draw(drawData);

                    continue;
                }

                effect.Parameters.SourceTexture = new HlslSampler2D
                {
                    Sampler = SamplerState.PointClamp,
                    Texture = unpainted,
                };

                effect.Parameters.MinSat = treeSettings?.SpecialGroupMinimumSaturationValue ?? 0;
                effect.Parameters.MaxSat = treeSettings?.SpecialGroupMaximumSaturationValue ?? 1;

                effect.Parameters.MinHue = treeSettings?.SpecialGroupMinimalHueValue ?? 0;
                effect.Parameters.MaxHue = treeSettings?.SpecialGroupMaximumHueValue ?? 1;

                effect.Apply();

                sb.Draw(drawData);
            }
        }
        sb.End();
    }
}

