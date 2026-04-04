using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Daybreak.Common.Features.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Core;
using Terraria;
using Terraria.ModLoader;

namespace RadiantRevival.Common.SmoothLighting;

/// <summary>
///     Scoped override to apply a smooth lighting shader to some known
///     rendering paths.
/// </summary>
public static class SmoothLightingRenderer
{
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.SmoothLighting.VanillaSmoothLightingSampler.Parameters> EntityLightingShader { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    EntityLightingShader = Assets.SmoothLighting.VanillaSmoothLightingSampler.CreateSmoothLightingShader(),
                }
            ).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data) { }
    }

    private sealed record ApplicationState(
        Vector2 DrawOffset,
        float DrawZoom,
        Texture[] Targets
    );

    private sealed class ApplicationScope : IDisposable
    {
        public ApplicationScope(Vector2 drawOffset, float drawZoom)
        {
            var targets = GetNonNullTextures(Main.instance.GraphicsDevice.GetRenderTargets()).ToArray();
            var state = new ApplicationState(drawOffset, drawZoom, targets);
            currently_applied.Push(state);
        }

        public void Dispose()
        {
            currently_applied.Pop();
        }
    }

    public static bool IsCurrentlyApplied => currently_applied.Count > 0;

    private static readonly Stack<ApplicationState> currently_applied = [];

    public static IDisposable BeginScope(Vector2? drawOffset = null, float? drawZoom = null)
    {
        var screenPosition = Main.screenPosition;

        var off = new Vector2(screenPosition.X % 16, screenPosition.Y % 16);

        return new ApplicationScope(drawOffset ?? off, drawZoom ?? 1f / Main.GameZoomTarget);
    }

#pragma warning disable CA2255
    [ModuleInitializer]
    public static void ApplyHooks()
    {
        MonoModHooks.Add(
            typeof(SpriteBatch).GetMethod(nameof(SpriteBatch.PrepRenderState), BindingFlags.NonPublic | BindingFlags.Instance),
            PrepRenderState_InterceptEffect
        );
    }
#pragma warning restore CA2255

    // TODO: Maybe check if == Main.spriteBatch
    private static void PrepRenderState_InterceptEffect(Action<SpriteBatch> orig, SpriteBatch self)
    {
        // PrepRenderState is called in the SpriteBatch constructor in
        // ImmediateMode and shortly before rendering during a flush in any
        // other mode.  Both cases guarantee we can set customEffect before it's
        // used, which is what we need.
        // I tried hooking SpriteBatch::.ctor but it seemingly wouldn't apply,
        // maybe due to inlining, and the constructors are used everywhere, so I
        // can't selectively re-JIT affected methods.

        if (ShouldInterceptEffect(self, out var state))
        {
            var effect = Data.Instance.EntityLightingShader;
            {
                effect.Parameters.draw_offset = state.DrawOffset;
                effect.Parameters.draw_zoom = state.DrawZoom;
                effect.Parameters.light_map = new HlslSampler2D
                {
                    Sampler = SamplerState.LinearClamp,
                    Texture = LightingBuffers.Instance.TotalLightingBuffer.Target,
                };

                effect.Apply();
            }
            self.customEffect = effect.Shader;
        }

        orig(self);
    }

    private static bool ShouldInterceptEffect(SpriteBatch sb, [NotNullWhen(returnValue: true)] out ApplicationState? state)
    {
        state = null;

        if (!IsCurrentlyApplied)
        {
            return false;
        }

        if (sb != Main.spriteBatch)
        {
            return false;
        }

        if (sb.customEffect is not null)
        {
            return false;
        }

        // TODO: Do we want to keep this?
        if (sb.blendState != BlendState.AlphaBlend)
        {
            return false;
        }

        // Shouldn't be possible.
        if (!currently_applied.TryPeek(out state))
        {
            return false;
        }

        var targets = GetNonNullTextures(Main.instance.GraphicsDevice.renderTargetBindings).ToArray();
        if (!targets.SequenceEqual(state.Targets))
        {
            return false;
        }

        return true;
    }

    private static IEnumerable<Texture> GetNonNullTextures(RenderTargetBinding[] bindings)
    {
        return bindings.Where(x => x.RenderTarget is not null)
                       .Select(x => x.RenderTarget);
    }
}
