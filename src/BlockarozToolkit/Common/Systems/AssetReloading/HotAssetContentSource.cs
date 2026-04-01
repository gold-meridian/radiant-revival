using ReLogic.Content.Sources;
using System;
using System.IO;
using RadiantRevival;
using Terraria.ModLoader;

namespace BlockarozToolkit.Common.Systems.AssetReloading;

/// <summary>
///     A content source exposing a way to modify and refresh known asset paths
///     and reads files from disk given a base directory.
/// </summary>
internal sealed class HotAssetContentSource : ContentSource
{
    public string[] AssetPaths
    {
        get => assetPaths;
        set => assetPaths = value;
    }

    private readonly string directory;

    /// <summary>
    ///     Initializes the content source to read from the base directory
    ///     <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">The base directory to read from.</param>
    public HotAssetContentSource(string directory)
    {
        this.directory = directory;
        assetPaths = [];
    }

    /// <summary>
    ///     Refreshes asset names given the current value of
    ///     <see cref="AssetPaths"/>.
    /// </summary>
    public void RefreshAssetNames()
    {
        try
        {
            SetAssetNames(AssetPaths);
        }
        catch (Exception ex)
        {
            ModContent.GetInstance<ModImpl>().Logger.Warn(ex);
        }
    }

    /// <summary>
    ///     Opens a stream to a file directly on disk.
    /// </summary>
    public override Stream OpenStream(string fullAssetName)
    {
        return File.OpenRead(Path.Combine(directory, fullAssetName));
    }

    public override void Refresh() { }
    public override string? FileWatcherPath => null;
}