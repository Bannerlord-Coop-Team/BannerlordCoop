using Common;
using GameInterface.Services.BloodFeuds.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

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

    // The server changes feuds per player in the interface, the client fires this only to refresh feud icons
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
    private static bool OnDeathMarkAddedPrefix() => false;

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
