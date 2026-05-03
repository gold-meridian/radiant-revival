using System.Numerics;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace RenderReprise.Common;

/// <summary>
///     Responsible for managing tiled rendering state and operations.
///     <br />
///     Tiled rendering abstracts over existing vanilla render buffers to allow
///     for rendering content that would normally exceed the limits of the
///     screen buffer.
/// </summary>
public static class TiledRenderingManager
{
    private sealed class TileInfo
    {
        public required RenderTargetLease Lease { get; set; }

        public required Vector2 WorldPosition { get; set; }

        public required int TileX { get; set; }

        public required int TileY { get; set; }

        public RenderTarget2D Target => Lease.Target;
    }
}
