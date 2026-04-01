using Daybreak.Common.Features.Hooks;
using ReLogic.Content.Readers;
using System;
using System.Reflection;
using Terraria.ModLoader;

namespace BlockarozToolkit.Common.Systems.AssetReloading;

/// <summary>
///     Makes sure PNG images 
/// </summary>
internal static class TextureLoadFix
{
    [OnLoad]
    private static void HookPremultiply()
    {
        MonoModHooks.Add(
            typeof(PngReader).GetMethod(nameof(PngReader.PreMultiplyAlpha), BindingFlags.NonPublic | BindingFlags.Static),
            PreMultiplyAlpha_SkipIfLoadingFromMod
        );
    }

    private static void PreMultiplyAlpha_SkipIfLoadingFromMod(Action<nint, int> orig, nint img, int len)
    {
        if (!HotAssetSystem.IsReloadingAssetsRightNow)
            orig(img, len);
    }
}