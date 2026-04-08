using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Terraria;
using Terraria.Graphics.Light;
using VanillaEngine = Terraria.Graphics.Light.LightingEngine;

namespace RadiantRevival.Common;

/// <summary>
///     The contract for an advanced lighting engine implementation.
/// </summary>
public interface IAdvancedLightingEngine
{
    /// <summary>
    ///     Retrieves the export for this engine.
    /// </summary>
    LightingEngineExport GetExport();
}

/// <summary>
///     Converts an <see cref="ILightingEngine"/> implementation to an
///     <see cref="IAdvancedLightingEngine"/> implementation.
/// </summary>
public interface ILightingEngineConverter
{
    /// <summary>
    ///     Attempts to perform a conversion from a vanilla lighting engine
    ///     implementation to an advanced implementation.
    /// </summary>
    bool TryConvert(
        ILightingEngine engine,
        [NotNullWhen(returnValue: true)] out IAdvancedLightingEngine? advanced
    );
}

/// <summary>
///     Provides APIs for interfacing with the vanilla lighting engine and our
///     various extensions to its capabilities.
/// </summary>
partial class LightingEngine
{
    private readonly struct LegacyLightingAdvanced(LegacyLighting engine) : IAdvancedLightingEngine
    {
        public LightingEngineExport GetExport()
        {
            throw new NotImplementedException();
        }
    }

    private readonly struct LightingEngineAdvanced(VanillaEngine engine) : IAdvancedLightingEngine
    {
        public LightingEngineExport GetExport()
        {
            return new LightingEngineExport(engine._activeLightMap, engine._activeProcessedArea);
        }
    }

    private sealed class DefaultLightingEngineConverter<T>(Func<T, IAdvancedLightingEngine> converter) : ILightingEngineConverter
        where T : ILightingEngine
    {
        public bool TryConvert(
            ILightingEngine engine,
            [NotNullWhen(returnValue: true)] out IAdvancedLightingEngine? advanced
        )
        {
            if (engine is not T tEngine)
            {
                advanced = null;
                return false;
            }

            advanced = converter(tEngine);
            return true;
        }
    }

    private static readonly Dictionary<Type, ILightingEngineConverter> engine_converters = [];

    static LightingEngine()
    {
        RegisterAdvancedEngineConverter<LegacyLighting>(static engine => new LegacyLightingAdvanced(engine));
        RegisterAdvancedEngineConverter<VanillaEngine>(static engine => new LightingEngineAdvanced(engine));
    }

    /// <summary>
    ///     Attempts to interpret the currently active lighting engine as an
    ///     <see cref="IAdvancedLightingEngine"/>.
    /// </summary>
    public static bool TryGetCurrentEngine([NotNullWhen(returnValue: true)] out IAdvancedLightingEngine? engine)
    {
        var activeEngine = Lighting._activeEngine;

        // Probably not possible (at least, it *definitely* isn't in vanilla,
        // but who knows about other mods?).
        if (activeEngine is null)
        {
            engine = null;
            return false;
        }

        // Best case for modded engines.
        if (activeEngine is IAdvancedLightingEngine advancedLightingEngine)
        {
            engine = advancedLightingEngine;
            return true;
        }

        // Vanilla engines and weak-referenced modded engines.
        if (engine_converters.TryGetValue(activeEngine.GetType(), out var converter))
        {
            if (converter.TryConvert(activeEngine, out engine))
            {
                return true;
            }
        }

        engine = null;
        return false;
    }

    /// <summary>
    ///     Gets the currently active lighting engine as an
    ///     <see cref="IAdvancedLightingEngine"/>, throwing if the conversion
    ///     fails.
    /// </summary>
    public static IAdvancedLightingEngine GetCurrentEngine()
    {
        return TryGetCurrentEngine(out var engine)
            ? engine
            : throw new InvalidOperationException($"Failed to get current engine: the engine '{Lighting._activeEngine?.GetType().FullName ?? "<null>"}' is not convertible to our advanced engine!");
    }

    /// <inheritdoc cref="RegisterAdvancedEngineConverter"/>
    public static void RegisterAdvancedEngineConverter<T>(ILightingEngineConverter converter)
        where T : ILightingEngine
    {
        RegisterAdvancedEngineConverter(typeof(T), converter);
    }

    /// <inheritdoc cref="RegisterAdvancedEngineConverter"/>
    public static void RegisterAdvancedEngineConverter<T>(Func<T, IAdvancedLightingEngine> converter)
        where T : ILightingEngine
    {
        RegisterAdvancedEngineConverter(typeof(T), new DefaultLightingEngineConverter<T>(converter));
    }

    /// <summary>
    ///     Registers a factory method for creating an
    ///     <see cref="IAdvancedLightingEngine"/> instance from an existing
    ///     <see cref="ILightingEngine"/>.
    /// </summary>
    public static void RegisterAdvancedEngineConverter(Type lightingEngineType, ILightingEngineConverter converter)
    {
        engine_converters[lightingEngineType] = converter;
    }
}
