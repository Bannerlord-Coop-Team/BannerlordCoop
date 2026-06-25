using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Localization;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(GovernorCampaignBehavior))]
internal class GovernorKingdomCreationPatches
{
    private const string FinalizationMethodName = "governor_talk_kingdom_creation_finalization_on_consequence";
    private static readonly ILogger Logger = LogManager.GetLogger<GovernorKingdomCreationPatches>();

    [HarmonyPatch(FinalizationMethodName)]
    [HarmonyPrefix]
    public static bool FinalizationPrefix(GovernorCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return true;

        if (TryCreateKingdomCreationRequest(__instance, out KingdomCreationRequested request))
        {
            MessageBroker.Instance.Publish(__instance, request);
        }

        return false;
    }

    internal static bool TargetMethodExists()
    {
        return AccessTools.Method(typeof(GovernorCampaignBehavior), FinalizationMethodName) != null;
    }

    internal static bool TryCreateKingdomCreationRequest(
        GovernorCampaignBehavior behavior,
        out KingdomCreationRequested request)
    {
        request = null;

        if (behavior == null)
        {
            Logger.Warning("Unable to request kingdom creation because governor behavior was null.");
            return false;
        }

        var chosenName = behavior._kingdomCreationChosenName;
        var chosenCulture = behavior._kingdomCreationChosenCulture;
        string kingdomName = chosenName?.ToString();
        if (!TryGetCultureId(chosenCulture, out string cultureId))
        {
            Logger.Warning("Unable to request kingdom creation because the culture id could not be resolved.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(kingdomName) || string.IsNullOrWhiteSpace(cultureId))
        {
            Logger.Warning(
                "Unable to request kingdom creation. Name: {KingdomName}, CultureId: {CultureId}",
                kingdomName,
                cultureId);
            return false;
        }

        request = new KingdomCreationRequested(kingdomName, cultureId);
        return true;
    }

    private static bool TryGetCultureId(CultureObject culture, out string cultureId)
    {
        cultureId = null;
        if (culture == null) return false;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;

        return objectManager.TryGetIdWithLogging(culture, out cultureId);
    }
}
