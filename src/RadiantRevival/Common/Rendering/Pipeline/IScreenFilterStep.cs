using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

public interface IScreenFilterStep : ILoadable
{
    void ILoadable.Load(Mod mod)
    {
        ScreenFilterRenderer.Register(this);
    }

    void ILoadable.Unload()
    { }

    EffectPriority Priority { get; }

    // TODO: Sort before/after specific filters with ScreenShaderData?

    bool Apply(in ScreenFilterRendererContext ctx);
}
