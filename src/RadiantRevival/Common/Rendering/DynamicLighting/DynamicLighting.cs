using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Models;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Core;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Events;
using Terraria.GameContent.LeashedEntities;
using Terraria.Graphics.Light;
using Terraria.Graphics.Renderers;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Default.Developer.Jofairden;
using Terraria.ModLoader.Default.Patreon;
using Terraria.ModLoader.UI;
using BitOperations = System.Numerics.BitOperations;

namespace RadiantRevival.Common;

public static class DynamicLighting
{
    private sealed class Data : IStatic<Data>
    {
        public required WrapperShaderData<Assets.DynamicLighting.RadialBlurShader.Parameters> RadialBlurShader { get; init; }

        public required RenderTargetLease LightTarget { get; init; }

        public required RenderTargetLease LightTargetSwap { get; init; }

        public static Data LoadData(Mod mod)
        {
            return Main.RunOnMainThread(
                () => new Data
                {
                    RadialBlurShader = Assets.DynamicLighting.RadialBlurShader.CreateRadialBlurShader(),
                    LightTarget = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, GetLightTargetSize),
                    LightTargetSwap = ScreenspaceTargetPool.Shared.Rent(Main.instance.GraphicsDevice, GetLightTargetSize),
                }
            ).GetAwaiter().GetResult();

            static (int w, int h) GetLightTargetSize(int width, int height, int targetWidth, int targetHeight)
            {
                return (width / 2, height / 2);
            }
        }

