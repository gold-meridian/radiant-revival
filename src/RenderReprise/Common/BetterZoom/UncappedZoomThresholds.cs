using System.Reflection;
using Daybreak.Common.Features.Hooks;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using Terraria;
using Terraria.GameContent.UI.States;
using Terraria.Graphics;
using Terraria.Initializers;
using Terraria.ModLoader;

namespace RenderReprise.Common;

internal static class UncappedZoomThresholds
{
    [OnLoad]
    private static void ApplyHooks()
    {
        // Game zoom
        IL_Main.DoDraw += il =>
        {
            var c = new ILCursor(il);

            c.GotoNext(MoveType.After, x => x.MatchLdsflda<Main>(nameof(Main.GameViewMatrix)));
            c.EmitDup();
            c.EmitDelegate(
                (ref SpriteViewMatrix gameViewMatrix) =>
                {
                    // No more clamping of GameZoomTarget.
                    gameViewMatrix.Zoom = new Vector2(Main.ForcedMinimumZoom * Main.GameZoomTarget);
                }
            );
        };

        // Interface zoom
        MonoModHooks.Add(
            typeof(Main).GetProperty(nameof(Main.UIScaleMax), BindingFlags.Public | BindingFlags.Instance)!.GetGetMethod()!,
            (Main self) => float.MaxValue
        );
        
        // Uses UIScaleMax
        MonoModHooks.Modify(
            typeof(Main).GetProperty(nameof(Main.UIScale), BindingFlags.Public | BindingFlags.Static)!.GetSetMethod()!,
            _ => { }
        );
        
        // Uses set_UIScale
        IL_UIWorldGenDebug.Recalculate += _ => { };
        IL_UIWorldGenDebug.Draw += _ => { };
        IL_IngameOptions.Draw += _ => { };
        IL_UILinksInitializer.HandleOptionsSpecials += _ => { };
        IL_Main.LoadSettings += _ => { };
        IL_Main.PreDrawMenu += _ => { };
        IL_Main.FixUIScale += _ => { };
        
        // TODO: May also need to check for uses of FixUIScale
    }
}
