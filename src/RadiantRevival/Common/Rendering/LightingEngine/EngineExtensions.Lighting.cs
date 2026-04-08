using System;
using Daybreak.Common.Features.Hooks;
using Terraria.Graphics.Light;
using VanillaEngine = Terraria.Graphics.Light.LightingEngine;

namespace RadiantRevival.Common;

partial class LightingEngine
{
    private sealed class LightingEngineAdvanced(VanillaEngine engine) : IAdvancedLightingEngine
    {
        public LightingEngineExport GetExport()
        {
            return new LightingEngineExport(engine._activeLightMap, engine._activeProcessedArea);
        }

        public IDisposable OverrideLightMap(LightMap lightMap)
        {
            var activeLightMap = engine._activeLightMap;
            {
                engine._activeLightMap = lightMap;
            }

            return DisposableBuilder
                  .Create()
                  .AddAction(() => engine._activeLightMap = activeLightMap)
                  .Build();
        }

        public IDisposable OverrideLightMapFullbright()
        {
            var fbMap = FullbrightLightMap.GetLightMap(engine._activeLightMap.Width, engine._activeLightMap.Height);
            return OverrideLightMap(fbMap);
        }
    }

    [OnLoad]
    private static void LoadLightingImplementation()
    {
        RegisterAdvancedEngineConverter<VanillaEngine>(static engine => new LightingEngineAdvanced(engine));
    }
}
