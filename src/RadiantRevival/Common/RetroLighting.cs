using System.Diagnostics;
using Daybreak.Common.Features.Hooks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using System.Reflection;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.GameContent.Animations;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.Liquid;
using Terraria.GameContent.UI.Elements;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Renderers;
using Terraria.ModLoader;
using Terraria.Testing;

namespace RadiantRevival.Common;

public static class RetroLighting
{
    private static bool targetsReady;

#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Init()
    {
        MonoModHooks.Modify(
            typeof(Main).GetProperty(
                nameof(Main.DefaultSamplerState), BindingFlags.Public | BindingFlags.Static
            )!.GetMethod,
            get_DefaultSamplerState_RetPointClamp
        );

        IL_Main.DrawProjectiles += _ => { };
        IL_Main.DrawNPCDirect_Inner += _ => { };
        IL_Main.DrawNPCDirect_HallowBoss += _ => { };
        IL_Main.DrawProj_LightsBane += _ => { };
        IL_Main.PrepareDrawnEntityDrawing += _ => { };
        IL_Main.DrawCachedProjs += _ => { };
        IL_Main.DrawSuperSpecialProjectiles += _ => { };
        IL_Main.DrawWallOfStars += _ => { };
        IL_Main.DrawSmartCursor += _ => { };
        IL_Main.DrawBlack += _ => { };
        IL_Main.DoDraw_WallsTilesNPCs += _ => { };
        IL_Main.DoDraw_Tiles_Solid += _ => { };
        IL_Main.DoDraw_Tiles_NonSolid += _ => { };
        IL_Main.DoDraw_DrawNPCsOverTiles += _ => { };
        IL_Main.DoDraw_DrawNPCsBehindTiles += _ => { };
        IL_Main.DoDraw_WallsAndBlacks += _ => { };
        IL_DebugLineDraw.Draw += _ => { };
        IL_UIBestiaryEntryIcon.ctor += _ => { };
        // TODO
        // IL_LiquidEdgeRenderer.DrawTileMask += _ => { };
        IL_TileDrawing.PostDrawTiles += _ => { };
        IL_TileDrawing.DrawEntities_DisplayDolls += _ => { };
        IL_TileDrawing.DrawEntities_HatRacks += _ => { };
        IL_TileDrawing.DrawCustom += _ => { };
        IL_TileDrawingBase.Begin += _ => { };
        IL_Segments.SpriteSegment.MaskedFadeEffect.AfterDrawing += _ => { };

        IL_LegacyPlayerRenderer.DrawPlayerFull += _ => { };
        IL_ReturnGatePlayerRenderer.DrawReturnGateInWorld += _ => { };
    }
#pragma warning restore CA2255

    [OnLoad]
    private static void Load()
    {
        IL_Main.DoDraw += DoDraw_DontCaptureMenu;

        IL_Main.DoDraw += DoDraw_CaptureRetroLighting;
        IL_FilterManager.EndCapture_RenderTarget2D_RenderTarget2D_RenderTarget2D_Vector2_Vector2_Vector2 += EndCapture_RetroColoration;

        IL_CaptureCamera.EndDrawCapture += EndDrawCapture_AllowCapturing;
        IL_Main.DrawCapture += _ => { };

        On_FilterManager.BeginCapture += BeginCapture_Safety;
    }

    private static void get_DefaultSamplerState_RetPointClamp(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(
            MoveType.After,
            i => i.MatchLdsfld<Main>(nameof(Main.drawToScreen))
        );

        c.EmitPop();

        c.EmitLdcI4(0);
    }

    private static void DoDraw_DontCaptureMenu(ILContext il)
    {
        var c = new ILCursor(il);

        ILLabel jumpDrawMenuTarget = c.DefineLabel();
        ILLabel drawMenuTarget = c.DefineLabel();

        c.GotoNext(
            MoveType.Before,
            i => i.MatchCall<Main>(nameof(Main.DrawMenu))
        );

        var c2 = c.Clone();

        c2.GotoNext(
            MoveType.Before,
            i => i.MatchLdsfld<Main>(nameof(Main.HorizonHelper)),
            i => i.MatchLdloc(out _)
        );

        c2.EmitRet();
        c2.MarkLabel(jumpDrawMenuTarget);

        c2.GotoNext(
            MoveType.Before,
            i => i.MatchRet()
        );

        c2.MoveAfterLabels();
        c2.EmitBr(drawMenuTarget);

        c.GotoPrev(
            MoveType.After,
            i => i.MatchCall<Main>(nameof(Main.DrawLensFlare))
        );

        c.EmitBr(jumpDrawMenuTarget);
        c.MarkLabel(drawMenuTarget);
    }

