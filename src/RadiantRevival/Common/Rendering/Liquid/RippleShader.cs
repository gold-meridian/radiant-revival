using Terraria.Graphics.Effects;

namespace RadiantRevival.Common;

public static class RippleShader
{
    [OnLoad]
    private static void Load()
    {
        Filters.Scene["WaterDistortion"]._priority = EffectPriority.VeryLow;
    }
}
