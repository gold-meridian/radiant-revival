using System;
using Daybreak.Common.Features.Hooks;
using Terraria.Graphics.Light;
using VanillaEngine = Terraria.Graphics.Light.LightingEngine;

namespace RadiantRevival.Common;

partial class LightingEngine
{
    private sealed class OverrideMapScope(VanillaEngine engine, LightMap activeMap) : IDisposable
    {
        public void Dispose()
        {
            engine._activeLightMap = activeMap;
        }
    }

    private readonly struct LightingEngineAdvanced(VanillaEngine engine) : IAdvancedLightingEngine
    {
        public LightingEngineExport GetExport()
        {
            return new LightingEngineExport(engine._activeLightMap, engine._activeProcessedArea);
        }

        public IDisposable OverrideMap(LightMap lightMap)
        {
            var activeLightMap = engine._activeLightMap;
            {
                engine._activeLightMap = lightMap;
            }
            return new OverrideMapScope(engine, activeLightMap);
        }

        public IDisposable OverrideMapFullbright()
        {
            return FullbrightLightMap.ApplyTo(engine._activeLightMap);
        }
    }

    [OnLoad]
    private static void LoadLightingImplementation()
    {
        RegisterAdvancedEngineConverter<VanillaEngine>(static engine => new LightingEngineAdvanced(engine));
    }
}