        public static void UnloadData(Data data)
        {
            Main.RunOnMainThread(
                () =>
                {
                    data.LightTarget.Dispose();
                    data.LightTargetSwap.Dispose();
                }
            );
        }
    }

    public static RenderTargetLease LightTarget => Data.Instance.LightTarget;

    public static RenderTargetLease LightTargetSwap => Data.Instance.LightTargetSwap;

    private static bool currentlyApplied;

    private sealed class ApplicationScope : IDisposable
    {
        private readonly bool wasApplied;

        public ApplicationScope()
        {
            wasApplied = currentlyApplied;
            currentlyApplied = true;
        }

        public void Dispose()
        {
            currentlyApplied = wasApplied;
        }
    }

    public static IDisposable BeginScope()
    {
        return new ApplicationScope();
    }

    public static void Scope(Action callback)
    {
        using (BeginScope())
        {
            callback();
        }
    }

    private const int bits_per_chunk = sizeof(ulong) * 8;

    private readonly record struct Light(Vector2 Position, Color Color, float Size);

    private const int max_lights = 512;

    private static readonly Light[] lights = new Light[max_lights];
    private static readonly ulong[] lights_mask = new ulong[(int)Math.Ceiling((double)max_lights / bits_per_chunk)];

    private static readonly (Type, string)[] methods_to_rejit =
    [
        (typeof(DelegateMethods), nameof(DelegateMethods.CastLight)),
        (typeof(DelegateMethods), nameof(DelegateMethods.CastLightOpen)),
        (typeof(DelegateMethods), nameof(DelegateMethods.CastLightOpen_StopForSolids_ScaleWithDistance)),
        (typeof(DelegateMethods), nameof(DelegateMethods.CastLightOpen_StopForSolids)),
        (typeof(DelegateMethods), nameof(DelegateMethods.SpreadLightOpen_StopForSolids)),
        (typeof(Dust), nameof(Dust.UpdateDust)),
        (typeof(Gore), nameof(Gore.Update)),
        (typeof(Gore), nameof(Gore.UpdateLightningBunnySparks)),
        (typeof(Main), nameof(Main.DrawProj_DrawExtras)),
        (typeof(Mount), nameof(Mount.Hover)),
        (typeof(Mount), nameof(Mount.UpdateFrame)),
        (typeof(Mount), nameof(Mount.UpdateEffects)),
        (typeof(Mount), nameof(Mount.AimAbility)),
        (typeof(NPC), nameof(NPC.VanillaAI_Inner)),
        (typeof(NPC), nameof(NPC.AI_065_Butterflies)),
        (typeof(NPC), nameof(NPC.AI_120_HallowBoss)),
        (typeof(NPC), nameof(NPC.AI_037_Destroyer)),
        (typeof(NPC), nameof(NPC.AI_117_BloodNautilus)),
        (typeof(NPC), nameof(NPC.AI_112_FairyCritter)),
        (typeof(NPC), nameof(NPC.AI_007_TownEntities)),
        (typeof(NPC), nameof(NPC.AI_005_EaterOfSouls)),
        (typeof(NPC), nameof(NPC.AI_002_FloatingEye)),
        (typeof(NPC), nameof(NPC.AI_003_Fighters)),
        (typeof(NPC), nameof(NPC.AI_001_Slimes)),
        (typeof(NPC), nameof(NPC.AI_127_Pal)),
        (typeof(NPC), nameof(NPC.AI_121_QueenSlime)),
        (typeof(NPC), nameof(NPC.AI_026_Unicorns)),
        (typeof(NPC), nameof(NPC.UpdateNPC_UpdateTrails)),
        (typeof(NPC), nameof(NPC.UpdateNPC_BuffApplyVFX)),
        (typeof(NPC), nameof(NPC.UpdateNPC_CastLights)),
        (typeof(Player), nameof(Player.UpdateBuffs)),
        (typeof(Player), nameof(Player.ApplyEquipFunctional)),
        (typeof(Player), nameof(Player.Update)),
        (typeof(Player), nameof(Player.UpdateArmorLights)),
        (typeof(Player), nameof(Player.PlayerFrame)),
        (typeof(Player), nameof(Player.DashMovement)),
        (typeof(Player), nameof(Player.UpdateArmorSetsOld)),
        (typeof(Player), nameof(Player.WingFrame)),
        (typeof(Player), nameof(Player.ItemCheck_EmitUseVisuals)),
        (typeof(Player), nameof(Player.ItemCheck_EmitHeldItemLight)),
        (typeof(Projectile), nameof(Projectile.ProjLight)),
        (typeof(Projectile), nameof(Projectile.EmitEnchantmentVisualsAt)),
        (typeof(Projectile), nameof(Projectile.VanillaAI)),
        (typeof(Projectile), nameof(Projectile.AI_003_Boomerang)),
        (typeof(Projectile), nameof(Projectile.AI_001)),
        (typeof(Projectile), nameof(Projectile.AI_026)),
        (typeof(Projectile), nameof(Projectile.AI_075)),
        (typeof(Projectile), nameof(Projectile.AI_205_RemoteControlCar)),
        (typeof(Projectile), nameof(Projectile.AI_203_StormLightning)),
        (typeof(Projectile), nameof(Projectile.AI_196_Petal)),
        (typeof(Projectile), nameof(Projectile.AI_195_JimsDrone)),
        (typeof(Projectile), nameof(Projectile.AI_191_TrueNightsEdge)),
        (typeof(Projectile), nameof(Projectile.AI_190_NightsEdge)),
        (typeof(Projectile), nameof(Projectile.AI_182_FinalFractal)),
        (typeof(Projectile), nameof(Projectile.AI_167_SparkleGuitar)),
        (typeof(Projectile), nameof(Projectile.AI_166_Dove)),
        (typeof(Projectile), nameof(Projectile.AI_165_Whip)),
        (typeof(Projectile), nameof(Projectile.AI_067_FreakingPirates)),
        (typeof(Projectile), nameof(Projectile.AI_157_SharpTears)),
        (typeof(Projectile), nameof(Projectile.AI_007_GrapplingHooks)),
        (typeof(Projectile), nameof(Projectile.AI_147_Celeb2Rocket)),
        (typeof(Projectile), nameof(Projectile.AI_130_FlameBurstTower)),
        (typeof(Projectile), nameof(Projectile.AI_204_Digtoise)),
        (typeof(Projectile), nameof(Projectile.AI_199_MeteorOre)),
        (typeof(Projectile), nameof(Projectile.AI_105_SporeSac)),
        (typeof(Projectile), nameof(Projectile.AI_113_TargetSticker)),
        (typeof(Projectile), nameof(Projectile.AI_100_Medusa)),
        (typeof(Projectile), nameof(Projectile.AI_120_StardustGuardian)),
        (typeof(Projectile), nameof(Projectile.AI_019_Spears)),
        (typeof(Projectile), nameof(Projectile.AI_138_ExplosiveTrap)),
        (typeof(Projectile), nameof(Projectile.AI_140_MonkStaffT1)),
        (typeof(Projectile), nameof(Projectile.AI_142_MonkStaffT2And3)),
        (typeof(WaterfallManager), nameof(WaterfallManager.AddLight)),
        (typeof(WorldItem), nameof(WorldItem.UpdateItem)),
        (typeof(WorldItem), nameof(WorldItem.UpdateItem_VisualEffects)),
        (typeof(BloodyExplosionParticle), nameof(BloodyExplosionParticle.Update)),
        (typeof(GasParticle), nameof(GasParticle.Update)),
        (typeof(PotionOfReturnGateHelper), nameof(PotionOfReturnGateHelper.Update)),
        (typeof(VoidLensHelper), nameof(VoidLensHelper.Update)),
        (typeof(FireflyLeashedCritter), nameof(FireflyLeashedCritter.AddLight)),
        (typeof(HellButterflyLeashedCritter), "VisualEffects"),
        (typeof(SnailLeashedCritter), "VisualEffects"),
        (typeof(EmpressButterflyLeashedCritter), "VisualEffects"),
        (typeof(FairyLeashedCritter), "VisualEffects"),
        (typeof(TileDrawing), nameof(TileDrawing.DrawTrees)),
        (typeof(OrianSetEffectPlayer), nameof(OrianSetEffectPlayer.PostUpdate)),
        (typeof(JofairdenArmorEffectPlayer), nameof(JofairdenArmorEffectPlayer.PostUpdate)),
        (typeof(ArmorSetBonuses.Benefits), nameof(ArmorSetBonuses.Benefits.Forbidden)),
        (typeof(ParticleOrchestrator), nameof(ParticleOrchestrator.Spawn_StormLightning)),
        (typeof(ParticleOrchestrator), nameof(ParticleOrchestrator.Spawn_LeafCrystalShot)),
        (typeof(ParticleOrchestrator), nameof(ParticleOrchestrator.Spawn_BlueLightningSmall)),
    ];

