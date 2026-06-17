using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace RadiantRevival.Common.Rendering.Sky;

/// <summary>
///     A profile that contains all relevant artistic dials
///     for the purposes of tuning the atmosphere, clouds, etc.
///     based on some contextual influence, such as a player
///     being in a specific biome.
/// </summary>
public sealed record SkyProfileInfluence
{
    /// <summary>
    ///     The priority that this influence should have
    ///     relative to all others. Greater values mean
    ///     that this profile goes into effect later, and
    ///     thus overlaps profiles with a lesser priority.
    /// </summary>
    public int InfluencePriority
    {
        get;
        init;
    }

    /// <summary>
    ///     An optionally overrideable value that
    ///     can be used to dictate contextual surface
    ///     temperature values for cloud formation.
    /// </summary>
    /// <remarks>
    ///     Temperature values are expected to be in
    ///     Fahrenheit. Refer to <see cref="DensityFieldSystem.StandardSurfaceTemperature"/>
    ///     for a sane baseline.
    /// </remarks>
    public float? OverridingSurfaceTemperature
    {
        get;
        init;
    }

    /// <summary>
    ///     The color that auroras in the snow biome should
    ///     be tinted in the red channel.
    /// </summary>
    public Vector3 RedTermAuroraTint
    {
        get;
        init;
    }

    /// <summary>
    ///     The color that auroras in the snow biome should
    ///     be tinted in the green channel.
    /// </summary>
    public Vector3 GreenTermAuroraTint
    {
        get;
        init;
    }

    /// <summary>
    ///     The color that auroras in the snow biome should
    ///     be tinted in the blue channel.
    /// </summary>
    public Vector3 BlueTermAuroraTint
    {
        get;
        init;
    }

    /// <summary>
    ///     The color that natural rainbows should be tinted.
    /// </summary>
    public required Color RainbowTintColor
    {
        get;
        init;
    }

    /// <summary>
    ///     The color that the atmosphere should be tinted.
    /// </summary>
    public required Vector3 AtmosphereTintColor
    {
        get;
        init;
    }

    /// <summary>
    ///     The function that dictates the overall influence
    ///     of the effects in this profile.
    /// </summary>
    /// <remarks>
    ///     It is expected that this function will return values
    ///     ranging from zero to one.
    /// </remarks>
    public Func<Player, float> InfluenceFunction
    {
        get;
        private set;
    }

    public SkyProfileInfluence(Func<Player, float> influenceFunction) => InfluenceFunction = influenceFunction;
}
