using System.Collections.Generic;
using Terraria.Graphics;

namespace RadiantRevival.Common;

/// <summary>
///     An arbitrary step extending vanilla's target rendering pipeline.
/// </summary>
public interface IVanillaPipelineStep
{
    /// <summary>
    ///     Input targets which, when modified, indicates that
    ///     <see cref="Apply" /> should be run.
    /// </summary>
    List<WorldSceneLayerTarget> Inputs { get; }

    /// <summary>
    ///     Mutates vanilla targets, returning the mutated targets.
    /// </summary>
    /// <remarks>
    ///     API consumers *must* report any mutated targets.  Built-in state
    ///     tracking only applies to vanilla operations.
    /// </remarks>
    List<WorldSceneLayerTarget> Apply();
}
