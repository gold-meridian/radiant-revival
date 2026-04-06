using System;
using System.Collections.Generic;
using System.Linq;
using Daybreak.Common.Features.Hooks;
using RadiantRevival.Common.SmoothLighting;
using Terraria;
using Terraria.Graphics;

namespace RadiantRevival.Common;

/// <summary>
///     Wraps vanilla rendering to track state changes within its targets.
///     <br />
///     Applies an arbitrary set of pipeline steps after-the-fact to facilitate
///     mutating rendered targets.
/// </summary>
public static class VanillaTargetRenderer
{
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

        foreach (var step in steps)
        {
            if (!step.Inputs.Any(mutatedSet.Contains))
            {
                continue;
            }

            var mutatedNew = step.Apply();
            foreach (var mutated in mutatedNew)
            {
                mutatedSet.Add(mutated);
            }
        }
    }
}
