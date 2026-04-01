using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader;

namespace BlockarozToolkit.Common.Systems.AssetReloading;

/// <summary>
///     A model providing necessary data to manage the asset hot reload state
///     of a given <see cref="Mod"/>.
/// </summary>
/// <param name="mod">The mod.</param>
internal sealed class HotReloadContext(Mod mod)
{
    /// <summary>
    ///     The mod this context belongs to.
    /// </summary>
    public Mod Mod { get; } = mod;

    /// <summary>
    ///     The path to the mod's source on disk.
    /// </summary>
    public string SourceFolder => Mod.SourceFolder;

    /// <summary>
    ///     The file system watcher monitoring changes to mod content.
    /// </summary>
    public FileSystemWatcher Watcher { get; } = new(mod.SourceFolder)
    {
        NotifyFilter = NotifyFilters.Attributes | NotifyFilters.Security | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastAccess | NotifyFilters.LastWrite,
        IncludeSubdirectories = true,
        EnableRaisingEvents = true,
    };

    /// <summary>
    ///     The content source providing modified files read from disk.
    /// </summary>
    public HotAssetContentSource Source { get; } = new(mod.SourceFolder);

    /// <summary>
    ///     Whether this mod needs hot reload changes to be applied.
    /// </summary>
    public bool NeedsReload { get; set; }
}