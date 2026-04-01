using System;
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

    private sealed class ApplicationScope : IDisposable
    {
        public ApplicationScope()
        {
            currentlyApplied++;
        }

        public void Dispose()
        {
            currentlyApplied--;
        }
    }

    public static bool IsCurrentlyApplied => currentlyApplied > 0;

    private static int currentlyApplied;

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

        var targets = Main.instance.GraphicsDevice.renderTargetBindings
                          .Where(x => x.RenderTarget is not null)
                          .Select(x => x.RenderTarget)
                          .ToArray();
        if (targets.Length != 0 && (targets.Length != 1 || targets[0] != Main.screenTarget))
        {
            return false;
        }

        return true;
    }
}
