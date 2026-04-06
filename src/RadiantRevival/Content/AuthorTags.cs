using System;
using Daybreak.Common.Features.Authorship;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace RadiantRevival.Content;

internal abstract class CommonAuthorTag : AuthorTag
{
    private const string suffix = "Tag";

    public override string Name => base.Name.EndsWith(suffix) ? base.Name[..^suffix.Length] : base.Name;

    public override string Texture => string.Join('/', Assets.Authorship.Zoey.KEY.Split('/')[..^1]) + '/' + Name;
}

internal sealed class TomatTag : Daybreak.Content.Authorship.TomatTag;

internal sealed class ZoeyTag : CommonAuthorTag
{
    private static readonly Color glow_color = new(179, 133, 255);

    public override void DrawIcon(SpriteBatch spriteBatch, Vector2 position)
    {
        var glowPosition = new Vector2(position.X, position.Y - 2);
        var glowColor = glow_color * MathF.Sin(Main.GlobalTimeWrappedHourly);
        {
            spriteBatch.Draw(Assets.Authorship.Zoey_Glow.Asset.Value, glowPosition, glowColor);
        }

        base.DrawIcon(spriteBatch, position);
    }
}
