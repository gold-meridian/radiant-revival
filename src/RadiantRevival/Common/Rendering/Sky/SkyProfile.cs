using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace RadiantRevival.Common.Rendering.Sky;

/// <summary>
///     A profile that contains all relevant artistic dials
///     for the purposes of tuning the atmosphere, clouds, etc.
/// </summary>
public sealed record SkyProfile
{
    /// <summary>
    ///     The amount by which the background should be upwardly
    ///     saturated. This is not realistically accurate, but artistically
    ///     it's cool and can help the colors feel a bit more vivid.
    /// </summary>
    public required float AtmosphereSaturationBoost
    {
        get;
        init;
    }

    /// <summary>
    ///     The factor by which clouds should be saturated relative
    ///     to the baseline background color.
    /// </summary>
    public required float CloudSaturationFactor
    {
        get;
        init;
    }

    /// <summary>
    ///     The color used for tinting the tiles and background
    ///     as the sun is low (e.g. during sunrise or sunset).
    /// </summary>
    public required Color LowSunTintColor
    {
        get;
        init;
    }

    /// <summary>
    ///     The color tint that should be applied to clouds the
    ///     stronger rain currently is.
    /// </summary>
    public required Vector3 CloudRainColorTint
    {
        get;
        init;
    }

    /// <summary>
    ///     The function which dictates the additive color
    ///     to apply to clouds in response to a low sun angle, such
    ///     as during sunrise or sunset.
    /// </summary>
    public required LowSunColorExaggerationDelegate LowSunColorExaggerationFunction
    {
        get;
        init;
    }

    /// <summary>
    ///     The component-wise RGB -> wavelength mappings of light.
    /// </summary>
    public required Vector3 ColorWavelengthsNanometers
    {
        get;
        init;
    }

    /// <summary>
    ///     The set of all contextual influences that apply
    ///     to this global profile.
    /// </summary>
    public List<SkyProfileInfluence> Influences
    {
        get;
        private set;
    } = [];

    public delegate Vector3 LowSunColorExaggerationDelegate(float lowSun);

    public SkyProfile(params SkyProfileInfluence[] influences) => Influences = [.. influences.OrderBy(i => i.InfluencePriority)];
}
