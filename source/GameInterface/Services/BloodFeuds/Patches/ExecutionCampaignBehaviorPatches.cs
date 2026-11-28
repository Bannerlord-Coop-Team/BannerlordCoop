using Common;
using GameInterface.Services.BloodFeuds.Interfaces;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.BloodFeuds.Patches;

/// <summary>
/// Runs every <see cref="ExecutionCampaignBehavior"/> listener through <see cref="IExecutionCampaignBehaviorInterface"/> on the server,
/// the private helpers they call are only reached from these listeners.
/// </summary>
[HarmonyPatch(typeof(ExecutionCampaignBehavior))]
internal class ExecutionCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnPrisonerDonatedToSettlement))]
    [HarmonyPrefix]
    private static bool OnPrisonerDonatedToSettlementPrefix(MobileParty oldOwner, FlattenedTroopRoster prisonerRoster)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        executionInterface.OnPrisonerDonatedToSettlement(oldOwner, prisonerRoster);
        return false;
    }

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnHeroKilled))]
    [HarmonyPrefix]
    private static bool OnHeroKilledPrefix(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        executionInterface.OnHeroKilled(victim, killer, detail);
        return false;
    }

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnHeroRelationChanged))]
    [HarmonyPrefix]
    private static bool OnHeroRelationChangedPrefix(Hero effectiveHero, Hero effectiveHeroGainedRelationWith)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        executionInterface.OnHeroRelationChanged(effectiveHero, effectiveHeroGainedRelationWith);
        return false;
    }

    // The server changes feuds per player clan in the interface, the client fires this only to refresh feud icons
    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnBloodFeudStateChanged))]
    [HarmonyPrefix]
    private static bool OnBloodFeudStateChangedPrefix() => !ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out _);

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnHeroTakenPrisoner))]
    [HarmonyPrefix]
    private static bool OnHeroTakenPrisonerPrefix(PartyBase capturer, Hero prisoner)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        executionInterface.OnHeroTakenPrisoner(capturer, prisoner);
        return false;
    }

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnHeroPrisonerReleased))]
    [HarmonyPrefix]
    private static bool OnHeroPrisonerReleasedPrefix()
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        executionInterface.UpdateAllPlayerClanMemberExecutionStates();
        return false;
    }

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnSettlementOwnerChanged))]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix()
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        executionInterface.UpdateAllPlayerClanMemberExecutionStates();
        return false;
    }

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnClanDestroyed))]
    [HarmonyPrefix]
    private static bool OnClanDestroyedPrefix()
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        executionInterface.UpdateAllPlayerClanMemberExecutionStates();
        return false;
    }

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnPrisonersChangedInSettlement))]
    [HarmonyPrefix]
    private static bool OnPrisonersChangedInSettlementPrefix()
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        executionInterface.UpdateAllPlayerClanMemberExecutionStates();
        return false;
    }

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.DailyTickHero))]
    [HarmonyPrefix]
    private static bool DailyTickHeroPrefix(Hero hero)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        executionInterface.DailyTickHero(hero);
        return false;
    }

    // PlayerCaptivityServerHandler executes a captured player without the conversation
    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnGameMenuOpened))]
    [HarmonyPrefix]
    private static bool OnGameMenuOpenedPrefix() => !ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out _);

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.QuarterHourlyTick))]
    [HarmonyPrefix]
    private static bool QuarterHourlyTickPrefix()
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        executionInterface.QuarterHourlyTick();
        return false;
    }

    // HeroExecutionScenePatch executes with Executed, so nothing adds an ExecutionAfterMapEvent mark for this to track
    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnDeathMarkAdded))]
    [HarmonyPrefix]
    private static bool OnDeathMarkAddedPrefix() => !ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out _);

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.CanHeroBeReleased))]
    [HarmonyPrefix]
    private static bool CanHeroBeReleasedPrefix(Hero hero, ref bool result)
    {
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        executionInterface.CanHeroBeReleased(hero, ref result);
        return false;
    }

    // Adds the ransom broker feud and lord_executes_player dialog lines
    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnSessionLaunched))]
    [HarmonyPrefix]
    private static bool OnSessionLaunchedPrefix() => ModInformation.IsClient;

    private const string RansomBrokerEndFeudLineId = "ransom_broker_talk_end_blood_feud";
    private const string RansomBrokerFeudClanLineId = "ransom_broker_end_feud_clan";

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.OnSessionLaunched))]
    [HarmonyPostfix]
    private static void OnSessionLaunchedPostfix()
    {
        var sentences = Campaign.Current.ConversationManager._sentences;

        var endFeudSentence = sentences.FirstOrDefault(sentence => sentence.Id == RansomBrokerEndFeudLineId);
        if (endFeudSentence != null) endFeudSentence.OnClickableCondition = RansomBrokerEndFeudClickableCondition;

        var feudClanSentence = sentences.FirstOrDefault(sentence => sentence.Id == RansomBrokerFeudClanLineId);
        if (feudClanSentence != null) feudClanSentence.OnClickableCondition = RansomBrokerFeudClanClickableCondition;
    }

    private static bool RansomBrokerEndFeudClickableCondition(out TextObject explanation)
    {
        if (!Hero.MainHero.IsClanLeader)
        {
            explanation = GameTexts.FindText("str_coop_clan_blood_feud_leader_only");
            return false;
        }

        explanation = null;
        return true;
    }

    // Player clans end their feuds with each other by agreement in a trade
    private static bool RansomBrokerFeudClanClickableCondition(out TextObject explanation)
    {
        if (ConversationSentence.CurrentProcessedRepeatObject is Clan clan && clan.IsPlayerClan())
        {
            explanation = GameTexts.FindText("str_coop_blood_feud_player_clan_agreement_only");
            return false;
        }

        explanation = null;
        return true;
    }

    [HarmonyPatch(nameof(ExecutionCampaignBehavior.conversation_ransom_broker_end_feud_on_consequence))]
    [HarmonyPrefix]
    private static bool RansomBrokerEndFeudConsequencePrefix(ExecutionCampaignBehavior __instance)
    {
        if (!ContainerProvider.TryResolve<IExecutionCampaignBehaviorInterface>(out var executionInterface)) return true;

        Clan selectedClan = __instance._selectedBloodFeudClan;
        if (selectedClan != null && selectedClan.HasBloodFeudWithPlayer)
        {
            executionInterface.RequestSettleBloodFeudByRansomPayment(selectedClan);
        }
        return false;
    }
}
