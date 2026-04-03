using System;
using System.Collections.Generic;
using System.Text;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Features.ModPanel;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;
using Terraria.UI.Chat;

namespace RadiantRevival.Content;

internal sealed class PanelStyle : ModPanelStyleExt
{
    [Autoload(Side = ModSide.Client)]
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.UI.ModPanel.ModPanelShader.Parameters> PanelShader { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () =>
                {
                    var panelShaderData = Assets.UI.ModPanel.ModPanelShader.CreatePanelShader();

                    return new Data
                    {
                        PanelShader = panelShaderData,
                    };
                }
            ).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data) { }
    }

    public sealed class ModName : UIText
    {
        private readonly string originalText;

        public ModName(string text, float textScale = 1, bool large = false) : base(text, textScale, large)
        {
            if (ChatManager.Regexes.Format.Matches(text).Count != 0)
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
        public ModIcon() : base() { }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            // base.DrawSelf(spriteBatch);
        }
    }

    private static float hoverIntensity;

    public override Dictionary<TextureKind, Asset<Texture2D>> TextureOverrides { get; } = [];

    public override void Load()
    {
        base.Load();
    }

    public override bool PreInitialize(UIModItem element)
    {
        element.BorderColor = Color.Black;

        return base.PreInitialize(element);
    }

    public override void PostInitialize(UIModItem element)
    {
        base.PostInitialize(element);
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

        // Render our cool custom panel with a shader.
        {
            sb.End(out var ss);
            sb.Begin(
                SpriteSortMode.Immediate,
                BlendState.NonPremultiplied,
                SamplerState.PointClamp,
                DepthStencilState.None,
                ss.RasterizerState,
                null,
                Main.UIScaleMatrix
            );
            {
                var data = Data.Instance;
                var shaderData = data.PanelShader;

                var dims = element.GetDimensions();

                hoverIntensity = MathHelper.Lerp(hoverIntensity, element.IsMouseHovering ? 1f : 0f, 0.15f);
                hoverIntensity = Math.Clamp(MathF.Round(hoverIntensity, 2), 0f, 1f);

                element.DrawPanel(sb, Assets.UI.ModPanel.BevelPanel.Asset.Value, element.BackgroundColor);
            }
            sb.Restart(in ss);
        }

        return false;
    }

    public override Color ModifyEnabledTextColor(bool enabled, Color color)
    {
        return base.ModifyEnabledTextColor(enabled, color);
    }

    private static Vector4 Transform(Vector4 vector)
    {
        var vec1 = Vector2.Transform(new Vector2(vector.X, vector.Y), Main.UIScaleMatrix);
        var vec2 = Vector2.Transform(new Vector2(vector.Z, vector.W), Main.UIScaleMatrix);
        return new Vector4(vec1, vec2.X, vec2.Y);
    }
}
