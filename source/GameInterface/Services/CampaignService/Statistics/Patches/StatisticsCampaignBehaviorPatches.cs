using Common;
using GameInterface.Services.CampaignService.Statistics.Interfaces;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.CampaignService.Statistics.Patches;

/// <summary>
/// The handlers write to fields that assume one player, so only OnMissionStarted runs on clients. The server
/// records the handlers it can attribute to a player through the statistics interface.
/// </summary>
[HarmonyPatch(typeof(StatisticsCampaignBehavior))]
internal class StatisticsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnHeroCreated))]
    [HarmonyPrefix]
    public static bool OnHeroCreatedPrefix(Hero hero)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnHeroCreated(hero);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnIssueUpdated))]
    [HarmonyPrefix]
    public static bool OnIssueUpdatedPrefix(IssueBase issue, IssueBase.IssueUpdateDetails details, Hero issueSolver)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnIssueUpdated(issue, details, issueSolver);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnTournamentFinished))]
    [HarmonyPrefix]
    public static bool OnTournamentFinishedPrefix(CharacterObject winner)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnTournamentFinished(winner);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnClanInfluenceChanged))]
    [HarmonyPrefix]
    public static bool OnClanInfluenceChangedPrefix(Clan clan, float change)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnClanInfluenceChanged(clan, change);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnCrimeRatingChanged))]
    [HarmonyPrefix]
    public static bool OnCrimeRatingChangedPrefix(float deltaCrimeAmount)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnCrimeRatingChanged(deltaCrimeAmount);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnBeforeSave))]
    [HarmonyPrefix]
    public static bool OnBeforeSavePrefix()
    {
        if (TryGetServerStatistics(out var statistics)) statistics.UpdatePlayTime();
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnHeroOrPartyTradedGold))]
    [HarmonyPrefix]
    public static bool OnHeroOrPartyTradedGoldPrefix((Hero, PartyBase) recipient, (int, string) goldAmount)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnHeroOrPartyTradedGold(recipient.Item1, recipient.Item2, goldAmount.Item1);
        return false;
    }

    // The battle size is read when the map event ends instead, so players joining after the start are included
    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnMapEventStarted))]
    [HarmonyPrefix]
    public static bool OnMapEventStartedPrefix() => false;

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnMapEventEnd))]
    [HarmonyPrefix]
    public static bool OnMapEventEndPrefix(MapEvent mapEvent)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnMapEventEnded(mapEvent);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnClanDestroyed))]
    [HarmonyPrefix]
    public static bool OnClanDestroyedPrefix(Clan clan)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnClanDestroyed(clan);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnPartyAttachedAnotherParty))]
    [HarmonyPrefix]
    public static bool OnPartyAttachedAnotherPartyPrefix(MobileParty mobileParty)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnArmyChanged(mobileParty.Army);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnArmyCreated))]
    [HarmonyPrefix]
    public static bool OnArmyCreatedPrefix(Army army)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnArmyChanged(army);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnHeroPrisonerTaken))]
    [HarmonyPrefix]
    public static bool OnHeroPrisonerTakenPrefix(PartyBase capturer)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnHeroPrisonerTaken(capturer);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnRaidCompleted))]
    [HarmonyPrefix]
    public static bool OnRaidCompletedPrefix(BattleSideEnum winnerSide, RaidEventComponent raidEventComponent)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnRaidCompleted(winnerSide, raidEventComponent.MapEvent);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnHideoutBattleCompleted))]
    [HarmonyPrefix]
    public static bool OnHideoutBattleCompletedPrefix(BattleSideEnum winnerSide, HideoutEventComponent hideoutEventComponent)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnHideoutBattleCompleted(winnerSide, hideoutEventComponent.MapEvent);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnHeroPrisonerReleased))]
    [HarmonyPrefix]
    public static bool OnHeroPrisonerReleasedPrefix(Hero prisoner)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnHeroPrisonerReleased(prisoner);
        return false;
    }

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnHeroKilled))]
    [HarmonyPrefix]
    public static bool OnHeroKilledPrefix(Hero killer, KillCharacterAction.KillCharacterActionDetail detail)
    {
        if (TryGetServerStatistics(out var statistics)) statistics.OnHeroKilled(killer, detail);
        return false;
    }

    // Missions run on clients, which count their own kills and send them when the mission ends
    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnMissionStarted))]
    [HarmonyPrefix]
    public static bool OnMissionStartedPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnAgentRemoved))]
    [HarmonyPrefix]
    public static bool OnAgentRemovedPrefix(Agent affectedAgent, Agent affectorAgent)
    {
        if (ModInformation.IsServer) return false;

        if (!ContainerProvider.TryResolve<IStatisticsCampaignBehaviorInterface>(out var statistics)) return false;

        statistics.OnAgentRemoved(affectedAgent, affectorAgent);
        return false;
    }

    // These events carry no player or never fire on the server, so the co-op code that replaces them records the statistic
    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnAfterSessionLaunched))]
    [HarmonyPrefix]
    public static bool OnAfterSessionLaunchedPrefix() => false;

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnMainPartyPrisonerRecruited))]
    [HarmonyPrefix]
    public static bool OnMainPartyPrisonerRecruitedPrefix() => false;

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnUnitRecruited))]
    [HarmonyPrefix]
    public static bool OnUnitRecruitedPrefix() => false;

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnCraftingPartUnlocked))]
    [HarmonyPrefix]
    public static bool OnCraftingPartUnlockedPrefix() => false;

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnNewItemCrafted))]
    [HarmonyPrefix]
    public static bool OnNewItemCraftedPrefix() => false;

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnPrisonersTaken))]
    [HarmonyPrefix]
    public static bool OnPrisonersTakenPrefix() => false;

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnPlayerPartyKnockedOrKilledTroop))]
    [HarmonyPrefix]
    public static bool OnPlayerPartyKnockedOrKilledTroopPrefix() => false;

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnPlayerEarnedGoldFromAsset))]
    [HarmonyPrefix]
    public static bool OnPlayerEarnedGoldFromAssetPrefix() => false;

    // Players trading companions also fire this, so hires are counted from AddCompanionAction instead
    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnNewCompanionAdded))]
    [HarmonyPrefix]
    public static bool OnNewCompanionAddedPrefix() => false;

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnDefectionPersuasionSucess))]
    [HarmonyPrefix]
    public static bool OnDefectionPersuasionSucessPrefix() => false;

    [HarmonyPatch(nameof(StatisticsCampaignBehavior.OnPlayerAcceptedRansomOffer))]
    [HarmonyPrefix]
    public static bool OnPlayerAcceptedRansomOfferPrefix() => false;

    private static bool TryGetServerStatistics(out IStatisticsCampaignBehaviorInterface statistics)
    {
        statistics = null;
        return ModInformation.IsServer && ContainerProvider.TryResolve(out statistics);
    }
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMissionEnded))]
internal class StatisticsMissionEndedPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        if (ModInformation.IsServer) return;

        if (!ContainerProvider.TryResolve<IStatisticsCampaignBehaviorInterface>(out var statistics)) return;

        statistics.SendMissionStatistics();
    }
}

