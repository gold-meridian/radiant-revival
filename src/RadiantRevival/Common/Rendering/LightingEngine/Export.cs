using System.Runtime.CompilerServices;
using Daybreak.Common.Features.Hooks;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using Terraria;
using Terraria.Graphics.Light;

namespace RadiantRevival.Common;

/// <summary>
///     A lightweight view into the light map export of an
///     <see cref="ILightingEngine"/>.
///     <br />
///     None of the returned data accounts for
///     <see cref="Lighting.GlobalBrightness"/> (by design)!
/// </summary>
public sealed class LightingEngineExport(LightMap? workingMap, Rectangle area)
{
    public Rectangle Area => area;

    // The map will never be directly accessed in safe APIs if the area is zero,
    // since a point will never fall within in.  Unsafe APIs have to deal with
    // this themselves, since indexing would fail regardless.
    private LightMap Map => workingMap!;

    /// <summary>
    ///     Gets the unmodified color in the lightmap using absolute tile
    ///     coordinates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 GetColor(int x, int y)
    {
        if (!Area.Contains(x, y))
        {
            return Vector3.Zero;
        }

        x -= Area.X;
        y -= Area.Y;
        return Map[x, y];
    }

    /// <summary>
    ///     Gets the unmodified color in the lightmap using local tile
    ///     coordinates (relative to the light map).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 GetColorLocal(int x, int y)
    {
        if (x < 0 || x >= Area.Width || y < 0 || y >= Area.Height)
        {
            return Vector3.Zero;
        }

        return Map[x, y];
    }

    /// <summary>
    ///     Gets the unmodified color in the lightmap using absolute tile
    ///     coordinates.
    ///     <br />
    ///     This unsafe variant performs no bounds checking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 GetColorUnsafe(int x, int y)
    {
        return Map[x - Area.X, y - Area.Y];
    }

    /// <summary>
    ///     Gets the unmodified color in the lightmap using local tile
    ///     coordinates (relative to the light map).
    ///     <br />
    ///     This unsafe variant performs no bounds checking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 GetColorLocalUnsafe(int x, int y)
    {
        return Map[x, y];
    }
}

partial class LightingEngine
{
    // This empty export has a null map to reduce memory consumption and extra
    // time spent initializing an export that goes generally unused.  We just
    // need to populate with a sane default value to avoid exceptions before
    // a game world is entered.
    private static readonly LightingEngineExport empty_export = new(workingMap: null, area: Rectangle.Empty);

    public static LightingEngineExport EngineExport { get; private set; } = empty_export;

    [OnLoad]
    private static void ApplyExportHooks()
    {
        IL_Lighting.LightTiles += il =>
        {
            var c = new ILCursor(il);

            c.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt<ILightingEngine>(nameof(ILightingEngine.ProcessArea)));
            c.EmitDelegate(
                static () =>
                {
                    if (!TryGetCurrentEngine(out var engine))
                    {
                        EngineExport = empty_export;
                        return;
                    }

                    EngineExport = engine.GetExport();
                }
            );
        };
    }
}
