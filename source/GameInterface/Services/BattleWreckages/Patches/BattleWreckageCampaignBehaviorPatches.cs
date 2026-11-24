using Common;
using Common.Messaging;
using GameInterface.Services.BattleWreckages.Interfaces;
using GameInterface.Services.BattleWreckages.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.BattleWreckages.Patches;

[HarmonyPatch(typeof(BattleWreckageCampaignBehavior))]
internal class BattleWreckageCampaignBehaviorPatches
{
    /// <summary>
    /// IsPlayerMapEvent call covered by IsPlayerMapEventPatch
    /// </summary>
    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.OnMapEventEnded))]
    [HarmonyPrefix]
    public static bool OnMapEventEndedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.OnSessionLaunched))]
    [HarmonyPrefix]
    public static bool OnSessionLaunchedPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.OnHourlyTick))]
    [HarmonyPrefix]
    public static bool OnHourlyTickPrefix()
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IBattleWreckageCampaignBehaviorInterface>(out var battleWreckageCampaignBehaviorInterface)) return true;

        battleWreckageCampaignBehaviorInterface.OnHourlyTick();

        return false;
    }

    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.OnAiHourlyTickEvent))]
    [HarmonyPrefix]
    public static bool OnAiHourlyTickEventPrefix() => ModInformation.IsServer;

    /// <summary>
    /// Server detected and PvP encounters fire this from ConversationRequestHandler.ProcessConversationRequest
    /// </summary>
    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.OnPartyEncounter))]
    [HarmonyPrefix]
    public static bool OnPartyEncounterPrefix(PartyBase attackerParty, PartyBase defenderParty)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IBattleWreckageCampaignBehaviorInterface>(out var battleWreckageCampaignBehaviorInterface)) return true;

        battleWreckageCampaignBehaviorInterface.OnPartyEncounter(attackerParty, defenderParty);

        return false;
    }

    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.OnHeroPrisonerTakenEvent))]
    [HarmonyPrefix]
    public static bool OnHeroPrisonerTakenEventPrefix(PartyBase capturerParty, Hero hero)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IBattleWreckageCampaignBehaviorInterface>(out var battleWreckageCampaignBehaviorInterface)) return true;

        battleWreckageCampaignBehaviorInterface.OnHeroPrisonerTakenEvent(hero);

        return false;
    }

    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.LeaveWreckageEncounter))]
    [HarmonyPrefix]
    public static bool LeaveWreckageEncounterPrefix(BattleWreckageCampaignBehavior __instance, BattleWreckageCampaignBehavior.WreckageLeaveReason reason)
    {
        // Server side calls for leaving wreckage encounters are handled in BattleWreckageCampaignBehaviorInterface.LeaveWreckageEncounter
        if (ModInformation.IsServer) return false;

        var message = new RequestLeaveWreckageEncounter(Hero.MainHero, reason);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.battle_remains_leave_consequence))]
    [HarmonyPrefix]
    public static bool BattleRemainsLeaveConsequencePrefix(BattleWreckageCampaignBehavior __instance)
    {
        // Vanilla clears the encounter here without LeaveWreckageEncounter, so the server would never hear about it
        if (ModInformation.IsServer) return false;

        var message = new RequestLeaveWreckageEncounter(Hero.MainHero, BattleWreckageCampaignBehavior.WreckageLeaveReason.LeaveWithoutInvestigation);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.ApplyWreckageInvestigationResults))]
    [HarmonyPrefix]
    public static bool ApplyWreckageInvestigationResultsPrefix(BattleWreckageCampaignBehavior __instance)
    {
        // Server side results are applied in BattleWreckageCampaignBehaviorInterface.ApplyWreckageInvestigationResults
        if (ModInformation.IsServer) return false;

        // Results arrive from the server once, then vanilla steps through them each time the connection menu reopens
        if (__instance._consequenceExplanations.Count > 0) return true;

        var message = new RequestApplyWreckageInvestigationResults(Hero.MainHero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.SelectWreckageConsequences))]
    [HarmonyPrefix]
    public static bool SelectWreckageConsequencesPrefix(BattleWreckageCampaignBehavior __instance)
    {
        // Consequences are rolled server side in BattleWreckageCampaignBehaviorInterface.SelectWreckageConsequences
        if (ModInformation.IsServer) return false;

        var message = new RequestSelectWreckageConsequences(Hero.MainHero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.game_menu_recover_troops_accept_consequence))]
    [HarmonyPrefix]
    public static bool RecoverTroopsAcceptConsequencePrefix(BattleWreckageCampaignBehavior __instance)
    {
        // Server side acceptance is handled in BattleWreckageCampaignBehaviorInterface.AcceptRecoverTroops
        if (ModInformation.IsServer) return false;

        var message = new RequestAcceptRecoverTroops(Hero.MainHero);
        MessageBroker.Instance.Publish(__instance, message);

        // Vanilla still sets the client's local start time and switches to the wait menu, the server copy in the tracker is authoritative
        return true;
    }
}
