using ReLogic.Content;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RadiantRevival;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Assets;

namespace BlockarozToolkit.Common.Systems.AssetReloading;

public static class ToolkitUtils
{
    public static string NormalizePath(string path)
    {
        path = path.StartsWith('\\') || path.StartsWith('/') ? path[1..] : path;
        return path.Replace('\\', '/');
    }
}

public sealed class HotReloadAssetTool
{
    public static bool Active => ModContent.GetInstance<ModImpl>().SourceFolder is not null;
}

/// <summary>
///     Manages monitoring and applying asset hot reloads.
/// </summary>
[Autoload(Side = ModSide.Client)]
internal sealed class HotAssetSystem : ModSystem
{
    private static readonly Dictionary<string, HotAssetReloader> reloaderByExtension = new Dictionary<string, HotAssetReloader>()
    {
        { ".fx", new ShaderSourceHotReloader() },
        { ".hlsl", new ShaderSourceHotReloader() },
    };
    
    private readonly Dictionary<string, HotReloadContext> mods = [];
    private static string[] supportedExtensions = [];

    private static readonly CancellationTokenSource tokenSource = new();

    public static bool IsReloadingAssetsRightNow { get; private set; }

    public override void Load()
    {
        base.Load();

        // Watch for eligible mods.
        foreach (Mod mod in ModLoader.Mods.Where(x => !string.IsNullOrEmpty(x.SourceFolder) && Directory.Exists(x.SourceFolder)))
        {
            mods[mod.Name] = new HotReloadContext(mod);
        }

        foreach (HotReloadContext mod in mods.Values)
        {
            mod.Watcher.Changed += OnFileChanged(mod);
            mod.Watcher.Created += OnFileCreated(mod);
            mod.Watcher.Deleted += OnFileDeleted(mod);
            mod.Watcher.Renamed += OnFileRenamed(mod);
        }

        supportedExtensions = Main.instance.Services.Get<AssetReaderCollection>().GetSupportedExtensions();

        MonoModHooks.Add(
            typeof(TModContentSource).GetMethod(nameof(TModContentSource.OpenStream), BindingFlags.Public | BindingFlags.Instance),
            OpenStream_UseHotReloadContext
        );
    }

    private Stream OpenStream_UseHotReloadContext(Func<TModContentSource, string, Stream> orig, TModContentSource self, string assetName)
    {
        if (mods.TryGetValue(self.file.Name, out HotReloadContext ctx) && ctx.Source.EnumerateAssets().Contains(assetName))
        {
            return ctx.Source.OpenStream(assetName);
        }
        
        return orig(self, assetName);
    }

    public override void PostAddRecipes()
    {
        base.PostAddRecipes();

        if (!Main.dedServ)
        {
            Task.Run(UpdateAssetReloader, tokenSource.Token);
        }
    }

    public async Task UpdateAssetReloader()
    {
        tokenSource.Token.ThrowIfCancellationRequested();

        while (true)
        {
            tokenSource.Token.ThrowIfCancellationRequested();

            if (HotReloadAssetTool.Active)
            {
                lock (mods)
                {
                    foreach (HotReloadContext mod in mods.Values.Where(n => n.NeedsReload))
                        ReloadModAssets(mod);
                }
            }

            await Task.Delay(1000);
        }
        
        // ReSharper disable once FunctionNeverReturns
    }

    public override void Unload()
    {
        base.Unload();
        
        tokenSource.Cancel();
    }

    private static void ReloadModAssets(HotReloadContext mod)
    {
        // Forcefully reload assets.
        Main.RunOnMainThread(
            () =>
            {
                IsReloadingAssetsRightNow = true;

                try
                {
                    mod.NeedsReload = false;

                    // First set to only the normal content source to unapply any
                    // previous hot reload changes, then apply with the hot reload
                    // source to re-apply changes.
                    // We can't just re-apply with the hot reload source already
                    // present because if a modified file is modified again, it
                    // won't be replicated in-game (without first unapplying).
                    mod.Mod.Assets.SetSources([mod.Mod.RootContentSource]);
                    mod.Mod.Assets.SetSources([mod.Source, mod.Mod.RootContentSource]);
                }
                finally
                {
                    IsReloadingAssetsRightNow = false;
                }
            }
        );
    }

