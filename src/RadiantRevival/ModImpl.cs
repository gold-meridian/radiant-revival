using Daybreak.Common.Features.Authorship;
using Daybreak.Common.Features.ModPanel;
using RadiantRevival.Common;
using ReLogic.Content.Sources;

namespace RadiantRevival;

#pragma warning disable CS8603 // Possible null reference return.

partial class ModImpl : IHasCustomAuthorMessage
{
    public ModImpl()
    {
        // Handled by the asset generator.
        MusicAutoloadingEnabled = false;
    }

    public override IContentSource CreateDefaultContentSource()
    {
        AddContent(new ObjModelReader());

        return base.CreateDefaultContentSource();
    }

    public override object Call(params object[] args)
    {
        return ModCallDispatcher.Dispatch(args);
    }

    string IHasCustomAuthorMessage.GetAuthorText()
    {
        return AuthorText.GetAuthorTooltip(this, headerText: null);
    }
}
