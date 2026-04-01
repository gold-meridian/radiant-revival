using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Daybreak.Common.Features.Hooks;
using GoldMeridian.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RadiantRevival.Common.CodeModel;
using ReLogic.Threading;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ModLoader;
using Terraria.UI;

namespace RadiantRevival.Common;

/// <summary>
///     This is responsible for rewriting <see cref="LightMap"/>.  It changes
///     the backing color store from an array of <see cref="Vector3"/>s to an
///     array of <see cref="Vector4"/>s for trivial buffer uploads to the GPU,
///     and changes the layout of the buffers from column-major to row-major
///     to avoid needing to transform coordinates in shaders.
/// </summary>
internal static class LightMapRewrite
{
    [ExtensionDataFor<LightMap>]
    public sealed class NewData
    {
        public required Vector4[] Colors { get; set; }

        public Texture2D? BufferTexture { get; set; }

        public bool BufferNeedsUpdating { get; set; }
    }

#pragma warning disable CA2255
    [ModuleInitializer]
    public static void RewriteImplementations()
    {
        On_LightMap.BlurPass += (_, self) =>
        {
            var rowStride = self.Width + 1;
            FastParallel.For(
                0,
                self.Width,
                (start, end, _) =>
                {
                    for (var j = start; j < end; j++)
                    {
                        self.BlurLine(self.IndexOf(j, 0), self.IndexOf(j, self.Height - 1 - self.NonVisiblePadding), rowStride);
                        self.BlurLine(self.IndexOf(j, self.Height - 1), self.IndexOf(j, self.NonVisiblePadding), -rowStride);
                    }
                }
            );

            FastParallel.For(
                0,
                self.Height,
                (start, end, _) =>
                {
                    for (var i = start; i < end; i++)
                    {
                        self.BlurLine(self.IndexOf(0, i), self.IndexOf(self.Width - 1 - self.NonVisiblePadding, i), 1);
                        self.BlurLine(self.IndexOf(self.Width - 1, i), self.IndexOf(self.NonVisiblePadding, i), -1);
                    }
                }
            );
        };
    }
#pragma warning restore CA2255

    [OnLoad]
    private static void OutlineImplementations(Mod mod)
    {
        // LightMap::.ctor
        //           GetLight
        //           GetMask
        //           Clear
        //           SetMaskAt
        //           Blur
        //           BlurPass
        //           IndexOf
        //           SetSize
        
        // LightMap::.ctor
        //   Called in LegacyLighting/LightingEngine constructors, but that's
        //   fine to leave alone.
        MethodJit.ForceJit(typeof(LightingEngine).GetMethod(nameof(LightingEngine.Rebuild), BindingFlags.Public | BindingFlags.Instance)!);
    }

    /*
    private static NewData GetOrInitData(LightMap lightMap)
    {
        if (lightMap.NewData is not null)
        {
            return lightMap.NewData;
        }

        return lightMap.NewData
    }
    */

    // public static LightingBuffer GetGpuBuffer(LightMap lightMap) { }

    [ModSystemHooks.ModifyInterfaceLayers]
    private static void DebugDrawBuffer(List<GameInterfaceLayer> layers)
    {
        layers.Add(
            new LegacyGameInterfaceLayer(
                "abc",
                () =>
                {
                    var a = Lighting.GetGpuBuffer();
                    Main.spriteBatch.Draw(a.Texture, new Vector2(256f), Color.White);
                    return true;
                },
                InterfaceScaleType.None
            )
        );
    }
}
