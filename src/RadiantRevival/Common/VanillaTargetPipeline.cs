using System;
using System.Collections.Generic;
using Daybreak.Common.Features.Hooks;
using Terraria;
using Terraria.Graphics;

namespace RadiantRevival.Common;

/// <summary>
///     An arbitrary step in the vanilla-wrapped target pipeline.
/// </summary>
internal interface ITargetPipelineStep
{
    /// <summary>
    ///     Input targets which, when modified, indicates that
    ///     <see cref="Apply"/> should be run.
    /// </summary>
    ReadOnlySpan<WorldSceneLayerTarget> Inputs { get; }

    /// <summary>
    ///     Mutates vanilla targets, returning the mutated targets.
    /// </summary>
    /// <remarks>
    ///     API consumers *must* report any mutated targets.  Built-in state
    ///     tracking only applies to vanilla operations.
    /// </remarks>
    ReadOnlySpan<WorldSceneLayerTarget> Apply();
}

/// <summary>
///     Wraps vanilla rendering to track state changes within its targets.
///     <br />
///     Applies an arbitrary set of pipeline steps after-the-fact to facilitate
///     mutating rendered targets.
/// </summary>
internal static class VanillaTargetPipeline
{
    private static readonly ITargetPipelineStep[] steps = [];

    private static List<WorldSceneLayerTarget>? mutatedTargets;

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

    private static void ApplyPipeline() { }
}
