using System;
using System.Collections.Generic;
using System.Linq;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using RadiantRevival.Common.SmoothLighting;
using Terraria;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

public readonly record struct VanillaTargetRendererContext(
    RenderTargetLease WorldSceneTargetSwap
);

/// <summary>
///     Wraps vanilla rendering to track state changes within its targets.
///     <br />
///     Applies an arbitrary set of pipeline steps after-the-fact to facilitate
///     mutating rendered targets.
/// </summary>
public static class VanillaTargetRenderer
{
    private sealed class Data : IStatic<Data>
    {
        public required RenderTargetLease WorldSceneTargetSwap { get; set; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    WorldSceneTargetSwap = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, (_, _, targetWidth, targetHeight) => (targetWidth, targetHeight)),
                }
            ).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data)
        {
            Main.RunOnMainThread(
                () =>
                {
                    data.WorldSceneTargetSwap.Dispose();
                }
            );
        }
    }

    private static readonly IVanillaPipelineStep[] steps =
    [
        new AmbientOcclusion.WallRenderer(),
    ];

    private static HashSet<WorldSceneLayerTarget>? mutatedTargets;

    [OnLoad]
    private static void ApplyHooks()
    {
        On_Main.RenderToTargets += RenderToTargets_TrackStateChangesAndApplyPipeline;
        On_WorldSceneLayerTarget.UpdateContent += UpdateContent_RecordChange;
    }

    private static void RenderToTargets_TrackStateChangesAndApplyPipeline(On_Main.orig_RenderToTargets orig, Main self)
    {
        // TODO: Vanilla checks for this in the real method.  Do we need to
        //       respect this when handling our pipelines?
        if (!Main.render)
        {
            return;
        }

        mutatedTargets = [];

        try
        {
            orig(self);
            ApplyPipeline();
        }
        finally
        {
            mutatedTargets.Clear();
            mutatedTargets = null;
        }
    }

    private static void UpdateContent_RecordChange(On_WorldSceneLayerTarget.orig_UpdateContent orig, WorldSceneLayerTarget self, Action render)
    {
        orig(self, render);
        mutatedTargets?.Add(self);
    }

    private static void ApplyPipeline()
    {
        var mutatedSet = new HashSet<WorldSceneLayerTarget>(mutatedTargets ?? []);

        var ctx = new VanillaTargetRendererContext(
            Data.Instance.WorldSceneTargetSwap
        );

        foreach (var step in steps)
        {
            if (!step.Inputs.Any(mutatedSet.Contains))
            {
                continue;
            }

            var mutatedNew = step.Apply(in ctx);
            foreach (var mutated in mutatedNew)
            {
                mutatedSet.Add(mutated);
            }
        }
    }
}
