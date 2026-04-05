using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using RadiantRevival.Core;
using System;
using System.Diagnostics;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.ModLoader;

namespace RadiantRevival.Common;

// TODO: Config
public class LogoNormals
{
    private static WrapperShaderData<Assets.UI.LogoNormals.Parameters>? logoNormalsShaderData;

    [OnLoad]
    private static void Load()
    {
        logoNormalsShaderData = Assets.UI.LogoNormals.CreateLogoNormalsShader();

        MonoModHooks.Modify(
            typeof(MenuLoader).GetMethod(
                nameof(MenuLoader.UpdateAndDrawModMenuInner),
                BindingFlags.NonPublic | BindingFlags.Static
            ),
            UpdateAndDrawModMenuInner_Normals
        );
    }

    private static void UpdateAndDrawModMenuInner_Normals(ILContext il)
    {
        var c = new ILCursor(il);

        int spriteBatchIndex = -1; // loc
        int logoDrawCenterIndex = -1; // loc

        int logoRotationIndex = -1; // arg
        int logoScale2Index = -1; // loc

        c.GotoNext(
            i => i.MatchCallvirt<ModMenu>(nameof(ModMenu.PreDrawLogo))
        );

        c.GotoNext(
            i => i.MatchLdloc(out logoDrawCenterIndex),
            i => i.MatchLdcI4(out _),
            i => i.MatchLdcI4(out _)
        );

        c.GotoNext(
            i => i.MatchNewobj<Rectangle?>(),
            i => i.MatchLdarg(out _),
            i => i.MatchLdarg(out logoRotationIndex)
        );

        c.GotoNext(
            i => i.MatchNewobj<Vector2>(),
            i => i.MatchLdloc(out logoScale2Index));

        c.GotoNext(
            MoveType.Before,
            i => i.MatchLdsfld(typeof(MenuLoader), nameof(MenuLoader.currentMenu)),
            i => i.MatchLdarg(out spriteBatchIndex)
        );

        c.MoveBeforeLabels();

        c.EmitLdarg(spriteBatchIndex);

        c.EmitLdloc(logoDrawCenterIndex);

        c.EmitLdarg(logoRotationIndex);
        c.EmitLdloc(logoScale2Index);

        c.EmitDelegate(DrawLighting);
    }

    private static void DrawLighting(SpriteBatch sb, Vector2 logoDrawCenter, float logoRotation, float logoScale2)
    {
        if (MenuLoader.currentMenu.Logo.Value != ModMenu.modLoaderLogo.Value)
        {
            return;
        }

        Debug.Assert(logoNormalsShaderData is not null);

        sb.End(out var ss);
        sb.Begin(ss with { SortMode = SpriteSortMode.Immediate, CustomEffect = null });

        var screenSize = new Vector2(Main.screenWidth, Main.screenHeight);

        logoNormalsShaderData.Parameters.rotation = logoRotation;
        logoNormalsShaderData.Parameters.light_position = Main.LastCelestialBodyPosition * screenSize;

        logoNormalsShaderData.Apply();

        Texture2D normal = Assets.UI.tMLLogoNormals.Asset.Value;
        Vector2 normalOrigin = normal.Size() * 0.5f;

        HorizonHelper.GetCelestialBodyColors(out var sunColor, out var moonColor);

        sunColor = sunColor.MultiplyRGB(Color.PeachPuff);
        moonColor = Color.Pow(moonColor, 6f) * 100f;

        NextHorizonRenderer.GetVisibilities(out var sunsetVisibility, out var sunriseVisibility, out var celestialVisibility);

        Color color = Main.dayTime ? sunColor : moonColor;

        float num = Math.Max(sunsetVisibility, sunriseVisibility) * celestialVisibility;
        if (!Main.dayTime)
        {
            num = Math.Max(num, celestialVisibility * 0.15f);
        }

        color *= num;

        sb.Draw(normal, logoDrawCenter, null, color, logoRotation, normalOrigin, logoScale2, SpriteEffects.None, 0f);

        sb.Restart(in ss);
    }
}
