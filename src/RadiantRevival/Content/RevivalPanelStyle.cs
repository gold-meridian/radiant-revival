using Daybreak.Common.Features.Authorship;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Features.ModPanel;
using Daybreak.Common.Rendering;
using Daybreak.Common.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Common;
using RadiantRevival.Core;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;
using Terraria.UI;
using Terraria.UI.Chat;
using static Terraria.GameContent.Skies.StardustSky;

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

    private static bool currentlyDrawing;

    private static readonly Color state_text_inner = new Color(255, 227, 123);
    private static readonly Color state_text_outer = new Color(167, 23, 152);

    [OnLoad]
    private static void Load()
    {
        MonoModHooks.Modify(
            typeof(UIModStateText).GetMethod(
                nameof(UIModStateText.DrawEnabledText),
                BindingFlags.Instance | BindingFlags.NonPublic
            ),
            DrawEnabledText_CustomText
        );
    }

    private static void DrawEnabledText_CustomText(ILContext il)
    {
        var c = new ILCursor(il);

        var positionIndex = -1;

        var jumpRetTarget = c.DefineLabel();

        c.GotoNext(
            MoveType.After,
            i => i.MatchMul()
        );

        c.GotoNext(
            MoveType.After,
            i => i.MatchStloc(out positionIndex)
        );

        c.EmitLdarg0();
        c.EmitLdloc(positionIndex);

        c.EmitDelegate(
            static (UIModStateText element, Vector2 position) =>
            {
                if (!currentlyDrawing)
                {
                    return false;
                }

                var sb = Main.spriteBatch;
                var font = FontAssets.MouseText.Value;

                ChatManager.DrawColorCodedStringWithShadow(
                    sb,
                    font,
                    element.DisplayText,
                    position,
                    state_text_inner,
                    state_text_outer,
                    0f,
                    Vector2.Zero,
                    Vector2.One,
                    999f, // For whatever reason the game doesn't use shadowColor if maxWidth is below 0???
                    1.5f
                );

                return true;
            }
        );

        c.EmitBrfalse(jumpRetTarget);

        c.EmitRet();

        c.MarkLabel(jumpRetTarget);
    }

    public override Color ModifyEnabledTextColor(bool enabled, Color color)
    {
        return state_text_inner;
    }

    public override bool PreDraw(UIModItem element, SpriteBatch sb)
    {
        currentlyDrawing = true;
        return base.PreDraw(element, sb);
    }

    public override void PostDraw(UIModItem element, SpriteBatch sb)
    {
        currentlyDrawing = false;
    }

    [ModSystemHooks.PostSetupContent]
    private static void PostSetupContent()
    {
        Main.RunOnMainThread(InitFireworkPatterns).GetAwaiter().GetResult();
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
        { TextureKind.InnerPanel, Assets.UI.ModPanel.InnerPanel.Asset },
    };

    public override bool PreInitialize(UIModItem element)
    {
        element.BorderColor = Color.Black;

        element.OnUpdate += OnUpdate_Particles;

        element.OnUpdate += OnUpdate_Hover;

        starsCreated = false;
        element.OnUpdate += OnUpdate_Stars;

        return base.PreInitialize(element);
    }

    private static bool starsCreated;

    private static void OnUpdate_Stars(UIElement affectedElement)
    {
        if (!starsCreated)
        {
            CreateStars(affectedElement.Dimensions);
            starsCreated = true;
        }
    }

    private static float hoverIntensity;

    private static void OnUpdate_Hover(UIElement affectedElement)
    {
        hoverIntensity -= 0.01f;

        if (affectedElement.Dimensions.Contains(Main.MouseScreen.ToPoint()))
        {
            hoverIntensity += 0.2f;
        }

        hoverIntensity = MathHelper.Clamp(hoverIntensity, 0f, 1f);
    }

    private static void OnUpdate_Particles(UIElement affectedElement)
    {
        UpdateSparks(affectedElement.Dimensions);
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

    private static readonly Color background_gradient_lower = new Color(9, 14, 211);
    private static readonly Color background_nebula = new Color(5, 10, 255);

    public override bool PreDrawPanel(UIModItem element, SpriteBatch sb, ref bool drawDivider)
    {
        if (element._needsTextureLoading)
        {
            element._needsTextureLoading = false;
            element.LoadTextures();
        }

        var device = sb.GraphicsDevice;

        var dims = element.Dimensions;

        var panelShader = Data.Instance.MaskShader;

        var scissor = device.ScissorRectangle;

        sb.End(out var ss);

        using var lease = RenderTargetPool.Shared.Rent(device, dims.Width / 2, dims.Height / 2, RenderTargetDescriptor.Default);

        using (lease.Scope(clearColor: Color.Black))
        {
            DrawPanelContents(sb, device);
        }

        device.ScissorRectangle = scissor;

        sb.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            ss.RasterizerState,
            null,
            Main.UIScaleMatrix
        );
        {
            var source = new Vector4(dims.Width, dims.Height, dims.X, dims.Y);
            source = Transform(source);

            panelShader.Parameters.PanelSource = source;
            panelShader.Parameters.TargetTexture = new HlslSampler2D
            {
                Sampler = SamplerState.PointClamp,
                Texture = lease.Target,
            };

            panelShader.Apply();

            element.DrawPanel(sb, element._backgroundTexture.Value, Color.White);

            sb.spriteEffectPass.Apply();

            var outlineColor = Color.OklabLerp(background_nebula, new Color(246, 190, 66), hoverIntensity);

            element.DrawPanel(sb, element._borderTexture.Value, outlineColor);
        }
        sb.Restart(in ss);

        return false;

        static Vector4 Transform(Vector4 vector)
        {
            var vec1 = Vector2.Transform(new Vector2(vector.X, vector.Y), Main.UIScaleMatrix);
            var vec2 = Vector2.Transform(new Vector2(vector.Z, vector.W), Main.UIScaleMatrix);
            return new Vector4(vec1, vec2.X, vec2.Y);
        }
    }

    private static void DrawPanelContents(SpriteBatch sb, GraphicsDevice device)
    {
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        {
            var sky = Assets.UI.ModPanel.PanelBackground.Asset.Value;

            var bounds = device.Viewport.Bounds;

            sb.Draw(sky, bounds, null, Color.White);

            var pixel = TextureAssets.MagicPixel.Value;

            var overlayColor = background_gradient_lower * MathF.Pow(hoverIntensity, 3f) * 0.6f;

            sb.Draw(pixel, bounds, null, overlayColor);

            var nebula = Assets.UI.ModPanel.NebulaLeft.Asset.Value;

            var nebulaColor = background_nebula * 0.5f;

            sb.Draw(nebula, Vector2.Zero, nebulaColor);
        }
        sb.End();

        DrawStars(sb);

        DrawSparks(sb);
    }

