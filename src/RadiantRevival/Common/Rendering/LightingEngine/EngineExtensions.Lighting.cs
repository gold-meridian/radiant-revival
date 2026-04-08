using Daybreak.Common.Features.Hooks;
using VanillaEngine = Terraria.Graphics.Light.LightingEngine;

namespace RadiantRevival.Common;

partial class LightingEngine
{
    private readonly struct LightingEngineAdvanced(VanillaEngine engine) : IAdvancedLightingEngine
    {
        public LightingEngineExport GetExport()
        {
            return new LightingEngineExport(engine._activeLightMap, engine._activeProcessedArea);
        }
    }

    [OnLoad]
    private static void LoadLightingImplementation()
    {
        RegisterAdvancedEngineConverter<VanillaEngine>(static engine => new LightingEngineAdvanced(engine));
    }
}
