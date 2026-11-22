using GameInterface.Policies;
using GameInterface.Services.PartyVisuals.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Patches;

/// <summary>
/// Asks <see cref="IPartyVisualInterface"/> to replicate a new party's visual from a headless server.
/// </summary>
[HarmonyPatch]
internal class HeadlessPartyVisualCreationPatches
{
    // The party constructor patch assigns its handle before the campaign adds it.
    [HarmonyPatch(typeof(CampaignObjectManager), nameof(CampaignObjectManager.AddMobileParty))]
    [HarmonyPostfix]
    private static void AddMobilePartyPostfix(MobileParty party)
    {
        // Skip replicating the visual for parties created during save loading or while applying a received action.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ContainerProvider.TryResolve<IPartyVisualInterface>(out var partyVisuals))
            partyVisuals.OnPartyAdded(party);
    }
}
