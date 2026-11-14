using Common.Messaging;
using GameInterface.Services.Party.Messages;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party.Patches;

/// <summary>
/// The done handlers only run on the client, so their effects are skipped here. The server runs them from the
/// party screen commit, see <see cref="PartyScreenCommandProcessor"/>.
/// </summary>
[HarmonyPatch(typeof(PartyScreenHelper))]
internal class PartyScreenHelperPatches
{
    // The server builds the new party from the screen's commit, see PartyScreenCommandProcessor.CreateClanParty.
    [HarmonyPatch(nameof(PartyScreenHelper.OpenScreenAsCreateClanPartyForHeroPartyScreenClosed))]
    [HarmonyPrefix]
    public static bool OpenScreenAsCreateClanPartyForHeroPartyScreenClosedPrefix() => false;

    [HarmonyPatch(nameof(PartyScreenHelper.SellPrisonersDoneHandler))]
    [HarmonyPrefix]
    public static bool SellPrisonersDoneHandlerPrefix(ref bool __result, TroopRoster leftPrisonRoster)
    {
        var message = new PrisonersSold(MobileParty.MainParty.Party, leftPrisonRoster);
        MessageBroker.Instance.Publish(null, message);

        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(PartyScreenHelper.DonateGarrisonDoneHandler))]
    [HarmonyPrefix]
    public static bool DonateGarrisonDoneHandlerPrefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(PartyScreenHelper.DonatePrisonersDoneHandler))]
    [HarmonyPrefix]
    public static bool DonatePrisonersDoneHandlerPrefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(PartyScreenHelper.ManageGarrisonDoneHandler))]
    [HarmonyPrefix]
    public static bool ManageGarrisonDoneHandlerPrefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(PartyScreenHelper.HandleReleasedAndTakenPrisoners))]
    [HarmonyPrefix]
    public static bool HandleReleasedAndTakenPrisonersPrefix() => false;
}
