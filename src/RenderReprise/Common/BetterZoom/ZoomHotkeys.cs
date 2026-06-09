using System;
using Daybreak.Common.Features.Hooks;
using Terraria;
using Terraria.GameInput;

namespace RenderReprise.Common;

/// <summary>
///     Allows for modifying UI zoom with hotkeys like in the Better Zoom mod.
/// </summary>
internal static class ZoomHotkeys
{
    private enum ZoomType
    {
        Game,
        Interface,
    }

    private enum ZoomDirection
    {
        In,
        Out,
        None,
    }

    [OnLoad]
    private static void LoadHooks()
    {
        On_Main.UpdateViewZoomKeys += (orig, self) =>
        {
            if (Main.inFancyUI)
            {
                return;
            }

            var zoomType = GetZoomType();
            var zoomDirection = GetZoomDirection();
            if (zoomDirection == ZoomDirection.None)
            {
                return;
            }

            var zoomAmount = 0.02f; // TODO: config
            if (zoomDirection == ZoomDirection.Out)
            {
                zoomAmount = -zoomAmount;
            }

            // TODO: min/max config
            switch (zoomType)
            {
                case ZoomType.Game:
                    zoomAmount *= Main.GameZoomTarget;

                    Main.GameZoomTarget = Utils.Clamp(Main.GameZoomTarget + zoomAmount, 0.5f, 2f);
                    break;

                case ZoomType.Interface:
                    zoomAmount *= Main.UIScale;

                    Main.UIScale = Utils.Clamp(Main.UIScale + zoomAmount, 0.5f, 2f);
                    Main.temporaryGUIScaleSlider = Main.UIScale;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        };
    }

    private static ZoomDirection GetZoomDirection()
    {
        var zoomIn = PlayerInput.Triggers.Current.ViewZoomIn;
        var zoomOut = PlayerInput.Triggers.Current.ViewZoomOut;
        if (!(zoomIn ^ zoomOut))
        {
            return ZoomDirection.None;
        }

        return zoomIn ? ZoomDirection.In : ZoomDirection.Out;
    }

    private static ZoomType GetZoomType()
    {
        return Main.keyState.PressingShift() ? ZoomType.Interface : ZoomType.Game;
    }
}
