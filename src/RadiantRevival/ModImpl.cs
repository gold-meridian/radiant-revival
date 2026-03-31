using Daybreak.Common.Features.Authorship;
using Daybreak.Common.Features.ModPanel;

namespace RadiantRevival;

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
}
