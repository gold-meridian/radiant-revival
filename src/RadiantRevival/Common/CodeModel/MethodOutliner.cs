using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using log4net;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;

namespace RadiantRevival.Common.CodeModel;

// TODO: Propagate upward in case a method with an inlined function could also
//       be inlined?

/// <summary>
///     Utility for reversing method inling in edited methods to more
///     accurately propagate hook changes.
/// </summary>
internal static class MethodOutliner
{
    // TODO: Only scans the Terraria module because hooks should be applied
    //       in the module initializer if they're inlinable, preferably.
    private static readonly Module[] modules =
    [
        typeof(Main).Module,
    ];

    public static void OutlineIfPlausiblyInlinable(MethodBase method, ILog logger)
    {
        logger.Debug("OutlineIfPlausiblyInlinable: " + MethodToNameString(method));

        if (!InlineHeuristics.IsPlausiblyInlinable(method))
        {
            logger.Debug("    Not plausibly inlinable, skipping...");
            return;
        }

        OutlineMethod(method, logger);
    }

    public static void OutlineMethod(MethodBase method, ILog logger)
    {
        logger.Debug("OutlineMethod: " + MethodToNameString(method));

        var sw = Stopwatch.StartNew();
        foreach (var module in modules)
        {
            logger.Debug("    Scanning module: " + module.FullyQualifiedName);
            var callers = FindCallers(module, method);
            if (callers.Length == 0)
            {
                logger.Debug("    No callers found, skipping module...");
                continue;
            }

            logger.Debug($"    Found {callers.Length} caller(s):");
            foreach (var caller in callers)
            {
                logger.Debug("        " + MethodToNameString(caller));
            }

            foreach (var caller in callers)
            {
                MonoModHooks.Modify(caller, DoNothing);
            }
        }

        sw.Stop();
        logger.Debug($"    Outlined {method.Name} in {sw.ElapsedMilliseconds}ms");
        return;

        static void DoNothing(ILContext il) { }
    }

    private static MethodBase[] FindCallers(Module module, MethodBase target)
    {
        var targetToken = target.MetadataToken;

        var bag = new ConcurrentBag<MethodBase>();
        Parallel.ForEach(
            module.GetTypes(),
            type =>
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (method.GetMethodBody() is not { } body)
                    {
                        continue;
                    }

                    if (body.GetILAsByteArray() is not { } il)
                    {
                        continue;
                    }

                    if (!ContainsCall(il, targetToken))
                    {
                        continue;
                    }

                    bag.Add(method);
                }
            }
        );

        return bag.ToArray();
    }

    private static bool ContainsCall(byte[] il, int targetToken)
    {
        var i = 0;

        while (i < il.Length)
        {
            var op = il[i++];

            // two-byte opcode
            if (op == 0xFE)
            {
                op = (byte)(0xFE00 | il[i++]);
            }

            switch (op)
            {
                case 0x28: // call
                case 0x6F: // callvirt
                case 0x73: // newobj
                {
                    var token = BitConverter.ToInt32(il, i);
                    i += 4;

                    if (token == targetToken)
                    {
                        return true;
                    }

                    break;
                }

                default:
                    i += OperandSize(op);
                    break;
            }
        }

        return false;
    }

    private static int OperandSize(int opcode)
    {
        // minimal but works for most cases
        switch (opcode)
        {
            case 0x28: // call
            case 0x6F: // callvirt
            case 0x73: // newobj
                return 4;

            case 0x2A: // ret
                return 0;

            default:
                return 0; // fallback (safe but may desync on rare ops)
        }
    }

    private static string MethodToNameString(MethodBase method)
    {
        return $"{method.DeclaringType?.FullName ?? "<null>"}::{method.Name}({string.Join(',', method.GetParameters().Select(p => p.ParameterType.Name))})";
    }
}
