using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Daybreak.Common.Features.Hooks;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace RadiantRevival.Common;

internal static class ModCallLoader
{
    public static Dictionary<string, List<MethodInfo>> Handlers { get; } = [];

    [OnLoad]
    private static void Load(Mod mod)
    {
        FindModCalls(mod);
    }

    private static void FindModCalls(Mod mod)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        var methods = AssemblyManager.GetLoadableTypes(mod.Code)
                                     .Where(x => !x.ContainsGenericParameters)
                                     .SelectMany(t => t.GetMethods(flags));

        foreach (var method in methods)
        {
            if (method.IsGenericMethod)
            {
                continue;
            }

            if (method.GetCustomAttribute<ModCallAttribute>() is not { } attribute)
            {
                continue;
            }

            var names = attribute.NameAliases.Length <= 0 ? [method.Name] : attribute.NameAliases;
            foreach (var name in names)
            {
                if (!Handlers.TryGetValue(name, out var handlers))
                {
                    Handlers[name] = handlers = [];
                }

                handlers.Add(method);
            }
        }
    }
}
