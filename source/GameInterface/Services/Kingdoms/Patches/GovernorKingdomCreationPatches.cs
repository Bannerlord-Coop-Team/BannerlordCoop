using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using Serilog;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Localization;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(GovernorCampaignBehavior))]
internal class GovernorKingdomCreationPatches
{
    private const string FinalizationMethodName = "governor_talk_kingdom_creation_finalization_on_consequence";
    private static readonly ILogger Logger = LogManager.GetLogger<GovernorKingdomCreationPatches>();
    private static readonly FieldInfo ChosenNameField =
        AccessTools.Field(typeof(GovernorCampaignBehavior), "_kingdomCreationChosenName");
    private static readonly FieldInfo ChosenCultureField =
        AccessTools.Field(typeof(GovernorCampaignBehavior), "_kingdomCreationChosenCulture");

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

        if (ChosenNameField == null || ChosenCultureField == null)
        {
            Logger.Error("Unable to request kingdom creation because governor creation fields could not be found.");
            return false;
        }

        var chosenName = ChosenNameField.GetValue(behavior) as TextObject;
        var chosenCulture = ChosenCultureField.GetValue(behavior) as CultureObject;
        string kingdomName = chosenName?.ToString();
        string cultureId = chosenCulture?.StringId;

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
}
