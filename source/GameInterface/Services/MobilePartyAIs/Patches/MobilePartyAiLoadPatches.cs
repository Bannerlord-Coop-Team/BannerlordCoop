using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Patches;

[HarmonyPatch(typeof(MobilePartyAi))]
internal class MobilePartyAiLoadPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyAiLoadPatches>();

    [HarmonyPatch("OnLateLoad")]
    [HarmonyPostfix]
    private static void OnLateLoadPostfix(MobilePartyAi __instance)
    {
        EnsureFleeingData(__instance);
    }

    internal static bool EnsureFleeingData(MobilePartyAi partyAi)
    {
        if (partyAi._fleeingData != null)
        {
            return false;
        }

        partyAi._fleeingData = new MobilePartyAi.FleeingData();
        Logger.Warning("Restored missing fleeing data for mobile party {PartyId}",
            partyAi._mobileParty?.StringId);
        return true;
    }
}
