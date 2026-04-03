using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Daybreak.Common.Features.Models;
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

    private sealed record ApplicationState(Texture[] Targets);

    private sealed class ApplicationScope : IDisposable
    {
        public ApplicationScope()
        {
            var targets = GetNonNullTextures(Main.instance.GraphicsDevice.GetRenderTargets()).ToArray();
            var state = new ApplicationState(targets);
            currently_applied.Push(state);
        }

        public void Dispose()
        {
            currently_applied.Pop();
        }
    }

    public static bool IsCurrentlyApplied => currently_applied.Count > 0;

    private static readonly Stack<ApplicationState> currently_applied = [];

    public static IDisposable BeginScope()
    {
        return new ApplicationScope();
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

        if (ShouldInterceptEffect(self))
        {
            var effect = Data.Instance.EntityLightingShader;
            {
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

    private static bool ShouldInterceptEffect(SpriteBatch sb)
    {
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
        if (!currently_applied.TryPeek(out var state))
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
