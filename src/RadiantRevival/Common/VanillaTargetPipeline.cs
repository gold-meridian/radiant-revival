using System;
using Daybreak.Common.Features.Hooks;
using GoldMeridian.CodeAnalysis;
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
    [ExtensionDataFor<WorldSceneLayerTarget>]
    public sealed class WorldSceneLayerTargetStateTracking
    {
        public bool TrackingContentUpdates { get; set; }

        public bool ContentUpdated { get; set; }
    }

    extension(WorldSceneLayerTarget target)
    {
        public bool ContentUpdated => target.StateTracking is { TrackingContentUpdates: true, ContentUpdated: true };
    }

    private readonly ref struct ContentUpdateTracker : IDisposable
    {
        private readonly WorldSceneLayerTarget target;

        public ContentUpdateTracker(WorldSceneLayerTarget target)
        {
            this.target = target;
            ResetState(tracking: true);
        }

        public void Dispose()
        {
            ResetState(tracking: false);
        }

        private void ResetState(bool tracking)
        {
            target.StateTracking ??= new WorldSceneLayerTargetStateTracking();
            target.StateTracking.TrackingContentUpdates = tracking;
            target.StateTracking.ContentUpdated = false;
        }
    }

    private static readonly ITargetPipelineStep[] steps = [
        
    ];

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

        using (new ContentUpdateTracker(Main.backWaterTarget))
        using (new ContentUpdateTracker(Main.waterTarget))
        using (new ContentUpdateTracker(Main.tileTarget))
        using (new ContentUpdateTracker(Main.tile2Target))
        using (new ContentUpdateTracker(Main.wallTarget))
        using (new ContentUpdateTracker(Main.backgroundTarget))
        using (new ContentUpdateTracker(Main.backgroundTargetSwap))
        {
            orig(self);
            ApplyPipeline();
        }
    }

    private static void UpdateContent_RecordChange(On_WorldSceneLayerTarget.orig_UpdateContent orig, WorldSceneLayerTarget self, Action render)
    {
        orig(self, render);
        self.StateTracking?.ContentUpdated = true;
    }

    private static void ApplyPipeline()
    {
        
    }
}
