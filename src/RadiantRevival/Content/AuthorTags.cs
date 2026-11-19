using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace RadiantRevival.Content;

internal abstract class CommonAuthorTag : AuthorTag
{
    private const string suffix = "Tag";

    public override string Name => base.Name.EndsWith(suffix) ? base.Name[..^suffix.Length] : base.Name;

    public override string Texture => string.Join('/', Assets.Authorship.Sprunolia.KEY.Split('/')[..^1]) + '/' + Name;
}

internal sealed class TomatTag : Daybreak.Content.Authorship.TomatTag;

internal sealed class ZoeyTag : Daybreak.Content.Authorship.ZoeyTag;

internal sealed class SprunoliaTag : CommonAuthorTag;

internal sealed class LucilleTag : CommonAuthorTag
{
    /// <summary>
    ///     The 0-1 progress value indicating
    ///     how far along the twitch animation is.
    /// </summary>
    /// <remarks>
    ///     Does not update naturally unless the value
    ///     is greater than zero.
    /// </remarks>
    private static float twitchAnimationProgress;

    /// <summary>
    ///     The scale of the ears.
    /// </summary>
    private const float scale = 0.7f;

    /// <summary>
    ///     The 1/N probability that the twitch animation
    ///     will start.
    /// </summary>
    private const int twitch_start_chance = 700;

    /// <summary>
    ///     How long the twitch visual should last for, in
    ///     frames.
    /// </summary>
    private const float twitch_duration = 20;

    public override void DrawIcon(SpriteBatch spriteBatch, Vector2 position)
    {
        position += new Vector2(8f, 26f);

        if (twitchAnimationProgress > 0f)
        {
            twitchAnimationProgress += 1f / twitch_duration;
            if (twitchAnimationProgress >= 1f)
                twitchAnimationProgress = 0f;
        }
        else if (Main.rand.NextBool(twitch_start_chance))
            twitchAnimationProgress = 0.01f;

        var easedTwitchAnimationCompletion = 1f - MathF.Pow(1f - twitchAnimationProgress, 3f);
        var twitchAngleOffset = MathF.Sin(MathF.PI * easedTwitchAnimationCompletion) * -0.3f;

        var leftEar = Assets.Authorship.LucilleLeftEar.Asset.Value;
        var rightEar = Assets.Authorship.LucilleRightEar.Asset.Value;
        spriteBatch.Draw(leftEar, position, null, Color.White, twitchAngleOffset, new Vector2(8f, 40f), scale, 0, 0f);

        position.X += scale * 17f;
        spriteBatch.Draw(rightEar, position, null, Color.White, twitchAngleOffset * -0.05f, new Vector2(12f, 38f), scale, 0, 0f);

        // base.DrawIcon(spriteBatch, position);
    }
}

internal sealed class IncattusTag : CommonAuthorTag;
