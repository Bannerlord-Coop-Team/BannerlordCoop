using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface;
using GameInterface.Services.Entity;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Disables party encounters only on server; allows encounters on client.
/// </summary>
[HarmonyPatch(typeof(EncounterManager))]
public class DisablePartyEncounterPatch
{
    [HarmonyPatch(nameof(EncounterManager.StartPartyEncounter))]
    [HarmonyPrefix]
    private static bool StartPartyEncounterPrefix(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (!ModInformation.IsServer) return true;

        try
        {
            var attackerMobile = attackerParty?.MobileParty;
            var defenderMobile = defenderParty?.MobileParty;

            if ((attackerMobile != null && attackerMobile.IsPlayerParty()) ||
                (defenderMobile != null && defenderMobile.IsPlayerParty()))
            {
                return true;
            }

            if (ContainerProvider.TryResolve<IControlledEntityRegistry>(out var registry))
            {
                if ((attackerMobile != null && registry.TryGetControlledEntity(attackerMobile.StringId, out var _)) ||
                    (defenderMobile != null && registry.TryGetControlledEntity(defenderMobile.StringId, out var _)))
                {
                    return true;
                }
            }
        }
        catch { }

        return false;
    }
}
