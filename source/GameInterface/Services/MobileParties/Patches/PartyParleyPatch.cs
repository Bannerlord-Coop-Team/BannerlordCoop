using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// v1.5.6 lets the player Ctrl+Click a party on the map to talk to it at range. A client would start a local
/// conversation with a party the server owns, so a party parley is refused until it goes through the server (#4601).
/// MapScreen.TryParleyWith shows the explanation, and the party tooltip drops its parley hint. A settlement parley
/// is unchanged.
/// </summary>
[HarmonyPatch(typeof(DefaultEncounterModel), nameof(DefaultEncounterModel.CanMainHeroDoParleyWithParty))]
internal class PartyParleyPatch
{
    [HarmonyPostfix]
    private static void Postfix(PartyBase partyBase, ref bool __result, ref TextObject explanation)
    {
        if (!partyBase.IsMobile) return;

        __result = false;
        explanation = new TextObject("{=!}You can't start a parley with a party in a shared campaign yet.");
    }
}
