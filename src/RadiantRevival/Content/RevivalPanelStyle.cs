using Daybreak.Common.Features.Models;
using Daybreak.Common.Features.ModPanel;
using Daybreak.Common.Rendering;
using Daybreak.Common.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;
using Terraria.UI.Chat;

namespace RadiantRevival.Content;

internal sealed class RevivalPanelStyle : ModPanelStyleExt
{
    [Autoload(Side = ModSide.Client)]
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.UI.ModPanel.MaskShader.Parameters> MaskShader { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    MaskShader = Assets.UI.ModPanel.MaskShader.CreateMaskShader(),
                }
            ).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data) { }
    }

    // TODO: Custom font visuals
    public sealed class ModName : UIText
    {
        private readonly string originalText;

        public ModName(string text, float textScale = 1, bool large = false) : base(text, textScale, large)
        {
            if (ChatManager.Regexes.Format.Count(text) != 0)
            {
                throw new InvalidOperationException("The text cannot contain formatting.");
            }

            originalText = text;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var formattedText = GetAnimatedText(originalText, Main.GlobalTimeWrappedHourly);
            SetText(formattedText);

            base.DrawSelf(spriteBatch);
        }

        public static string GetAnimatedText(string text, float time)
        {
            // [c/______:x]
            const int character_length = 12;

            var sb = new StringBuilder(character_length * text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                /*
                var wave = MathF.Sin(time * speed + i * offset);

                // Factor normalized 0-1.
                var color = Color.Lerp(lightPurple, darkPurple, (wave + 1f) / 2f);

                sb.Append($"[c/{color.Hex3()}:{text[i]}]");
                */
            }

            return sb.ToString();
        }
    }

    private sealed class ModIcon : UIImage
    {
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            // base.DrawSelf(spriteBatch);
        }
    }

    public override Dictionary<TextureKind, Asset<Texture2D>> TextureOverrides { get; } = new()
    {
        { TextureKind.ModInfo, Assets.UI.ModPanel.ModInfo.Asset },
        { TextureKind.ModConfig, Assets.UI.ModPanel.ModConfig.Asset },
    };

    public override bool PreInitialize(UIModItem element)
    {
        element.BorderColor = Color.Black;

        return base.PreInitialize(element);
    }

    public override UIImage ModifyModIcon(UIModItem element, UIImage modIcon, ref int modIconAdjust)
    {
        return new ModIcon();
    }

    public override UIText ModifyModName(UIModItem element, UIText modName)
    {
        var name = Mods.RadiantRevival.UI.ModIcon.ModName.GetTextValue();
        return new ModName(name + $" v{element._mod.Version}")
        {
            Left = modName.Left,
            Top = modName.Top,
        };
    }

    public override bool PreSetHoverColors(UIModItem element, bool hovered)
    {
        // Always set to black, we have our own effect for hovering.
        element.BorderColor = Color.Black;
        element.BackgroundColor = new Color(20, 20, 20);

        return base.PreSetHoverColors(element, hovered);
    }

    public override bool PreDrawPanel(UIModItem element, SpriteBatch sb, ref bool drawDivider)
    {
        if (element._needsTextureLoading)
        {
            element._needsTextureLoading = false;
            element.LoadTextures();
        }

        var dims = element.Dimensions;

        var panelShader = Data.Instance.MaskShader;

        using (sb.Scope())
        {
            sb.Begin(
                SpriteSortMode.Immediate,
                BlendState.NonPremultiplied,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.UIScaleMatrix
            );

            var source = new Vector4(dims.Width, dims.Height, dims.X, dims.Y);
            source = Transform(source);

            panelShader.Parameters.PanelSource = source;
            panelShader.Parameters.TargetTexture = new HlslSampler2D
            {
                Sampler = SamplerState.PointClamp,
                Texture = Assets.Sky.CelestialBodies.Moon4.Asset.Value,
            };

            panelShader.Apply();

            element.DrawPanel(sb, element._backgroundTexture.Value, element.BackgroundColor);

            sb.End();
        }

        return false;

        static Vector4 Transform(Vector4 vector)
        {
            var vec1 = Vector2.Transform(new Vector2(vector.X, vector.Y), Main.UIScaleMatrix);
            var vec2 = Vector2.Transform(new Vector2(vector.Z, vector.W), Main.UIScaleMatrix);
            return new Vector4(vec1, vec2.X, vec2.Y);
        }
    }

    public override Color ModifyEnabledTextColor(bool enabled, Color color)
    {
        return base.ModifyEnabledTextColor(enabled, color);
    }
}
