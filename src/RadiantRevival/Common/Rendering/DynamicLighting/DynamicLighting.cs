using Microsoft.Xna.Framework;
using MonoMod.Cil;
using System;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.LeashedEntities;
using Terraria.Graphics.Light;
using Terraria.Graphics.Renderers;
using Terraria.ID;
using BitOperations = System.Numerics.BitOperations;

namespace RadiantRevival.Common;

public static class DynamicLighting
{
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

#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initialize()
    {
        // It's never enough.
        IL_Lighting.AddLight_int_int_float_float_float += AddLight_TileCoords;
        {
            IL_DelegateMethods.CastLight += _ => { };
            IL_DelegateMethods.CastLightOpen += _ => { };
            IL_DelegateMethods.CastLightOpen_StopForSolids_ScaleWithDistance += _ => { };
            IL_DelegateMethods.CastLightOpen_StopForSolids += _ => { };
            IL_DelegateMethods.SpreadLightOpen_StopForSolids += _ => { };
            IL_Dust.UpdateDust += _ => { };
            IL_Gore.Update += _ => { };
            IL_Main.DrawProj_DrawExtras += _ => { };
            IL_Mount.Hover += _ => { };
            IL_Mount.UpdateFrame += _ => { };
            IL_Mount.UpdateEffects += _ => { };
            IL_Mount.AimAbility += _ => { };
            IL_NPC.VanillaAI_Inner += _ => { };
            IL_NPC.AI_065_Butterflies += _ => { };
            IL_NPC.AI_037_Destroyer += _ => { };
            IL_NPC.AI_005_EaterOfSouls += _ => { };
            IL_NPC.AI_002_FloatingEye += _ => { };
            IL_NPC.AI_003_Fighters += _ => { };
            IL_NPC.AI_001_Slimes += _ => { };
            IL_NPC.UpdateNPC_UpdateTrails += _ => { };
            IL_NPC.UpdateNPC_BuffApplyVFX += _ => { };
            IL_NPC.UpdateNPC_CastLights += _ => { };
            IL_Player.UpdateBuffs += _ => { };
            IL_Player.ApplyEquipFunctional += _ => { };
            IL_Player.Update += _ => { };
            IL_Player.UpdateArmorLights += _ => { };
            IL_Player.PlayerFrame += _ => { };
            IL_Player.ItemCheck_EmitUseVisuals += _ => { };
            IL_Projectile.ProjLight += _ => { };
            IL_Projectile.VanillaAI += _ => { };
            IL_Projectile.AI_003_Boomerang += _ => { };
            IL_Projectile.AI_001 += _ => { };
            IL_Projectile.AI_026 += _ => { };
            IL_Projectile.AI_075 += _ => { };
            IL_WaterfallManager.AddLight += _ => { };
            IL_WorldItem.UpdateItem_VisualEffects += _ => { };
            IL_FireflyLeashedCritter.AddLight += _ => { };
            IL_HellButterflyLeashedCritter.VisualEffects += _ => { };
            IL_SnailLeashedCritter.VisualEffects += _ => { };
            IL_TileDrawing.DrawTrees += _ => { };
        }

        IL_Lighting.AddLight_int_int_int_float += AddLight_TileCoords;
        {
            IL_Dust.UpdateDust += _ => { };
        }

        IL_Lighting.AddLight_Vector2_Vector3 += AddLight_WorldCoords_Vector3;
        {
            IL_Dust.UpdateDust += _ => { };
            IL_Mount.UpdateEffects += _ => { };
            IL_NPC.VanillaAI_Inner += _ => { };
            IL_NPC.AI_065_Butterflies += _ => { };
            IL_NPC.AI_120_HallowBoss += _ => { };
            IL_NPC.AI_117_BloodNautilus += _ => { };
            IL_NPC.AI_112_FairyCritter += _ => { };
            IL_NPC.AI_007_TownEntities += _ => { };
            IL_NPC.AI_003_Fighters += _ => { };
            IL_Player.DashMovement += _ => { };
            IL_Player.ItemCheck_EmitUseVisuals += _ => { };
            IL_Player.ItemCheck_EmitHeldItemLight += _ => { };
            IL_Projectile.ProjLight += _ => { };
            IL_Projectile.EmitEnchantmentVisualsAt += _ => { };
            IL_Projectile.VanillaAI += _ => { };
            IL_Projectile.AI_205_RemoteControlCar += _ => { };
            IL_Projectile.AI_203_StormLightning += _ => { };
            IL_Projectile.AI_196_Petal += _ => { };
            IL_Projectile.AI_195_JimsDrone += _ => { };
            IL_Projectile.AI_191_TrueNightsEdge += _ => { };
            IL_Projectile.AI_190_NightsEdge += _ => { };
            IL_Projectile.AI_182_FinalFractal += _ => { };
            IL_Projectile.AI_167_SparkleGuitar += _ => { };
            IL_Projectile.AI_166_Dove += _ => { };
            IL_Projectile.AI_165_Whip += _ => { };
            IL_Projectile.AI_067_FreakingPirates += _ => { };
            IL_Projectile.AI_157_SharpTears += _ => { };
            IL_Projectile.AI_007_GrapplingHooks += _ => { };
            IL_Projectile.AI_147_Celeb2Rocket += _ => { };
            IL_Projectile.AI_001 += _ => { };
            IL_Projectile.AI_130_FlameBurstTower += _ => { };
            IL_WorldItem.UpdateItem_VisualEffects += _ => { };
            // Terraria.ModLoader.Default.Patreon.OrianSetEffectPlayer.PostUpdate
            // Terraria.ModLoader.Default.Developer.Jofairden.JofairdenArmorEffectPlayer.PostUpdate
            IL_EmpressButterflyLeashedCritter.VisualEffects += _ => { };
            IL_FairyLeashedCritter.VisualEffects += _ => { };
            IL_ParticleOrchestrator.Spawn_LeafCrystalShot += _ => { };
            IL_ParticleOrchestrator.Spawn_BlueLightningSmall += _ => { };
        }

        IL_Lighting.AddLight_Vector2_float_float_float += AddLight_WorldCoords_RGB;
        {
            IL_Dust.UpdateDust += _ => { };
            IL_Gore.UpdateLightningBunnySparks += _ => { };
            IL_Gore.Update += _ => { };
            IL_Main.DrawProj_DrawExtras += _ => { };
            IL_Mount.UpdateFrame += _ => { };
            IL_Mount.UpdateEffects += _ => { };
            IL_NPC.VanillaAI_Inner += _ => { };
            IL_NPC.AI_127_Pal += _ => { };
            IL_NPC.AI_121_QueenSlime += _ => { };
            IL_NPC.AI_007_TownEntities += _ => { };
            IL_NPC.AI_003_Fighters += _ => { };
            IL_NPC.AI_001_Slimes += _ => { };
            IL_NPC.AI_026_Unicorns += _ => { };
            IL_NPC.UpdateNPC_CastLights += _ => { };
            IL_Player.UpdateArmorSetsOld += _ => { };
            IL_Player.WingFrame += _ => { };
            IL_Player.ItemCheck_EmitHeldItemLight += _ => { };
            IL_Projectile.VanillaAI += _ => { };
            IL_Projectile.AI_204_Digtoise += _ => { };
            IL_Projectile.AI_199_MeteorOre += _ => { };
            IL_Projectile.AI_105_SporeSac += _ => { };
            IL_Projectile.AI_113_TargetSticker += _ => { };
            IL_Projectile.AI_100_Medusa += _ => { };
            IL_Projectile.AI_120_StardustGuardian += _ => { };
            IL_Projectile.AI_019_Spears += _ => { };
            IL_Projectile.AI_067_FreakingPirates += _ => { };
            IL_Projectile.AI_007_GrapplingHooks += _ => { };
            IL_Projectile.AI_001 += _ => { };
            IL_Projectile.AI_026 += _ => { };
            IL_Projectile.AI_062 += _ => { };
            IL_Projectile.AI_075 += _ => { };
            IL_Projectile.AI_138_ExplosiveTrap += _ => { };
            IL_Projectile.AI_140_MonkStaffT1 += _ => { };
            IL_Projectile.AI_142_MonkStaffT2And3 += _ => { };
            IL_WorldItem.UpdateItem += _ => { };
            IL_WorldItem.UpdateItem_VisualEffects += _ => { };
            IL_BloodyExplosionParticle.Update += _ => { };
            IL_GasParticle.Update += _ => { };
            IL_PotionOfReturnGateHelper.Update += _ => { };
            IL_VoidLensHelper.Update += _ => { };
            IL_ParticleOrchestrator.Spawn_StormLightning += _ => { };
            IL_ArmorSetBonuses.Benefits.Forbidden += _ => { };
            IL_FireflyLeashedCritter.AddLight += _ => { };
        }

        IL_Lighting.AddLight_Vector2_int += AddLight_WorldCoords_Torch;
        {
            IL_NPC.AI_001_Slimes += _ => { };
            IL_WorldItem.UpdateItem_VisualEffects += _ => { };
        }

        IL_Main.DoDraw += DoDraw_DrawDynamicLights;
    }
#pragma warning restore CA2255

    private static void DoDraw_DrawDynamicLights(ILContext il)
    {
        var c = new ILCursor(il);
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
            AddDynamicLight(position.ToWorldCoordinates(), new Vector3(r, g, b));
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

        var col = new Color(color.X / byte.MaxValue, color.Y / byte.MaxValue, color.Z / byte.MaxValue);

        AddDynamicLight(position, col, size);
    }

    public static void AddDynamicLight(Vector2 position, Color color, float size)
    {
        var index = GetFirstInactive();

        if (index == -1)
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
}
