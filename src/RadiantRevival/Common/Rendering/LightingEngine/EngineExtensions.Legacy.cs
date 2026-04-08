using System;
using Daybreak.Common.Features.Hooks;
using GoldMeridian.CodeAnalysis;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using Terraria;
using Terraria.Graphics.Light;

namespace RadiantRevival.Common;

partial class LightingEngine
{
    [ExtensionDataFor<LegacyLighting>]
    internal sealed class LegacyLightingData
    {
        public required LightMap ExposedLightMap { get; set; }
    }

    private readonly struct LegacyLightingAdvanced(LegacyLighting engine) : IAdvancedLightingEngine
    {
        public LightingEngineExport GetExport()
        {
            if (engine.Data is not { } data)
            {
                return empty_export;
            }

            return new LightingEngineExport(
                data.ExposedLightMap,
                new Rectangle(
                    engine._expandedRectLeft,
                    engine._expandedRectTop,
                    engine._expandedRectRight - engine._expandedRectLeft,
                    engine._expandedRectBottom - engine._expandedRectTop
                )
            );
        }
    }

    [OnLoad]
    private static void LoadLegacyImplementation()
    {
        RegisterAdvancedEngineConverter<LegacyLighting>(static engine => new LegacyLightingAdvanced(engine));

        IL_LegacyLighting.Rebuild += il =>
        {
            var c = new ILCursor(il);

            c.GotoNext(MoveType.Before, x => x.MatchCallOrCallvirt<LightMap>(nameof(LightMap.SetSize)));
            c.Remove();

            c.EmitLdarg0();
            c.EmitDelegate(
                (LightMap lightMap, int width, int height, LegacyLighting self) =>
                {
                    lightMap.SetSize(width, height);
                    self.Data ??= new LegacyLightingData { ExposedLightMap = new LightMap() };
                    self.Data.ExposedLightMap.SetSize(width, height);
                }
            );
        };

        On_LegacyLighting.ProcessArea += (orig, self, area) =>
        {
            orig(self, area);

            if (self.Data is not { } data)
            {
                self.Data = data = new LegacyLightingData { ExposedLightMap = new LightMap() };
                data.ExposedLightMap.SetSize(self._lightMap.Width, self._lightMap.Height);
            }

            var unscaledSize = self._camera.UnscaledSize;
            var offscreenTiles = Lighting.OffScreenTiles * 2;
            var maxLightArrayX = (int)unscaledSize.X / 16 + offscreenTiles;
            var maxLightArrayY = (int)unscaledSize.Y / 16 + offscreenTiles;
            var unscaledPosition = self._camera.UnscaledPosition;
            var num = (int)Math.Floor(unscaledPosition.X / 16f) - self._scrX;
            var num2 = (int)Math.Floor(unscaledPosition.Y / 16f) - self._scrY;
            if (num > 16)
            {
                num = 0;
            }

            if (num2 > 16)
            {
                num2 = 0;
            }

            var xStart = 0;
            var num4 = maxLightArrayX;
            var yStart = 0;
            var num6 = maxLightArrayY;
            if (num < 0)
            {
                xStart -= num;
            }
            else
            {
                num4 -= num;
            }

            if (num2 < 0)
            {
                yStart -= num2;
            }
            else
            {
                num6 -= num2;
            }

            var xEnd = num4;
            if (self._states.Length <= xEnd + num)
            {
                xEnd = self._states.Length - num - 1;
            }

            for (var x = xStart; x < xEnd; x++)
            {
                LegacyLighting.LightingState[] row = self._states[x];
                LegacyLighting.LightingState[] array4 = self._states[x + num];
                var yEnd = num6;
                if (array4.Length <= yEnd + num)
                {
                    yEnd = array4.Length - num2 - 1;
                }

                for (var y = yStart; y < yEnd; y++)
                {
                    var state = row[y];
                    data.ExposedLightMap[x, y] = state.ToVector3();
                }
            }
        };

        On_LegacyLighting.Clear += (orig, self) =>
        {
            orig(self);
            self.Data?.ExposedLightMap.Clear();
        };
    }
}
