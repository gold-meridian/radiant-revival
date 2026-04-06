using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RadiantRevival.Common;

internal static class ModCallDispatcher
{
    public static object? Dispatch(object?[]? args)
    {
        if (args is null || args.Length <= 0)
        {
            throw new ArgumentException("Zero arguments provided!");
        }

        if (args[0] is not string name)
        {
            throw new ArgumentException("First argument was not of type string!");
        }

        return Invoke(name, args.Skip(1).ToArray());
    }

    private static object? Invoke(string name, object?[]? args)
    {
        if (!ModCallLoader.Handlers.TryGetValue(name, out var handlers)
         || handlers.FirstOrDefault(MatchesParameters) is not { } info)
        {
            throw new KeyNotFoundException($"No suitable method under alias {name} found!");
        }

        return info.Invoke(null, args);

        bool MatchesParameters(MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParameters();

            if (parameters.Length != (args?.Length ?? 0))
            {
                return false;
            }

            if (parameters.Length <= 0)
            {
                return true;
            }

            for (var i = 0; i < parameters.Length; i++)
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