#region Stars
    private readonly record struct Star(Vector2 Position, int Style, float Phase);

    private const int star_styles = 9;

    private const int star_count = 35;
    private static readonly Star[] stars = new Star[star_count];

    private static void CreateStars(Rectangle dims)
    {
        for (var i = 0; i < stars.Length; i++)
        {
            var positon = RandomPosition(dims) * 0.5f;

            stars[i] = new Star(positon, Main.rand.Next(star_styles), Main.rand.NextFloatDirection());
        }

        return;

        static Vector2 RandomPosition(Rectangle dims)
        {
            return Main.rand.NextVector2FromRectangle(dims) - dims.TopLeft();
        }
    }

    private static void DrawStars(SpriteBatch sb)
    {
        const float twinkle_freq = 2f;
        const float twinkle_ampl = 0.7f;

        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        {
            var texture = Assets.UI.ModPanel.Stars.Asset.Value;

            var origin = texture.Frame(1, star_styles).Size() * 0.5f;

            foreach (var star in stars)
            {
                var frame = texture.Frame(1, star_styles, 0, star.Style);

                var frequency = twinkle_freq * (1 - (star.Style / (float)star_styles + 0.1f));

                var scale = 0.5f;
                scale *= 0.8f + (MathF.Sin((Main.GlobalTimeWrappedHourly + star.Phase) * frequency) * twinkle_ampl);
                scale = Math.Min(scale, 0.5f);

                var position = RoundPosition(star.Position);

                sb.Draw(texture, position, frame, Color.White, 0, origin, scale, SpriteEffects.None, 0f);
            }
        }
        sb.End();

        return;

        static Vector2 RoundPosition(Vector2 position)
        {
            return new Vector2((int)position.X + 0.5f, (int)position.Y + 0.5f);
        }
    }
