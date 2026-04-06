global using static RadiantRevival.Common.ShaderMacros;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace RadiantRevival.Common;

internal static class ShaderMacros
{
    public static Vector2 TextureSize(int register)
    {
        if (Main.instance.GraphicsDevice.Textures[register] is not Texture2D tex)
        {
            return Vector2.Zero;
        }

        return new Vector2(tex.Width, tex.Height);
    }
}
