using Daybreak.Common.Features.Authorship;
using Daybreak.Common.Features.ModPanel;
using RadiantRevival.Core;

namespace RadiantRevival;

#pragma warning disable CS8603 // Possible null reference return.

partial class ModImpl : IHasCustomAuthorMessage
{
    public ModImpl()
    {
        // Handled by the asset generator.
        MusicAutoloadingEnabled = false;
    }

    string IHasCustomAuthorMessage.GetAuthorText()
    {
        return AuthorText.GetAuthorTooltip(this, headerText: null);
    }

    public override object Call(params object[] args)
    {
        return ModCallLoader.HandleCall(args);
    }
}
