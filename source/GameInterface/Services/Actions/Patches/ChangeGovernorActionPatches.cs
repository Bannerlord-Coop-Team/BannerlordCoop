using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Actions.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(ChangeGovernorAction))]
internal class ChangeGovernorActionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ChangeGovernorActionPatches>();

    [HarmonyPatch(nameof(ChangeGovernorAction.ApplyInternal))]
    static bool ApplyInternalPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(ChangeGovernorAction.ApplyGiveUpInternal))]
    static bool ApplyGiveUpInternalPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(ChangeGovernorAction.Apply))]
    [HarmonyPrefix]
    public static bool ApplyPrefix(Town fortification, Hero governor)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new GovernorChanged(fortification, governor);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }

    [HarmonyPatch(nameof(ChangeGovernorAction.RemoveGovernorOf))]
    [HarmonyPrefix]
    public static bool RemoveGovernorOfPrefix(Hero governor)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        var message = new GovernorRemoved(governor);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }

    [HarmonyPatch(nameof(ChangeGovernorAction.RemoveGovernorOfIfExists))]
    [HarmonyPrefix]
    public static bool RemoveGovernorOfIfExistsPrefix(Town town)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;

        if (town.Governor != null)
        {
            var message = new GovernorRemoved(town.Governor);
            MessageBroker.Instance.Publish(null, message);
        }

        return false;
    }
}
