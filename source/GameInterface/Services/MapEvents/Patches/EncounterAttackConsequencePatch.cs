using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Helpers;
using Serilog;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MenuHelper))]
internal class EncounterAttackConsequencePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<EncounterAttackConsequencePatch>();

    [HarmonyPatch(nameof(MenuHelper.EncounterAttackConsequence))]
    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
            return true;

        // Server can run the original consequence normally.
        if (ModInformation.IsServer)
            return true;

        var battle = PlayerEncounter.Battle;

        if (battle == null)
        {
            Logger.Warning("Client tried to start attack mission, but PlayerEncounter.Battle was null");
            return false;
        }

        // Ask the server for the authoritative map event state / mission start.
        MessageBroker.Instance.Publish(
            battle,
            new AttackMissionAttempted(battle));

        return false;
    }
}
