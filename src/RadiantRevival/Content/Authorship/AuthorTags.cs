using Daybreak.Common.Features.Authorship;

namespace RadiantRevival.Content;

internal abstract class CommonAuthorTag : AuthorTag
{
    private const string suffix = "Tag";

    public override string Name => base.Name.EndsWith(suffix) ? base.Name[..^suffix.Length] : base.Name;

    public override string Texture => string.Join('/', Assets.Authorship.Zoey.KEY.Split('/')[..^1]) + '/' + Name;
}

internal sealed class TomatTag : Daybreak.Content.Authorship.TomatTag;

