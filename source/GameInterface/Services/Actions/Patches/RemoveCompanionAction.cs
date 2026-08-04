using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Actions.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(RemoveCompanionAction))]
internal class RemoveCompanionActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<RemoveCompanionActionPatches>();

    [HarmonyPatch(nameof(RemoveCompanionAction.ApplyInternal))]
    [HarmonyPrefix]
    public static bool ApplyInternalPrefix(Clan clan, Hero companion, RemoveCompanionAction.RemoveCompanionDetail detail)
    {
        // A removal can arrive after another sync channel already cleared the companionship
        // (e.g. an overlapping request, or the companion already left through another path).
        // Treat re-removing a non-companion as a no-op instead of re-announcing it, mirroring
        // ChangeGovernorActionPatches.ApplyGiveUpInternalPrefix's GovernorOf guard.
        if (companion?.CompanionOf != clan) return false;

        if (ModInformation.IsServer) return true;

        // Send a request to the server to remove the companion instead of applying the removal
        // (and its ApplyInternal-internal side effects: roster removal, fugitive/equipment reset,
        // governor cleanup) directly on the client.
        var message = new CompanionRemovalAttempted(clan, companion, detail);
        MessageBroker.Instance.Publish(companion, message);

        return false;
    }
}
