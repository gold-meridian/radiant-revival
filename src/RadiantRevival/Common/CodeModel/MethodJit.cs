using System.Reflection;
using MonoMod.Cil;
using Terraria.ModLoader;

namespace RadiantRevival.Common.CodeModel;

internal static class MethodJit
{
    public static void ForceJit(MethodBase method)
    {
        MonoModHooks.Modify(method, DoNothing);

        return;

        static void DoNothing(ILContext il) { }
    }
}
