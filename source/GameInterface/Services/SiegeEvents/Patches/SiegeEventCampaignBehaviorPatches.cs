using Common;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Picks which side runs the vanilla siege behavior's handlers (campaign mutations on the server, menus everywhere)
/// and restricts the assault menu options to a fully synced siege commanded by the BesiegerCamp leader.
/// </summary>
[HarmonyPatch(typeof(SiegeEventCampaignBehavior))]
internal class SiegeEventCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.OnSiegeEventStarted))]
    [HarmonyPrefix]
    private static bool OnSiegeEventStartedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.OnSiegeEngineBuilt))]
    [HarmonyPrefix]
    private static bool OnSiegeEngineBuiltPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.OnSiegeEngineDestroyed))]
    [HarmonyPrefix]
    private static bool OnSiegeEngineDestroyedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.OnSiegeEngineHit))]
    [HarmonyPrefix]
    private static bool OnSiegeEngineHitPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.OnSiegeBombardmentWallHit))]
    [HarmonyPrefix]
    private static bool OnSiegeBombardmentWallHitPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.OnPeaceDeclared))]
    [HarmonyPrefix]
    private static bool OnPeaceDeclaredPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IAiSiegeTerminalPolicy>(out var terminalPolicy))
            terminalPolicy.SyncData(dataStore);
    }

    // Vanilla only reacts to MobileParty.MainParty leaving the besieged settlement, which never
    // matches on the dedicated host; re-derive the trigger as "a player-led party left".
    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.OnSettlementLeft))]
    [HarmonyPrefix]
    private static bool OnSettlementLeftPrefix(SiegeEventCampaignBehavior __instance, MobileParty party, Settlement settlement)
    {
        if (ModInformation.IsClient) return false;

        if (settlement.SiegeEvent != null && party?.LeaderHero != null && party.LeaderHero.IsPlayerHero())
        {
            __instance.SetDefaultTactics(settlement.SiegeEvent, BattleSideEnum.Defender);
        }

        return false;
    }

    // Vanilla compares the side leader to Hero.MainHero, which is null on the dedicated host: a
    // leaderless garrison side would match null == null and get the Custom (player decides, AI idles)
    // strategy, and a player-led side would get an AI strategy. Same logic with the player check fixed.
    // Defenders stay Custom while any involved defender is player-led, so AI never plans over co-op builds.
    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.SetDefaultTactics))]
    [HarmonyPrefix]
    private static bool SetDefaultTacticsPrefix(SiegeEvent siegeEvent, BattleSideEnum side)
    {
        // Strategy is server-authoritative and replicates via the SiegeStrategy sync; the client must
        // not roll its own RNG here, matching every sibling handler's role gate.
        if (ModInformation.IsClient) return false;

        var leader = Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(siegeEvent, side);
        SiegeStrategy strategy = null;
        if (leader != null && leader.IsPlayerHero())
        {
            strategy = DefaultSiegeStrategies.Custom;
        }
        else if (side == BattleSideEnum.Defender && HasPlayerDefender(siegeEvent))
        {
            strategy = DefaultSiegeStrategies.Custom;
        }
        else
        {
            var strategies = side == BattleSideEnum.Attacker
                ? DefaultSiegeStrategies.AllAttackerStrategies
                : DefaultSiegeStrategies.AllDefenderStrategies;
            float bestScore = float.MinValue;
            foreach (var item in strategies)
            {
                float score = Campaign.Current.Models.SiegeEventModel.GetSiegeStrategyScore(siegeEvent, side, item) * (0.5f + (0.5f * MBRandom.RandomFloat));
                if (score > bestScore)
                {
                    bestScore = score;
                    strategy = item;
                }
            }
        }

        siegeEvent.GetSiegeEventSide(side).SetSiegeStrategy(strategy);
        return false;
    }

    private static bool HasPlayerDefender(SiegeEvent siegeEvent)
    {
        if (ContainerProvider.TryResolve<ISiegeDefenderCommandAuthority>(out var authority))
            return authority.HasPlayerDefender(siegeEvent, BattleSideEnum.Defender);
        return false;
    }

    // Hides "Lead assault" until the client's siege graph is fully synced.
    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.game_menu_siege_strategies_lead_assault_on_condition))]
    [HarmonyPrefix]
    private static bool LeadAssaultConditionPrefix(MenuCallbackArgs args, ref bool __result) =>
        ValidateSiegeGraph(args, GameMenuOption.LeaveType.LeadAssault, ref __result);

    // Hides "Order troops to attack" until the client's siege graph is fully synced.
    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.game_menu_siege_strategies_order_assault_on_condition))]
    [HarmonyPrefix]
    private static bool OrderAssaultConditionPrefix(MenuCallbackArgs args, ref bool __result) =>
        ValidateSiegeGraph(args, GameMenuOption.LeaveType.OrderTroopsToAttack, ref __result);

    // Only the BesiegerCamp leader can lead the assault.
    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.game_menu_siege_strategies_lead_assault_on_condition))]
    [HarmonyPostfix]
    private static void LeadAssaultConditionPostfix(MenuCallbackArgs args, bool __result) => DisableForCoBesieger(args, __result);

    // Only the BesiegerCamp leader can order the assault.
    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.game_menu_siege_strategies_order_assault_on_condition))]
    [HarmonyPostfix]
    private static void OrderAssaultConditionPostfix(MenuCallbackArgs args, bool __result) => DisableForCoBesieger(args, __result);

    // Disables a shown assault option when the main party is not the synced BesiegerCamp leader; joining an active
    // assault stays on the vanilla encounter flow.
    private static void DisableForCoBesieger(MenuCallbackArgs args, bool result)
    {
        if (!result) return;

        var settlement = MobileParty.MainParty?.BesiegedSettlement;
        if (settlement == null) return;

        var leader = settlement.SiegeEvent?.BesiegerCamp?.LeaderParty;
        if (leader == MobileParty.MainParty) return;

        args.IsEnabled = false;
        args.Tooltip = new TextObject("{=!}Only the siege leader can command the assault.");
    }

    // Lets vanilla evaluate the option only when the settlement, camp and engine containers all point at the player's siege.
    private static bool ValidateSiegeGraph(
        MenuCallbackArgs args,
        GameMenuOption.LeaveType leaveType,
        ref bool result)
    {
        args.optionLeaveType = leaveType;

        var siegeEvent = PlayerSiege.PlayerSiegeEvent;
        var settlement = siegeEvent?.BesiegedSettlement;
        if (settlement?.SiegeEvent == siegeEvent &&
            siegeEvent.BesiegerCamp?.SiegeEvent == siegeEvent &&
            (PlayerSiege.PlayerSide != BattleSideEnum.Attacker ||
             (MobileParty.MainParty?.BesiegerCamp == siegeEvent.BesiegerCamp &&
              siegeEvent.BesiegerCamp.SiegeEngines != null &&
              settlement.SiegeEngines != null)))
        {
            return true;
        }

        result = false;
        return false;
    }
}

// Retry on an hourly campaign turn, after any map-event teardown that deferred the decision has completed.
[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
internal class AiSiegeTerminalRetryPatches
{
    [HarmonyPostfix]
    private static void HourlyTickPostfix()
    {
        if (ModInformation.IsClient) return;
        if (!ContainerProvider.TryResolve<IAiSiegeTerminalPolicy>(out var terminalPolicy)) return;

        terminalPolicy.RetryDeferredTransitions();
    }
}
