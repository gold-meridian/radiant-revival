using Microsoft.Xna.Framework;
using MonoMod.Cil;
using System;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.LeashedEntities;
using Terraria.Graphics.Light;

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

    private static void Load()
    {
        IL_Lighting.AddLight_int_int_float_float_float += AddLight_DynamicLighting;
        {
            IL_DelegateMethods.CastLight += _ => { };
            IL_DelegateMethods.CastLightOpen += _ => { };
            IL_DelegateMethods.CastLightOpen_StopForSolids_ScaleWithDistance += _ => { };
            IL_DelegateMethods.CastLightOpen_StopForSolids += _ => { };
            IL_DelegateMethods.SpreadLightOpen_StopForSolids += _ => { };
            IL_Gore.Update += _ => { };
            IL_Lighting.AddLight_Vector2_Vector3 += _ => { };
            IL_Lighting.AddLight_Vector2_float_float_float += _ => { };
            IL_Lighting.AddLight_Vector2_int += _ => { };
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

        IL_Lighting.AddLight_int_int_int_float += AddLight_DynamicLighting;
        IL_Dust.UpdateDust += _ => { };
    }

    private static void AddLight_DynamicLighting(ILContext il)
    {
        var c = new ILCursor(il);

        var jumpDynamicLightsTarget = c.DefineLabel();

        c.GotoNext(
            MoveType.Before,
            i => i.MatchCallvirt<ILightingEngine>(nameof(ILightingEngine.AddLight))
        );

        c.EmitDelegate(static () => currentlyApplied);

        c.EmitBrfalse(jumpDynamicLightsTarget);

        c.EmitDelegate(AddDynamicLight);
        c.EmitPop();
        c.EmitRet();

        c.MarkLabel(jumpDynamicLightsTarget);
    }

    public static void AddDynamicLight(int i, int j, Vector3 color)
    {

    }
}
