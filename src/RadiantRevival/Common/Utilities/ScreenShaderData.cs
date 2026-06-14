using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Terraria.GameContent.Shaders;
using Terraria.Graphics.Effects;

namespace RadiantRevival.Common;

public static class WaterShaderDataExtensions
{
    extension(WaterShaderData)
    {
        public static WaterShaderData Instance => (WaterShaderData)Filters.Scene["WaterDistortion"].GetShader();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void QueueRipple(Vector2 position, float strength = 1f, RippleShape shape = RippleShape.Square, float rotation = 0f)
        {
            WaterShaderData.Instance.QueueRipple(position, strength, shape, rotation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void QueueRipple(Vector2 position, float strength, Vector2 size, RippleShape shape = RippleShape.Square, float rotation = 0f)
        {
            WaterShaderData.Instance.QueueRipple(position, strength, size, shape, rotation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void QueueRipple(Vector2 position, Color waveData, Vector2 size, RippleShape shape = RippleShape.Square, float rotation = 0f)
        {
            WaterShaderData.Instance.QueueRipple(position, waveData, size, shape, rotation);
        }
    }
}
