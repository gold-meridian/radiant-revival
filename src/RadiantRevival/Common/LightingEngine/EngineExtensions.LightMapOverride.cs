using System;
using Terraria.Graphics.Light;

namespace RadiantRevival.Common;

partial class LightingEngine
{
    private sealed class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private static readonly IDisposable no_op_disposable = new NoOpDisposable();

    public static IDisposable OverrideLightMap(LightMap lightMap)
    {
        if (!TryGetCurrentEngine(out var engine))
        {
            return no_op_disposable;
        }

        return engine.OverrideLightMap(lightMap);
    }

    public static IDisposable OverrideLightMapFullbright()
    {
        if (!TryGetCurrentEngine(out var engine))
        {
            return no_op_disposable;
        }

        return engine.OverrideLightMapFullbright();
    }
}