#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initialize()
    {
        // It's never enough.
        IL_Lighting.AddLight_int_int_float_float_float += AddLight_TileCoords;
        IL_Lighting.AddLight_int_int_int_float += AddLight_TileCoords;
        IL_Lighting.AddLight_Vector2_Vector3 += AddLight_WorldCoords_Vector3;
        IL_Lighting.AddLight_Vector2_float_float_float += AddLight_WorldCoords_RGB;
        IL_Lighting.AddLight_Vector2_int += AddLight_WorldCoords_Torch;

        IL_Main.DoDraw += DoDraw_DynamicLights;
    }
#pragma warning restore CA2255

    [OnLoad]
    private static void Load()
    {
        Parallel.ForEach(
            methods_to_rejit,
            static value =>
            {
                var (type, name) = value;
#if DEBUG
                Interface.loadMods.SubProgressText = $"{type}::{name}";
#endif
                MonoModHooks.Modify(
                    type.GetMethod(
                        name,
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                    ),
                    _ => { }
                );
            }
        );
#if DEBUG
        Interface.loadMods.SubProgressText = string.Empty;
#endif
    }

    private static void DoDraw_DynamicLights(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(
            MoveType.After,
            i => i.MatchLdcI4(0),
            i => i.MatchStsfld<Main>(nameof(Main.onlyDrawFancyUI))
        );

        c.GotoNext(
            MoveType.Before,
            i => i.MatchCall<Main>(nameof(Main.GetAreaToLight)),
            i => i.MatchCall<Lighting>(nameof(Lighting.LightTiles))
        );

        c.EmitDelegate(RenderLights);

        // REMOVE
        c.GotoNext(
            MoveType.After,
            i => i.MatchCall<MoonlordDeathDrama>(nameof(MoonlordDeathDrama.DrawWhite))
        );

        c.EmitDelegate(
            static () =>
            {
                Main.spriteBatch.Draw(LightTarget.Target, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
            }
        );
    }

    private static void AddLight_WorldCoords_Vector3(ILContext il)
    {
        var c = new ILCursor(il);

        var jumpDynamicLightsTarget = c.DefineLabel();

        c.EmitDelegate(static () => currentlyApplied);

        c.EmitBrfalse(jumpDynamicLightsTarget);

        c.EmitLdarg(0); // position
        c.EmitLdarg(1); // color
        c.EmitDelegate<Action<Vector2, Vector3>>(AddDynamicLight);
        c.EmitRet();

        c.MarkLabel(jumpDynamicLightsTarget);
    }

    private static void AddLight_WorldCoords_RGB(ILContext il)
    {
        var c = new ILCursor(il);

        var jumpDynamicLightsTarget = c.DefineLabel();

        c.EmitDelegate(static () => currentlyApplied);

        c.EmitBrfalse(jumpDynamicLightsTarget);

        c.EmitLdarg(0); // position
        c.EmitLdarg(1); // r
        c.EmitLdarg(2); // g
        c.EmitLdarg(3); // b
        c.EmitDelegate(AddLight);
        c.EmitRet();

        c.MarkLabel(jumpDynamicLightsTarget);

        return;

        static void AddLight(Vector2 position, float r, float g, float b)
        {
            AddDynamicLight(position, new Vector3(r, g, b));
        }
    }

    private static void AddLight_WorldCoords_Torch(ILContext il)
    {
        var c = new ILCursor(il);

        var jumpDynamicLightsTarget = c.DefineLabel();

        c.EmitDelegate(static () => currentlyApplied);

        c.EmitBrfalse(jumpDynamicLightsTarget);

        c.EmitLdarg(0); // position
        c.EmitLdarg(1); // torchId
        c.EmitDelegate(AddLight);
        c.EmitRet();

        c.MarkLabel(jumpDynamicLightsTarget);

        return;

        static void AddLight(Vector2 position, int torchId)
        {
            TorchID.TorchColor(torchId, out var r, out var g, out var b);

            AddDynamicLight(position, new Vector3(r, g, b));
        }
    }

    private static void AddLight_TileCoords(ILContext il)
    {
        var c = new ILCursor(il);

        var jumpDynamicLightsTarget = c.DefineLabel();

        c.GotoNext(
            MoveType.Before,
            i => i.MatchCallvirt<ILightingEngine>(nameof(ILightingEngine.AddLight))
        );

        c.EmitDelegate(static () => currentlyApplied);

        c.EmitBrfalse(jumpDynamicLightsTarget);

        c.EmitDelegate(AddLight);
        c.EmitPop();
        c.EmitRet();

        c.MarkLabel(jumpDynamicLightsTarget);

        return;

        static void AddLight(int i, int j, Vector3 color)
        {
            AddDynamicLight(new Point(i, j).ToWorldCoordinates(), color);
        }
    }

    public static void AddDynamicLight(Vector2 position, Vector3 color)
    {
        var size = Lighting.GlobalBrightness * (color.X + color.Y + color.Z) / 3f;

        var col = new Color(color.X, color.Y, color.Z);

        AddDynamicLight(position, col, size);
    }

    public static void AddDynamicLight(Vector2 position, Color color, float size)
    {
        var index = GetFirstInactive();

        if (index == -1)
        {
            return;
        }

        if (color is { R: <= 0, G: <= 0, B: <= 0 })
        {
            return;
        }

        lights[index] = new Light(position, color, size);

        var chunkIndex = index / bits_per_chunk;
        var bitIndex = index % bits_per_chunk;
        lights_mask[chunkIndex] ^= 1uL << bitIndex;

        return;

        static int GetFirstInactive()
        {
            for (var i = 0; i < lights_mask.Length; i++)
            {
                var offset = BitOperations.TrailingZeroCount(~lights_mask[i]);

                var allBitsAreOccupied = offset == bits_per_chunk;

                if (allBitsAreOccupied)
                {
                    continue;
                }

                return offset + i * bits_per_chunk;
            }

            return -1;
        }
    }

    private static void RenderLights()
    {
        var sb = Main.spriteBatch;
        var device = Main.graphics.GraphicsDevice;

        var radialBlurShader = Data.Instance.RadialBlurShader;

        using var _ = sb.Scope();

        // Just makes the UVs a little nicer to handle in the shader
        using (LightTargetSwap.Scope(clearColor: Color.Transparent))
        {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone);
            {
                var position = Main.tileTarget.Position - Main.screenPosition;
                position *= 0.5f;

                sb.Draw(Main.tileTarget.Texture, position, null, Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            }
            sb.End();
        }

        using (LightTarget.Scope(clearColor: Color.Transparent))
        {
            radialBlurShader.Parameters.SampleCount = 16;
            radialBlurShader.Parameters.DecayMult = 0.99f;
            radialBlurShader.Parameters.TileOcclusionStrength = 1f;

            radialBlurShader.Parameters.TileTexture = new HlslSampler2D
            {
                Sampler = SamplerState.PointWrap,
                Texture = LightTargetSwap.Target,
            };

            radialBlurShader.Apply();

            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, radialBlurShader.Shader, Matrix.Identity);
            {
                DrawLights();
            }
            sb.End();
        }

        for (var i = 0; i < lights_mask.Length; i++)
        {
            lights_mask[i] = 0uL;
        }

        return;

        void DrawLights()
        {
            var texture = Assets.DynamicLighting.Light.Asset.Value;

            var origin = texture.Size() * 0.5f;

            for (var i = 0; i < lights_mask.Length; i++)
            {
                var bits = lights_mask[i];

                while (bits != 0)
                {
                    var bitIndex = BitOperations.TrailingZeroCount(bits);
                    var index = i * bits_per_chunk + bitIndex;

                    var (position, color, size) = lights[index];

                    position -= Main.screenPosition;
                    position *= 0.5f;

                    color.A = (byte)(size * byte.MaxValue);

                    sb.Draw(
                        new DrawParameters(texture)
                        {
                            Position = position,
                            Color = color,
                            Origin = origin,
                        }
                    );

                    bits &= bits - 1;
                }
            }
        }
    }
}
