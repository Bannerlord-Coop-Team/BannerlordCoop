using Common.Messaging;
using GameInterface.Services.Incidents.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Incidents.Patches;

[HarmonyPatch(typeof(Incident))]
internal class IncidentPatches
{
    [HarmonyPatch(nameof(Incident.InvokeOption))]
    [HarmonyPrefix]
    public static bool InvokeOptionPrefix(Incident __instance, ref List<TextObject> __result, int index)
    {
        // Quick information messages sent to client after server applies effects
        __result = new();

        var message = new IncidentOptionInvoked(Hero.MainHero, MobileParty.MainParty, __instance, __instance._options[index].Text.GetID());
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}
