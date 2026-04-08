using System;
using System.Threading.Tasks;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

public static partial class LightingEngine
{
    private sealed class Buffers : IStatic<Buffers>
    {
        public required RenderTargetLease TileSpaceBuffer { get; init; }

        public required RenderTargetLease ScreenSpaceBuffer { get; init; }

        public static Buffers LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Buffers
                {
                    TileSpaceBuffer = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, GetBufferSize),
                    ScreenSpaceBuffer = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice),
                }
            ).GetAwaiter().GetResult();

            static (int, int) GetBufferSize(int width, int height)
            {
                return (
                    (int)Math.Ceiling(width / 16f) + BufferOffscreenTileRange * 2,
                    (int)Math.Ceiling(height / 16f) + BufferOffscreenTileRange * 2
                );
            }
        }

        public static void UnloadData(Buffers data)
        {
            Main.RunOnMainThread(
                () =>
                {
                    data.TileSpaceBuffer.Dispose();
                    data.ScreenSpaceBuffer.Dispose();
                }
            );
        }
    }

    /// <summary>
    ///     The number of additional tiles to include offscreen on each side of
    ///     the 2-dimensional <see cref="TileSpaceBuffer"/>.
    ///     <br />
    ///     This is to help with rendering of partially-offscreen entities.
    /// </summary>
    public static int BufferOffscreenTileRange => 1;

    /// <summary>
    ///     The light map buffer in tile-space.  This is the canonical buffer,
    ///     where each tile corresponds to a single pixel in a 2-dimensional
    ///     grid.
    /// </summary>
    public static RenderTargetLease TileSpaceBuffer => Buffers.Instance.TileSpaceBuffer;

    /// <summary>
    ///     The light map buffer in screen-space.  This is derived from
    ///     <see cref="TileSpaceBuffer"/>.  TODO: Make public?
    /// </summary>
    private static RenderTargetLease ScreenSpaceBuffer => Buffers.Instance.ScreenSpaceBuffer;

    private static Color[] colorBuffer = [];
    private static bool debugLightMap;

    [OnLoad]
    private static void ApplyBufferHooks()
    {
        // This target is selected because it's right after lighting updates and
        // before anything else is drawn.  We need to be rather early since the
        // light map is sampled for early rendering operations such as
        // background rendering.
        IL_Main.DoDraw += il =>
        {
            var c = new ILCursor(il);

            c.GotoNext(x => x.MatchStsfld<Main>(nameof(Main.onlyDrawFancyUI)));
            c.GotoNext(MoveType.After, x => x.MatchCall<Lighting>(nameof(Lighting.LightTiles)));
            c.EmitDelegate(
                () =>
                {
                    PopulateBuffers();
                    TransferBuffers();
                }
            );
        };

        On_Main.DoDraw_Tiles_Solid += (orig, self) =>
        {
            orig(self);
            DebugDrawLightmap(Main.spriteBatch);
        };
    }

    private static unsafe void PopulateBuffers()
    {
        var lightingBuffer = TileSpaceBuffer.Target;

        var bufferSize = lightingBuffer.Width * lightingBuffer.Height;
        if (colorBuffer.Length < bufferSize)
        {
            Array.Resize(ref colorBuffer, bufferSize);
        }

        var startX = (int)(Main.screenPosition.X / 16) - BufferOffscreenTileRange;
        var startY = (int)(Main.screenPosition.Y / 16) - BufferOffscreenTileRange;
        Parallel.For(
            0,
            lightingBuffer.Width,
            x =>
            {
                var tileX = startX + x;

                for (var y = 0; y < lightingBuffer.Height; y++)
                {
                    var tileY = startY + y;

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
        var sb = Main.spriteBatch;
        using (sb.Scope())
        using (ScreenSpaceBuffer.Scope())
        {
            sb.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            var offset = new Vector2(Main.screenPosition.X % 16, Main.screenPosition.Y % 16);
            sb.Draw(
                TileSpaceBuffer.Target,
                new Vector2(-BufferOffscreenTileRange * 16) - offset,
                null,
                Color.White,
                0,
                Vector2.Zero,
                16,
                SpriteEffects.None,
                0
            );

            sb.End();
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

            sb.Draw(ScreenSpaceBuffer.Target, Vector2.Zero, Color.White);

            sb.End();
        }
    }
}
