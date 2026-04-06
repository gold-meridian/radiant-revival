using System.Runtime.CompilerServices;
using Terraria.Initializers;
using Terraria.UI;

namespace RadiantRevival.Common;

internal static class HideAchievements
{
#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Init()
    {
        On_AchievementInitializer.OnAchievementCompleted += (_, _) => { };
        On_InGameNotificationsTracker.AddCompleted += (_, _) => { };
    }
#pragma warning restore CA2255
}
