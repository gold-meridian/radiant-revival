using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace RadiantRevival.Common.EntityRendering;

internal static class EntityRenderCapture
{
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.EntityLighting.EntityLightingShader.Parameters> EntityLightingShader { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    EntityLightingShader = Assets.EntityLighting.EntityLightingShader.CreateSmoothLightingShader(),
                }
            ).GetAwaiter().GetResult();
        }

        public static void UnloadData(Data data) { }
    }

    private struct DrawCmd
    {
        public Texture2D Tex;
        public Vector2 Pos;
        public Rectangle? Src;
        public Color Color;
        public float Rot;
        public Vector2 Origin;
        public Vector2 Scale;
        public SpriteEffects Fx;

        // Our own data.
        public byte Flags;
    }

    private struct DrawKey
    {
        public Texture2D Tex;
        public Vector2 Pos;
        public Rectangle? Src;
        public float Rot;
        public Vector2 Origin;
        public Vector2 Scale;
        public SpriteEffects Fx;
    }

    private struct BatchState
    {
        public BlendState Blend;
        public SamplerState Sampler;
        public Effect? Effect;
        public Matrix Transform;
    }

    private const int max_draws = 8192;

    // For auto-detecting glowmasks.
    private const int emissive_threshold = 240;

    private static DrawCmd[] buffer = new DrawCmd[max_draws];
    private static int count;

    private static bool inNpcDraw;
    private static Texture[] currentTargets = [];

    private static BatchState batch;

    private static DrawKey prevKey;
    private static Color prevColor;
    private static bool hasPrev;

    [OnLoad]
    private static void ApplyHooks()
    {
        On_Main.DrawNPCs += DrawNpcs_Scope;
        On_Main.EntitySpriteDraw_DrawData += EntitySpriteDraw_DrawData_Capture;
        On_Main.EntitySpriteDraw_Texture2D_Vector2_Nullable1_Color_float_Vector2_float_SpriteEffects_float += EntitySpriteDraw_FloatScale_Capture;
        On_Main.EntitySpriteDraw_Texture2D_Vector2_Nullable1_Color_float_Vector2_Vector2_SpriteEffects_float += EntitySpriteDraw_VectorScale_Capture;

        MonoModHooks.Add(
            typeof(SpriteBatch).GetMethod(nameof(SpriteBatch.PushSprite), BindingFlags.NonPublic | BindingFlags.Instance)!,
            PushSprite_Capture
        );

        foreach (var beginMethod in typeof(SpriteBatch).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(x => x.Name == nameof(SpriteBatch.Begin)))
        {
            MonoModHooks.Modify(
                beginMethod,
                Begin_Flush
            );
        }

        MonoModHooks.Add(
            typeof(SpriteBatch).GetMethod(nameof(SpriteBatch.End), BindingFlags.Public | BindingFlags.Instance)!,
            End_Flush
        );

        MonoModHooks.Add(
            typeof(GraphicsDevice).GetMethod(nameof(GraphicsDevice.SetRenderTargets), BindingFlags.Public | BindingFlags.Instance)!,
            SetRenderTargets
        );

        MonoModHooks.Add(
            typeof(GraphicsDevice).GetMethod(nameof(GraphicsDevice.ApplyState), BindingFlags.NonPublic | BindingFlags.Instance)!,
            ApplyState_Flush
        );
    }

    private static void DrawNpcs_Scope(On_Main.orig_DrawNPCs orig, Main self, bool behindTiles)
    {
        // To force light to be white... easy. TODO no
        using var _ = new ScopeStateCapture<bool>(ref Main.gameMenu);
        Main.gameMenu = true;
        
        try
        {
            inNpcDraw = true;
            orig(self, behindTiles);
            Flush();
        }
        finally
        {
            inNpcDraw = false;
        }
    }

    private static bool CaptureDraw(Texture2D tex, Vector2 pos, Rectangle? src, Color color, float rot, Vector2 origin, Vector2 scale, SpriteEffects fx)
    {
        if (!ShouldCapture())
        {
            return false;
        }

        SubmitDraw(tex, pos, src, color, rot, origin, scale, fx);
        return true;
    }

    private static void SubmitDraw(Texture2D tex, Vector2 pos, Rectangle? src, Color color, float rot, Vector2 origin, Vector2 scale, SpriteEffects fx)
    {
        if (count >= max_draws)
        {
            Flush();
        }

        var emissive = DetectGlow(tex, pos, src, color, rot, origin, scale, fx);
        ref var cmd = ref buffer[count++];
        cmd.Tex = tex;
        cmd.Pos = pos;
        cmd.Src = src;
        cmd.Color = color;
        cmd.Rot = rot;
        cmd.Origin = origin;
        cmd.Scale = scale;
        cmd.Fx = fx;
        cmd.Flags = (byte)(emissive ? 1 : 0);
    }

    private static void EntitySpriteDraw_DrawData_Capture(
        On_Main.orig_EntitySpriteDraw_DrawData orig,
        DrawData data
    )
    {
        if (CaptureDraw(data.texture, data.position, data.sourceRect, data.color, data.rotation, data.origin, data.scale, data.effect))
        {
            return;
        }

        orig(data);
    }

    private static void EntitySpriteDraw_FloatScale_Capture(
        On_Main.orig_EntitySpriteDraw_Texture2D_Vector2_Nullable1_Color_float_Vector2_float_SpriteEffects_float orig,
        Texture2D texture,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        SpriteEffects effects,
        float worthless
    )
    {
        if (CaptureDraw(texture, position, sourceRectangle, color, rotation, origin, new Vector2(scale), effects))
        {
            return;
        }

        orig(
            texture,
            position,
            sourceRectangle,
            color,
            rotation,
            origin,
            scale,
            effects,
            worthless
        );
    }

    private static void EntitySpriteDraw_VectorScale_Capture(
        On_Main.orig_EntitySpriteDraw_Texture2D_Vector2_Nullable1_Color_float_Vector2_Vector2_SpriteEffects_float orig,
        Texture2D texture,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color color,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects,
        float worthless
    )
    {
        if (CaptureDraw(texture, position, sourceRectangle, color, rotation, origin, scale, effects))
        {
            return;
        }

        orig(
            texture,
            position,
            sourceRectangle,
            color,
            rotation,
            origin,
            scale,
            effects,
            worthless
        );
    }

    private delegate void OrigPushSprite(
        SpriteBatch self,
        Texture2D texture,
        float sourceX,
        float sourceY,
        float sourceW,
        float sourceH,
        float destinationX,
        float destinationY,
        float destinationW,
        float destinationH,
        Color color,
        float originX,
        float originY,
        float rotationSin,
        float rotationCos,
        float depth,
        byte effects
    );

    private static void PushSprite_Capture(
        OrigPushSprite orig,
        SpriteBatch self,
        Texture2D texture,
        float sourceX,
        float sourceY,
        float sourceW,
        float sourceH,
        float destinationX,
        float destinationY,
        float destinationW,
        float destinationH,
        Color color,
        float originX,
        float originY,
        float rotationSin,
        float rotationCos,
        float depth,
        byte effects
    )
    {
        if (self == Main.spriteBatch && ShouldCapture())
        {
            // reconstruct source rectangle (pixel space)
            var src = new Rectangle(
                (int)(sourceX * texture.Width),
                (int)(sourceY * texture.Height),
                (int)(sourceW * texture.Width),
                (int)(sourceH * texture.Height)
            );

            // reconstruct rotation
            var rotation = MathF.Atan2(rotationSin, rotationCos);

            // reconstruct scale
            var scale = new Vector2(destinationW, destinationH) / new Vector2(src.Width, src.Height);

            // reconstruct origin (pixel space)
            var origin = new Vector2(
                originX * sourceW * texture.Width,
                originY * sourceH * texture.Height
            );

            var fx = (SpriteEffects)(effects & 0x3);

            SubmitDraw(texture, new Vector2(destinationX, destinationY), src, color, rotation, origin, scale, fx);
            return;
        }

        orig(
            self,
            texture,
            sourceX,
            sourceY,
            sourceW,
            sourceH,
            destinationX,
            destinationY,
            destinationW,
            destinationH,
            color,
            originX,
            originY,
            rotationSin,
            rotationCos,
            depth,
            effects
        );
    }

    // TODO: Maybe check if == Main.spriteBatch
    private static void Begin_Flush(ILContext il)
    {
        var c = new ILCursor(il);

        // All the other Begins forward to a single Begin with the parameters we
        // actually care about.
        if (c.TryGotoNext(x => x.MatchCallOrCallvirt<SpriteBatch>(nameof(SpriteBatch.Begin))))
        {
            return;
        }

        /*
        c.Index = 0;

        c.EmitLdarg(2); // blendState
        c.EmitLdarg(3); // samplerState
        c.EmitLdarg(6); // effect
        c.EmitLdarg(7); // transformMatrix
        c.EmitDelegate(
            (
                BlendState blendState,
                SamplerState samplerState,
                Effect effect,
                Matrix transformMatrix
            ) =>
            {
                Flush();

                batch.Blend = blendState ?? BlendState.AlphaBlend;
            }
        );
        */

        c.Index = 0;
        {
            c.EmitDelegate(
                Flush
            );
        }

        c.Next = null;
        c.GotoPrev(MoveType.Before, x => x.MatchRet());
        {
            c.EmitLdarg0();
            c.EmitDelegate(
                (SpriteBatch self) =>
                {
                    batch.Blend = self.blendState;
                    batch.Sampler = self.samplerState;
                    batch.Effect = self.customEffect;
                    batch.Transform = self.transformMatrix;
                }
            );
        }
    }

    private static void End_Flush(Action<SpriteBatch> orig, SpriteBatch self)
    {
        Flush();
        orig(self);
    }

    private static void SetRenderTargets(Action<GraphicsDevice, RenderTargetBinding[]> orig, GraphicsDevice self, RenderTargetBinding[]? renderTargets)
    {
        // null means none
        renderTargets ??= [];

        if (!currentTargets.SequenceEqual(renderTargets.Select(x => x.RenderTarget)))
        {
            Flush();
        }

        currentTargets = renderTargets.Select(x => x.RenderTarget).ToArray();
        orig(self, renderTargets);
    }

    private static void ApplyState_Flush(Action<GraphicsDevice> orig, GraphicsDevice self)
    {
        Flush();
        orig(self);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldCapture()
    {
        if (!inNpcDraw)
        {
            return false;
        }

        if (!IsScreenTarget())
        {
            return false;
        }

        if (batch.Blend != BlendState.AlphaBlend)
        {
            return false;
        }

        if (batch.Effect != null)
        {
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsScreenTarget()
    {
        return currentTargets.Length == 0 || (currentTargets.Length == 1 && currentTargets[0] == Main.screenTarget);
    }

    private static bool DetectGlow(
        Texture2D tex,
        Vector2 pos,
        Rectangle? src,
        Color color,
        float rot,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects fx
    )
    {
        var result = false;

        if (hasPrev)
        {
            if (prevKey.Tex != tex
             && prevKey.Pos == pos
             && prevKey.Src == src
             && prevKey.Rot == rot
             && prevKey.Origin == origin
             && prevKey.Scale == scale
             && prevKey.Fx == fx)
            {
                var curr = Brightness(color);
                var prev = Brightness(prevColor);

                if (curr >= emissive_threshold && curr > prev)
                {
                    result = true;
                }
            }
        }

        prevKey.Tex = tex;
        prevKey.Pos = pos;
        prevKey.Src = src;
        prevKey.Rot = rot;
        prevKey.Origin = origin;
        prevKey.Scale = scale;
        prevKey.Fx = fx;

        prevColor = color;
        hasPrev = true;

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Brightness(Color c)
    {
        var r = c.R;
        var g = c.G;
        var b = c.B;
        return r > g ? (r > b ? r : b) : (g > b ? g : b);
    }

    private static void Flush()
    {
        if (count == 0)
        {
            return;
        }

        if (!inNpcDraw)
        {
            return;
        }
        
        using var _ = new ScopeStateCapture<bool>(ref inNpcDraw);
        inNpcDraw = false;

        var effect = Data.Instance.EntityLightingShader;
        effect.Parameters.light_map = new HlslSampler2D
        {
            Sampler = SamplerState.LinearClamp,
            Texture = LightingBuffers.Instance.ScreenSizeLightingBuffer.Target,
        };
        effect.Apply();
        // effect.Parameters.Apply(effect.Shader.Parameters, effect._passName);

        using (Main.spriteBatch.Scope())
        {
            // Lit pass
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                effect.Shader,
                Main.GameViewMatrix.TransformationMatrix
            );

            for (var i = 0; i < count; i++)
            {
                ref var d = ref buffer[i];
                if ((d.Flags & 1) != 0)
                {
                    continue;
                }

                Main.spriteBatch.Draw(
                    d.Tex,
                    d.Pos,
                    d.Src,
                    d.Color,
                    d.Rot,
                    d.Origin,
                    d.Scale,
                    d.Fx,
                    0f
                );
            }

            Main.spriteBatch.End();

            // Emissive pass
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            for (var i = 0; i < count; i++)
            {
                ref var d = ref buffer[i];
                if ((d.Flags & 1) == 0)
                {
                    continue;
                }

                Main.spriteBatch.Draw(
                    d.Tex,
                    d.Pos,
                    d.Src,
                    d.Color,
                    d.Rot,
                    d.Origin,
                    d.Scale,
                    d.Fx,
                    0f
                );
            }

            Main.spriteBatch.End();

            count = 0;
            hasPrev = false;
        }
    }
}
