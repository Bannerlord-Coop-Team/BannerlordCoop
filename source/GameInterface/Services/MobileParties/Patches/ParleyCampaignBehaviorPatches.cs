using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// v1.5.6 lets the player Ctrl+Click a party on the map to talk to it at range (MapScreen.HandleParleyClick ends in
/// StartParley). A client would start a local conversation with a party the server owns, so clients refuse a party
/// parley until it goes through the server (#4601). A settlement parley is unchanged.
/// </summary>
[HarmonyPatch(typeof(ParleyCampaignBehavior), nameof(ParleyCampaignBehavior.StartParley))]
internal class ParleyCampaignBehaviorPatches
{
    [HarmonyPrefix]
    internal static bool StartParleyPrefix(PartyBase partyBase) => ModInformation.IsServer || !partyBase.IsMobile;
}
