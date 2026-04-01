using Daybreak.Common.Features.Hooks;
using JetBrains.Annotations;
using RadiantRevival.Core.DataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria.ModLoader;

namespace RadiantRevival.Core;

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class ModCallAttribute(params string[] nameAliases) : Attribute
{
    public string[] NameAliases = nameAliases;
}

internal static class ModCallLoader
{
    private sealed class ModCallCollection : AliasDictionary<string, MethodInfo>
    {
        public object? Invoke(string name, object?[]? args)
        {
            var info = this[name].Find(MatchesParameters);

            if (info is null)
            {
                throw new KeyNotFoundException($"No suitable method under alias {name} found!");
            }

            return info.Invoke(null, args);

            bool MatchesParameters(MethodInfo methodInfo)
            {
                ParameterInfo[] parameters = methodInfo.GetParameters();

                if (parameters.Length != (args?.Length ?? 0))
                {
                    return false;
                }

                if (parameters.Length <= 0)
                {
                    return true;
                }

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType != args?[i]?.GetType())
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    private static readonly ModCallCollection handlers = [];

    [OnLoad]
    private static void Load(Mod mod)
    {
        FindModCalls(mod);
    }

    private static void FindModCalls(Mod mod)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        var methods = mod.Code.GetTypes()
                              .SelectMany(t => t.GetMethods(flags));

        foreach (MethodInfo method in methods)
        {
            if (method.IsGenericMethod)
            {
                continue;
            }

            ModCallAttribute? attribute = method.GetCustomAttribute<ModCallAttribute>();

            if (attribute is null)
            {
                continue;
            }

            string[] names = attribute.NameAliases.Length <= 0
              ? [method.Name]
              : attribute.NameAliases;

            handlers.Add(names.ToHashSet(), method);
        }
    }

    public static object? HandleCall(object?[]? args)
    {
        if (args is null || args.Length <= 0)
        {
            throw new ArgumentException("Zero arguments provided!");
        }

        if (args[0] is not string name)
        {
            throw new ArgumentException("First argument was not of type string!");
        }

        return handlers.Invoke(name, args.Skip(1).ToArray());
    }
}
