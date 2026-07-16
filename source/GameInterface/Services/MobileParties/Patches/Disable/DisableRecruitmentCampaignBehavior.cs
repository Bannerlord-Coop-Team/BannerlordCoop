using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(RecruitmentCampaignBehavior))]
internal class DisableRecruitmentCampaignBehavior
{
    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    static bool PrefixRegisterEvents(RecruitmentCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return true;

        SubscribeSessionLaunchedOnly(__instance);
        return false;
    }

    [HarmonyPatch(nameof(RecruitmentCampaignBehavior.CheckRecruiting))]
    [HarmonyPrefix]
    /// Only allow recruiting for AI parties
    static bool PrefixCheckRecruiting(MobileParty mobileParty) => !mobileParty.IsPlayerParty();

    private static void SubscribeSessionLaunchedOnly(RecruitmentCampaignBehavior behavior)
    {
        var method = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "OnSessionLaunched");
        if (method == null) return;

        var onSessionLaunched = (Action<CampaignGameStarter>)Delegate.CreateDelegate(
            typeof(Action<CampaignGameStarter>),
            behavior,
            method);

        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(behavior, onSessionLaunched);
    }
}