#endregion

#region Fireworks
    private record struct Spark(Vector2 Position, Vector2 Velocity, Color Color, float Scale, float Lifetime, bool Active);

    private const int spark_count = 1300;
    private static readonly Spark[] sparks = new Spark[spark_count];

    private static readonly Color firework_red = new Color(255, 196, 216);

    private static readonly Color firework_yellow = new Color(255, 230, 117);

    private static readonly Color firework_blue = new Color(161, 213, 255);

    private static readonly Color firework_swirl_color = new Color(116, 131, 250);

    private readonly record struct ExplosionImage(Color[,] Colors, int Width, int Height);

    private readonly record struct FireworkPattern(Action<Vector2> Explosion, int Chance);

    private static readonly List<FireworkPattern> patterns = [];

    private static void InitFireworkPatterns()
    {
        patterns.Add(new FireworkPattern(ExplosionFivePointStar, 160));
        patterns.Add(new FireworkPattern(ExplosionFourPointStar, 180));
        patterns.Add(new FireworkPattern(ExplosionSwirl, 580));

        AddImage(Assets.UI.ModPanel.Fireworks.Extra_98.Asset, 700, true);
        AddImage(Assets.UI.ModPanel.Fireworks.Nightshade.Asset, 600, false);
        AddImage(Assets.UI.ModPanel.Fireworks.SteamHappy.Asset, 1300, false);

        var mod = ModContent.GetInstance<ModImpl>();
        var authors = mod.GetContent<AuthorTag>();

        foreach (var author in authors)
        {
            if (!ModContent.RequestIfExists<Texture2D>(author.Texture, out var icon))
            {
                continue;
            }

            AddImage(icon, 540, true);
        }

        return;

        static void AddImage(Asset<Texture2D> asset, int chance, bool doubleScale = true)
        {
            asset.Wait();

            var ex = FromImage(asset.Value, doubleScale);

            patterns.Add(
                new FireworkPattern(
                    p => ExplosionCustomImage(p, ex),
                    chance
                )
            );
        }

        static ExplosionImage FromImage(Texture2D texture, bool doubleScale = true)
        {
            var pixelSize = doubleScale ? 2 : 1;

            var data = new Color[texture.Width * texture.Height];

            var colors = new Color[texture.Width / pixelSize, texture.Height / pixelSize];

            texture.GetData(data);

            for (var i = 0; i < texture.Width; i += pixelSize)
            {
                for (var j = 0; j < texture.Height; j += pixelSize)
                {
                    var col = data[i + (j * texture.Width)];

                    colors[i / pixelSize, j / pixelSize] = col;
                }
            }

            return new ExplosionImage(colors, texture.Width / pixelSize, texture.Height / pixelSize);
        }
    }

    private static void UpdateSparks(Rectangle dims)
    {
        const float spark_lifetime_increment = 0.013f;

        for (var i = 0; i < sparks.Length; i++)
        {
            ref var spark = ref sparks[i];

            if (!spark.Active)
            {
                continue;
            }

            spark.Position += spark.Velocity;
            spark.Velocity *= 0.92f;

            spark.Lifetime += spark_lifetime_increment;

            if (spark.Lifetime > 1f)
            {
                spark.Active = false;
            }
        }

        SpawnFireworkExplosions();

        return;

        void SpawnFireworkExplosions()
        {
            foreach (var fireworkPattern in patterns)
            {
                if (!Main.rand.NextBool(fireworkPattern.Chance))
                {
                    continue;
                }

                var position = RandomPosition(dims) / 2;

                fireworkPattern.Explosion(position);
            }
        }

        static Vector2 RandomPosition(Rectangle dims)
        {
            return Main.rand.NextVector2FromRectangle(dims) - dims.TopLeft();
        }
    }

    private static void DrawSparks(SpriteBatch sb)
    {
        const float spark_scale_freq = 22f;

        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        {
            var texture = Assets.UI.ModPanel.Spark.Asset.Value;

            var origin = texture.Size() * 0.5f;

            foreach (var spark in sparks)
            {
                if (!spark.Active)
                {
                    continue;
                }

                var scale = spark.Scale;

                scale *= 1 - MathF.Pow(spark.Lifetime, 5);

                scale *= 1 + (MathF.Sin((spark.Lifetime + scale) * spark_scale_freq) * 0.5f);

                sb.Draw(texture, spark.Position, null, spark.Color, 0, origin, scale, SpriteEffects.None, 0f);
            }
        }
        sb.End();
    }

    private static void ExplosionCustomImage(Vector2 position, ExplosionImage explosion)
    {
        const float range = MathHelper.PiOver4;

        var speed = Main.rand.NextFloat(1f, 4.5f);

        var rotation = Main.rand.NextFloat(-range, range);

        for (var i = 0; i < explosion.Width; i++)
        {
            for (var j = 0; j < explosion.Height; j++)
            {
                var color = explosion.Colors[i, j];

                var imageSize = new Vector2(explosion.Width, explosion.Height);

                var velocity = new Vector2(i, j) - (imageSize * 0.5f);
                velocity /= imageSize * 0.5f;
                velocity *= speed;

                velocity = velocity.RotatedBy(rotation);

                var size = Main.rand.NextFloat(0.35f, 1.1f);

                var lifetime = Main.rand.NextFloat(0f, 0.35f);

                SpawnSpark(position, velocity, color, size, lifetime);
            }
        }
    }

    private static void ExplosionFivePointStar(Vector2 position)
    {
        var smoothness = Main.rand.NextFloat(0f, 0.3f);

        var color = Color.OklabLerp(firework_red, firework_yellow, Main.rand.NextFloat());

        ExplosionStar(position, color, 5, 60, smoothness);
    }

    private static void ExplosionFourPointStar(Vector2 position)
    {
        var smoothness = Main.rand.NextFloat(0.1f, 0.2f);

        var color = Color.OklabLerp(firework_red, firework_blue, Main.rand.NextFloat());

        ExplosionStar(position, color, 4, 50, smoothness);
    }

    private static void ExplosionStar(Vector2 position, Color color, int points, int count, float smoothness = 0)
    {
        var increment = MathF.Tau / count;

        var rotationOffset = Main.rand.NextFloatDirection();

        var speed = Main.rand.NextFloat(1.3f, 7.5f);

        for (var t = 0f; t < MathF.Tau; t += increment)
        {
            var m = points - 2;

            var num = MathF.Cos((2 * MathF.Asin(1 - smoothness) + MathF.PI * m) / (2 * points));
            var denom = MathF.Cos((2 * MathF.Asin((1 - smoothness) * MathF.Cos(points * t)) + MathF.PI * m) / (2 * points));
            var radius = num / denom;

            var velocity = Vector2.UnitY.RotatedBy(t + rotationOffset) * radius * speed;

            var size = Main.rand.NextFloat(0.15f, 0.75f);

            var lifetime = Main.rand.NextFloat(0f, 0.35f);

            SpawnSpark(position, velocity, color, size, lifetime);
        }
    }

    private static void ExplosionSwirl(Vector2 position)
    {
        const int count = 120;

        const float loops = 2;

        const float radians = MathF.Tau * loops;

        const float increment = radians / count;

        var rotationOffset = Main.rand.NextFloatDirection();

        var speed = Main.rand.NextFloat(2.3f, 6.5f);

        var color = firework_swirl_color;

        for (var t = 0f; t < radians; t += increment)
        {
            var radius = 0.1f + (t / radians);

            var velocity = Vector2.UnitY.RotatedBy(t + rotationOffset) * radius * speed;

            var size = Main.rand.NextFloat(0.25f, 0.65f);

            var lifetime = Main.rand.NextFloat(0f, 0.35f);

            SpawnSpark(position, velocity, color, size, lifetime);
        }
    }

    private static void SpawnSpark(Vector2 position, Vector2 velocity, Color color, float scale, float lifetime)
    {
        var index = Array.FindIndex(sparks, s => !s.Active);

        if (index == -1)
        {
            return;
        }

        if (color is { R: < 40, G: < 40, B: < 40 } || color.A == 0)
        {
            return;
        }

        scale *= color.A / (float)byte.MaxValue;

        color.A = 120;

        sparks[index] = new Spark(position, velocity, color, scale, lifetime, true);
    }
#endregion
}