    private static FileSystemEventHandler OnFileChanged(HotReloadContext mod)
    {
        return (_, args) =>
        {
            if (!HotReloadAssetTool.Active)
                return;

            string relativePath = ToolkitUtils.NormalizePath(args.FullPath[mod.SourceFolder.Length..]);

            if (IgnoreCompletely(relativePath))
                return;

            if (CommonSkipQueue(mod, relativePath))
                return;

            mod.Source.AssetPaths = mod.Source.AssetPaths.Where(x => x != relativePath).ToArray();
            mod.Source.AssetPaths = mod.Source.AssetPaths.Append(relativePath).ToArray();
            mod.Source.RefreshAssetNames();
        };
    }

    private static FileSystemEventHandler OnFileCreated(HotReloadContext mod)
    {
        return (_, args) =>
        {
            if (!HotReloadAssetTool.Active)
                return;

            string relativePath = ToolkitUtils.NormalizePath(args.FullPath[mod.SourceFolder.Length..]);

            if (IgnoreCompletely(relativePath))
                return;

            if (CommonSkipQueue(mod, relativePath))
                return;

            mod.Source.AssetPaths = mod.Source.AssetPaths.Where(x => x != relativePath).ToArray();
            mod.Source.AssetPaths = mod.Source.AssetPaths.Append(relativePath).ToArray();
            mod.Source.RefreshAssetNames();
        };
    }

    private static FileSystemEventHandler OnFileDeleted(HotReloadContext mod)
    {
        return (_, args) =>
        {
            // Maybe ignore deleting entirely?

            if (!HotReloadAssetTool.Active)
                return;

            string relativePath = ToolkitUtils.NormalizePath(args.FullPath[mod.SourceFolder.Length..]);

            if (IgnoreCompletely(relativePath))
                return;

            if (CommonSkipQueue(mod, relativePath))
                return;

            mod.Source.AssetPaths = mod.Source.AssetPaths.Where(x => x != relativePath).ToArray();
            mod.Source.RefreshAssetNames();
        };
    }

    private static RenamedEventHandler OnFileRenamed(HotReloadContext mod)
    {
        return (_, args) =>
        {
            if (!HotReloadAssetTool.Active)
                return;

            string relativeOldPath = ToolkitUtils.NormalizePath(args.OldFullPath[mod.SourceFolder.Length..]);
            string relativePath = ToolkitUtils.NormalizePath(args.FullPath[mod.SourceFolder.Length..]);

            if (IgnoreCompletely(relativePath) || IgnoreCompletely(relativeOldPath))
                return; // TODO

            if (CommonSkipQueue(mod, relativePath))
                return;

            mod.Source.AssetPaths = mod.Source.AssetPaths.Where(x => x != relativeOldPath).ToArray();
            mod.Source.AssetPaths = mod.Source.AssetPaths.Where(x => x != relativePath).ToArray();
            mod.Source.AssetPaths = mod.Source.AssetPaths.Append(relativePath).ToArray();
        };
    }

    private static bool CommonSkipQueue(HotReloadContext mod, string relativePath)
    {
        // Handle special cases
        string extension = Path.GetExtension(relativePath);
        if (reloaderByExtension.TryGetValue(extension, out HotAssetReloader reloader))
        {
            reloader.OnWatcherUpdate(mod, relativePath);

            if (!reloader.QueuePath(relativePath))
                return true;
        }

        if (!supportedExtensions.Contains(extension))
        {
            return true;
        }

        mod.NeedsReload = true;
        return false;
    }

    private static bool IgnoreCompletely(string path)
    {
        // ModCompile.IgnoreCompletely
        return path[0] == '.' || path.StartsWith("bin/") || path.StartsWith("obj/");
    }
}