/// <summary>
/// Replaces StatisticsCampaignBehavior.OnNewCompanionAdded
/// </summary>
[HarmonyPatch(typeof(AddCompanionAction), nameof(AddCompanionAction.ApplyInternal))]
internal class StatisticsCompanionHiredPatch
{
    [HarmonyPostfix]
    public static void Postfix(Clan clan)
    {
        if (ModInformation.IsClient) return;
        if (!ContainerProvider.TryResolve<IStatisticsCampaignBehaviorInterface>(out var statistics)) return;

        statistics.OnCompanionHired(clan);
    }
}

/// <summary>
/// Replaces StatisticsCampaignBehavior.OnPlayerPartyKnockedOrKilledTroop
/// </summary>
[HarmonyPatch(typeof(MapEventSide), nameof(MapEventSide.ApplySimulatedHitRewardToSelectedTroop))]
internal class StatisticsSimulatedHitPatch
{
    [HarmonyPostfix]
    public static void Postfix(MapEventSide __instance, bool isFinishingStrike)
    {
        if (!isFinishingStrike || ModInformation.IsClient) return;
        if (!ContainerProvider.TryResolve<IStatisticsCampaignBehaviorInterface>(out var statistics)) return;
        if (!__instance._allocatedTroops.TryGetValue(__instance._selectedSimulationTroopDescriptor, out var allocatedTroop)) return;

        statistics.OnSimulatedTroopKnockedOrKilled(allocatedTroop.Party);
    }
}
