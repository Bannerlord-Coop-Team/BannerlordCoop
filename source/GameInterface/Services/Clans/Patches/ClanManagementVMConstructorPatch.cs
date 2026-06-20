using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ClanManagementVM))]
internal class ClanManagementVMConstructorPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanManagementVMConstructorPatch>();

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(Action), typeof(Action<Hero>), typeof(Action<Hero>), typeof(Action) })]
    [HarmonyPostfix]
    public static void ClanManagementVMConstructorPostfix(ClanManagementVM __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        MessageBroker.Instance.Publish(__instance, new ClanManagementVMCreated(__instance));
    }
}
