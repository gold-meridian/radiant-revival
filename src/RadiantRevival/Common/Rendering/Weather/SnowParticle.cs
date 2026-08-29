using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using Terraria;

namespace RadiantRevival.Common.Rendering.Weather;

/// <summary>
///     Represents an instance of a snow particle.
/// </summary>
public struct SnowParticle
{
    /// <summary>
    ///     How long this snowflake has
    ///     existed, in frames.
    /// </summary>
    public int Timer
    {
        get;
        set;
    }

    /// <summary>
    ///     How long this snowflake should exist
    ///     for, in frames.
    /// </summary>
    public int Lifetime
    {
        get;
        set;
    }

    /// <summary>
    ///     The centered world position of
    ///     this snowflake.
    /// </summary>
    public Vector3 Position
    {
        get;
        set;
    }

    /// <summary>
    ///     The velocity of this
    ///     snowflake.
    /// </summary>
    public Vector3 Velocity
    {
        get;
        set;
    }

    /// <summary>
    ///     The scale of this snowflake.
    /// </summary>
    public Vector2 Scale
    {
        get;
        set;
    }

    /// <summary>
    ///     The rotation of this snowflake.
    /// </summary>
    public Quaternion Rotation
    {
        get;
        set;
    }

    /// <summary>
    ///     Updates this snowflake.
    /// </summary>
    public void Update()
    {
        var spin =
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, Velocity.Y * 0.03f) *
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, Velocity.X * 0.03f);
        Rotation = Quaternion.Normalize(Rotation * spin);
        if (Velocity.Length() >= 7f)
            Velocity *= 0.984f;

        var waveForce = Utils.GetLerpValue(0.1f, 0.6f, MathF.Abs(Main.windSpeedCurrent), true) * 0.125f;
        Velocity += Vector3.UnitY * MathF.Cos(Position.X * 0.005f + Position.Z * 0.002f) * waveForce;

        Position += Velocity;

        if (Position.Z < 320f && !CustomSnow.ForegroundParticlesAllowed)
        {
            Position += Vector3.UnitZ * 12f;
            Velocity *= new Vector3(1f, 1f, 0.9f);
        }

        Timer++;
    }

    /// <summary>
    ///     Determines whether this snowflake
    ///     should be considered killed or not, and
    ///     resets instance data if so.
    /// </summary>
    public bool KillIfNecessary()
    {
        if (Timer >= Lifetime)
        {
            Timer = 0;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Prepares this snowflake for rendering.
    /// </summary>
    internal void PrepareRender(Span<CustomSnow.Vertex> span)
    {
        Debug.Assert(span.Length == 4);

        var right = Vector3.Transform(Vector3.UnitX * Scale.X, Rotation);
        var down = Vector3.Transform(Vector3.UnitY * Scale.Y, Rotation);
        var rotation = new Vector4(Rotation.X, Rotation.Y, Rotation.Z, Rotation.W);

        var topLeft = Position - right - down;
        var topRight = Position + right - down;
        var bottomLeft = Position - right + down;
        var bottomRight = Position + right + down;

        var color = Color.White;

        var lifetimeRatio = Timer / (float)Lifetime;
        var opacity = Utils.GetLerpValue(1f, 0.6f, lifetimeRatio, true);
        color *= opacity;

        span[0] = new(topLeft, color, rotation, Vector2.Zero);
        span[1] = new(topRight, color, rotation, Vector2.UnitX);
        span[2] = new(bottomRight, color, rotation, Vector2.One);
        span[3] = new(bottomLeft, color, rotation, Vector2.UnitY);
    }
}
