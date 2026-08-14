using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.StanceLinks.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace GameInterface.Services.StanceLinks;
/// <summary>
/// Allow clients to create a local StanceLink,
/// but send it to the server, which also creates a StanceLink with the same faction-pair.
/// Then the server sends the clients a new StanceLink if they did not have one.
/// After that, the clients assign an id (does not matter if they already had the StanceLink).
/// </summary>
[HarmonyPatch]
internal class StanceLinkPatches
{
    [HarmonyPatch(typeof(FactionManager), nameof(FactionManager.GetStanceLinkInternal))]
    [HarmonyPrefix]
    private static bool Prefix(FactionManager __instance, IFaction faction1, IFaction faction2, ref StanceLink __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (faction1.IsBanditFaction != faction2.IsBanditFaction) return true;

        StanceLink stanceLink = __instance._stances.GetStance(faction1, faction2);
        if (stanceLink == null)
        {
            stanceLink = new StanceLink((
                Campaign.Current.Models.DiplomacyModel.GetDefaultDiplomaticStance(faction1, faction2) == DiplomacyModel.DiplomacyStance.War) ? StanceType.War : StanceType.Neutral,
                faction1,
                faction2);
            __instance.AddStance(faction1, faction2, stanceLink);
            MessageBroker.Instance.Publish(__instance, new RequestStanceLinkConstructed(stanceLink));
        }
        __result = stanceLink;
        return false;
    }
}

