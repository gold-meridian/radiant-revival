using System;
using System.Threading.Tasks;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

public sealed class LightingBuffers : IStatic<LightingBuffers>
{
    public required RenderTargetLease TotalLightingBuffer { get; init; }

    public required RenderTargetLease ScreenSizeLightingBuffer { get; init; }

    private static Color[] colorBuffer = [];
    private static bool debugLightMap;

    private const int lighting_buffer_offscreen_range_tiles = 1;

    public static LightingBuffers LoadData(Mod mod)
    {
        return Main.RunOnMainThread(
            () => new LightingBuffers
            {
                TotalLightingBuffer = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, GetBufferSize),
                ScreenSizeLightingBuffer = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice),
            }
        ).GetAwaiter().GetResult();

        static (int, int) GetBufferSize(int width, int height)
        {
            return (
                (int)Math.Ceiling(width / 16f) + lighting_buffer_offscreen_range_tiles * 2,
                (int)Math.Ceiling(height / 16f) + lighting_buffer_offscreen_range_tiles * 2
            );
        }
    }

    public static void UnloadData(LightingBuffers data)
    {
        Main.RunOnMainThread(
            () =>
            {
                data.TotalLightingBuffer.Dispose();
                data.ScreenSizeLightingBuffer.Dispose();
            }
        );
    }

    [OnLoad]
    private static void ApplyHooks()
    {
        On_Main.DoDraw_WallsTilesNPCs += (orig, self) =>
        {
            PopulateBuffers();
            TransferBuffers();
            orig(self);
        };

        On_Main.DoDraw_Tiles_Solid += (orig, self) =>
        {
            orig(self);
            DebugDrawLightmap(Main.spriteBatch);
        };
    }

    private static unsafe void PopulateBuffers()
    {
        var lightingBuffer = LightingBuffers.Instance.TotalLightingBuffer.Target;

        var bufferSize = lightingBuffer.Width * lightingBuffer.Height;
        if (colorBuffer.Length < bufferSize)
        {
            Array.Resize(ref colorBuffer, bufferSize);
        }

        Parallel.For(
            0,
            lightingBuffer.Width,
            x =>
            {
                var tileX = (int)(Main.screenPosition.X / 16) + x - lighting_buffer_offscreen_range_tiles;

                for (var y = 0; y < lightingBuffer.Height; y++)
                {
                    var tileY = (int)(Main.screenPosition.Y / 16) + y - lighting_buffer_offscreen_range_tiles;

                    colorBuffer[y * lightingBuffer.Width + x] = Lighting.GetColor(tileX, tileY);
                }
            }
        );

        fixed (Color* pColorBuffer = &colorBuffer[0])
        {
            lightingBuffer.SetDataPointerEXT(0, null, (nint)pColorBuffer, bufferSize * 4);
        }
    }

    private static void TransferBuffers()
    {
        using (Main.spriteBatch.Scope())
        using (LightingBuffers.Instance.ScreenSizeLightingBuffer.Scope())
        {
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            var offset = new Vector2(Main.screenPosition.X % 16, Main.screenPosition.Y % 16);
            Main.spriteBatch.Draw(
                LightingBuffers.Instance.TotalLightingBuffer.Target,
                new Vector2(-lighting_buffer_offscreen_range_tiles * 16) - offset,
                null,
                Color.White,
                0,
                Vector2.Zero,
                16,
                SpriteEffects.None,
                0
            );

            Main.spriteBatch.End();
        }
    }

    [ModSystemHooks.PostUpdateInput]
    private static void UpdateDebugInputs()
    {
        const Keys light_map_key = Keys.F6;

        if (Main.keyState.IsKeyDown(light_map_key) && !Main.oldKeyState.IsKeyDown(light_map_key))
        {
            debugLightMap = !debugLightMap;
            Main.NewText($"Light Map ({light_map_key}): " + (debugLightMap ? "Shown" : "Hidden"), debugLightMap ? Color.Green : Color.Red);
        }
    }

    private static void DebugDrawLightmap(SpriteBatch sb)
    {
        if (!debugLightMap)
        {
            return;
        }

        using (sb.Scope())
        {
            sb.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );

            sb.Draw(LightingBuffers.Instance.ScreenSizeLightingBuffer.Target, Vector2.Zero, Color.White);

            sb.End();
        }
    }
}