    private static void DoDraw_CaptureRetroLighting(ILContext il)
    {
        var c = new ILCursor(il);

        ILLabel jumpInitTargets = c.DefineLabel();

        c.GotoNext(
            MoveType.Before,
            i => i.MatchCall<Lighting>($"get_{nameof(Lighting.UpdateEveryFrame)}"),
            i => i.MatchBrfalse(out _)
        );

        c.MoveAfterLabels();

        c.EmitDelegate(LoadSpecificTargets);

        c.EmitBrtrue(jumpInitTargets);

        c.GotoNext(
            MoveType.Before,
            i => i.MatchLdsfld<Main>(nameof(Main.fpsCount))
        );

        c.MoveAfterLabels();

        c.MarkLabel(jumpInitTargets);

        for (int j = 0; j < 2; j++)
        {
            c.GotoNext(i => i.MatchLdstr("Sepia"));
        }

        c.GotoNext(
            MoveType.After,
            i => i.MatchLdsfld<Main>(nameof(Main.drawToScreen))
        );

        c.EmitPop();

        c.EmitLdcI4(0);

        c.GotoNext(
            MoveType.After,
            i => i.MatchCall<Lighting>($"get_{nameof(Lighting.NotRetro)}")
        );

        c.EmitPop();

        // Add our own condition, (This is just to be extra safe, if the targets were null the game may attempt to draw a null target in EndCapture).
        c.EmitDelegate(() =>
            Main.screenTarget is not null &&
            Main.screenTargetSwap is not null &&
            Main.skyTarget is not null &&
            !Main.screenTarget.IsContentLost &&
            !Main.screenTargetSwap.IsContentLost &&
            !Main.skyTarget.IsContentLost
        );
    }

    private static void EndCapture_RetroColoration(ILContext il)
    {
        var c = new ILCursor(il);

        while (c.TryGotoNext(
                   MoveType.After,
                   i => i.MatchLdsfld<Main>(nameof(Main.ColorOfTheSkies))
               ))
        {
            c.EmitDelegate(static (Color sky) => Lighting.UpdateEveryFrame ? Color.White : sky);
        }
    }

    private static void EndDrawCapture_AllowCapturing(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(
            MoveType.After,
            i => i.MatchCall<Lighting>($"get_{nameof(Lighting.NotRetro)}")
        );

        c.EmitPop();

        c.EmitLdcI4(1);
    }

    private static void BeginCapture_Safety(On_FilterManager.orig_BeginCapture orig, FilterManager self, RenderTarget2D screenTarget1)
    {
        if (Lighting.UpdateEveryFrame && !targetsReady)
        {
            return;
        }

        orig(self, screenTarget1);
    }

    private static bool LoadSpecificTargets()
    {
        if (!Lighting.UpdateEveryFrame)
        {
            return false;
        }

        if (Main.targetSet)
        {
            FauxReleaseTargets();
        }

        Main.targetSet = false;

        GraphicsDevice device = Main.graphics.GraphicsDevice;

        int width = device.PresentationParameters.BackBufferWidth;
        int height = device.PresentationParameters.BackBufferHeight;

        if (!ShouldRefreshTarget(Main.screenTarget)
         && !ShouldRefreshTarget(Main.screenTargetSwap)
         && !ShouldRefreshTarget(Main.skyTarget)
         && targetsReady)
        {
            return true;
        }

        var format = device.PresentationParameters.BackBufferFormat;

        Main.screenTarget?.Dispose();
        Main.screenTargetSwap?.Dispose();
        Main.skyTarget?.Dispose();

        Main.screenTarget = new RenderTarget2D(device, width, height, false, format, DepthFormat.None);
        Main.screenTargetSwap = new RenderTarget2D(device, width, height, false, format, DepthFormat.None);
        Main.skyTarget = new RenderTarget2D(device, width, height, false, format, DepthFormat.None);

        targetsReady = true;

        return true;

        bool ShouldRefreshTarget(RenderTarget2D? target)
        {
            return target is null || target.IsContentLost || target.Width != width || target.Height != height;
        }
    }

    private static void FauxReleaseTargets()
    {
        Main.drawToScreen = true;
        Main.offScreenRange = 0;

        Main.waterTarget?.Dispose();
        Main.backWaterTarget.Dispose();
        Main.tileTarget?.Dispose();
        Main.tile2Target?.Dispose();
        Main.wallTarget?.Dispose();
        Main.backgroundTarget?.Dispose();
    }
}
