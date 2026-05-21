using Common.Logging;
using HarmonyLib;
using SandBox.View.Map;
using SandBox.View.Map.Managers;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Patches;

[HarmonyPatch(typeof(MapScreen))]
internal class PartyVisualsRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyVisualsRobustnessPatches>();

    private static readonly HashSet<string> LoggedMissingVisuals = new HashSet<string>();

    [HarmonyPatch(nameof(MapScreen.StepSounds))]
    [HarmonyPrefix]
    static bool StepSoundsPrefix(MobileParty party)
    {
        if (!MobilePartyVisualManager.Current._partiesAndVisuals.ContainsKey(party.Party))
        {
            var stringId = party.StringId ?? "<null>";

            if (LoggedMissingVisuals.Add(stringId))
            {
                Logger.Warning(
                    "Skipping {MethodName} for party {StringId} because no party visual was registered in {ManagerType}",
                    nameof(MapScreen.StepSounds),
                    stringId,
                    nameof(MobilePartyVisualManager));
            }

            return false;
        }

        return true;
    }
}
