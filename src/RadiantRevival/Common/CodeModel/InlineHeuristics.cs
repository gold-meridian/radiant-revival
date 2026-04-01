using System;
using System.Collections.Generic;
using System.Reflection;

namespace RadiantRevival.Common.CodeModel;

/// <summary>
///     Heuristic analysis of <see cref="MethodInfo"/>s in relation to JIT
///     method inlining.
/// </summary>
internal static class InlineHeuristics
{
    private static readonly Dictionary<RuntimeMethodHandle, bool> plausibly_inlined_cache = new();

    /// <summary>
    ///     Attempts to determine whether a method is likely to be at all
    ///     inlined.  This doesn't mean immediately inlined, but whether the
    ///     method could ever be inlined.
    ///     <br />
    ///     It's better to be overly sensitive than under-sensitive.
    /// </summary>
    /// <remarks>
    ///     This is primarily intended for programmatically determining whether
    ///     method edits need to account for possible inlining.
    /// </remarks>
    public static bool IsPlausiblyInlinable(MethodBase method)
    {
        if (!plausibly_inlined_cache.TryGetValue(method.MethodHandle, out var result))
        {
            return plausibly_inlined_cache[method.MethodHandle] = IsPlausiblyInlinableInner(method);
        }

        return result;
    }

    private static bool IsPlausiblyInlinableInner(MethodBase method)
    {
        try
        {
            var impl = method.GetMethodImplementationFlags();
            if ((impl & MethodImplAttributes.NoInlining) != 0)
            {
                return false;
            }

            if ((impl & MethodImplAttributes.AggressiveInlining) != 0)
            {
                return true;
            }

            if (method.IsAbstract || method.GetMethodBody() is not { } body)
            {
                return false;
            }

            const int possible_max_inline_size = 500;
            var ilSize = body.GetILAsByteArray()?.Length ?? 0;
            
            // Discretionary... eventually see inline.cpp for more in-depth
            // checks?  We can't easily know the MaxInlineSize config.
            if (ilSize > possible_max_inline_size)
            {
                return false;
            }

            // Can eventually add more checks, but we're permissive.
            return true;
        }
        catch
        {
            return true;
        }
    }
}
