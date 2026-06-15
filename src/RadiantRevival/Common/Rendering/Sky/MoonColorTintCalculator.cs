using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace RadiantRevival.Common.Rendering.Sky;

// TODO -- This might be redundant? Pretty sure Zoey made something similar.
/// <summary>
///     A simple system that examines moon textures to
///     acquire an approximate tint for each of them.
/// </summary>
[Autoload(Side = ModSide.Client)]
public sealed class MoonColorTintCalculator : ModSystem
{
    private static Color[] tints = [];

    public override void PostSetupContent()
    {
        Main.RunOnMainThread(() =>
        {
            tints = new Color[TextureAssets.Moon.Length];

            for (var i = 0; i < TextureAssets.Moon.Length; i++)
            {
                var asset = TextureAssets.Moon[i];
                var texture = asset.Value;
                while (!asset.IsLoaded)
                    texture = asset.Value;

                var colors = new Color[texture.Width * texture.Height];
                texture.GetData(colors);

                tints[i] = CalculateTint(colors);
            }
        }).GetAwaiter().GetResult();
    }

    private static Color CalculateTint(Color[] colors)
    {
        var alphaDivisior = 0f;
        var colorSum = Vector4.Zero;
        for (var i = 0; i < colors.Length; i++)
        {
            var color = colors[i].ToVector4();
            colorSum += color * color.W;
            alphaDivisior += color.W;
        }
        colorSum /= alphaDivisior;

        return new Color(colorSum.X, colorSum.Y, colorSum.Z);
    }

    /// <summary>
    ///     Gets and returns the current moon color for a given
    ///     moon texture index.
    /// </summary>
    public static Color Get(int moonVariantIndex)
    {
        if (Main.netMode == NetmodeID.Server || moonVariantIndex < 0 || moonVariantIndex >= TextureAssets.Moon.Length)
            return Color.Transparent;

        return tints[moonVariantIndex];
    }
}
