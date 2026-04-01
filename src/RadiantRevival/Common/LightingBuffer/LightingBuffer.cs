using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Light;

namespace RadiantRevival.Common;

public readonly struct LightingBuffer
{
    public Texture2D Texture { get; init; }

    public Rectangle TileScreenBounds { get; init; }
}

public static class LightingBufferExtensions
{
    extension(LightMap lightMap)
    {
        public LightingBuffer GetGpuBuffer()
        {
            return default;
            //return LightMapRewrite.GetGpuBuffer(lightMap);
        }
    }
    
    extension(ILightingEngine engine)
    {
        public LightingBuffer GetGpuBuffer()
        {
            return engine switch
            {
                LegacyLighting legacyLighting => legacyLighting._lightMap.GetGpuBuffer(),
                LightingEngine lightingEngine => lightingEngine._activeLightMap.GetGpuBuffer(),
                _ => throw new InvalidOperationException("Cannot acquire lightmap GPU buffer for unknown lighting engine: " + engine.GetType().FullName)
            };
        }
    }
    
    extension(Lighting)
    {
        public static LightingBuffer GetGpuBuffer()
        {
            return Lighting._activeEngine.GetGpuBuffer();
        }
    }
}
