using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.StanceLinks.Messages;
using HarmonyLib;
using System.Linq;
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
    [HarmonyPatch(typeof(FactionManager), nameof(FactionManager.RemoveFactionsFromCampaignWars))]
    [HarmonyPrefix]
    private static bool Prefix(FactionManager __instance, IFaction faction1)
    {
        if (ModInformation.IsClient) return false;

        if (faction1.MapFaction != faction1)
        {
            return false;
        }
        StanceLink[] removedStances = (from x in __instance._stances.GetStanceLinks()
                                       where x.Faction1 == faction1 || x.Faction2 == faction1
                                       select x).ToArray();

        foreach (StanceLink stance in removedStances)
        {
            __instance.RemoveStance(stance);
        }
        foreach (IFaction faction2 in faction1.FactionsAtWarWith)
        {
            faction2.UpdateFactionsAtWarWith();
        }
        faction1.UpdateFactionsAtWarWith();
        MessageBroker.Instance.Publish(__instance, new StanceLinkDeconstructed(faction1, removedStances));
        return false;
    }
}

