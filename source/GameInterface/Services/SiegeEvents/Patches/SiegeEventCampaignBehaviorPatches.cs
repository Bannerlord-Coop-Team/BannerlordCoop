using Common;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Role gates for the vanilla siege behavior's handlers. Menu registration (OnSessionLaunched) runs
/// everywhere so clients get the siege menus; the campaign mutations (tactics, XP, RNG casualties)
/// run on the server only and reach clients as sync messages.
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
        else
        {
            var strategies = side == BattleSideEnum.Attacker
                ? DefaultSiegeStrategies.AllAttackerStrategies
                : DefaultSiegeStrategies.AllDefenderStrategies;
            float bestScore = float.MinValue;
            foreach (var item in strategies)
            {
                float score = Campaign.Current.Models.SiegeEventModel.GetSiegeStrategyScore(siegeEvent, side, item) * (0.5f + 0.5f * MBRandom.RandomFloat);
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
}
