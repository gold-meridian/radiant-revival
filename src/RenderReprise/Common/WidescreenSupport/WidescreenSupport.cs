using Daybreak.Common.Features.Hooks;
using MonoMod.Cil;
using Terraria;

namespace RenderReprise.Common;

internal static class WidescreenSupport
{
    private const int hidef_8k = 8192;
    
    [OnLoad]
    private static void InitWidescreenSupport()
    {
        // TODO: It might be worthwhile to save these to restore later, but does
        //       it really matter?
        ForceOptions();
        
        IL_Main.InitTargets += il =>
        {
            var c = new ILCursor(il);

            c.GotoNext(MoveType.After, x => x.MatchLdcI4(4096));
            c.EmitPop();
            c.EmitLdcI4(hidef_8k);
        };

        IL_Main.LoadSettings += il =>
        {
            var c = new ILCursor(il);

            c.GotoNext(MoveType.After, x => x.MatchStsfld<Main>(nameof(Main._renderTargetMaxSize)));
            c.EmitDelegate(ForceOptions);
        };

        Main.QueueMainThreadAction(Main.instance.InitTargets);
    }

    private static void ForceOptions()
    {
        Main.maxScreenW = hidef_8k;
        Main.maxScreenH = hidef_8k;
        Main._renderTargetMaxSize = hidef_8k;
        /*
        Main.Support4K = true;
        Main.Support8K = true;
        */
        Main.SupportWideScreen = true;
    }
}
