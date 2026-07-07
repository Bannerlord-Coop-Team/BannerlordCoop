using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Server-authoritative siege aftermath. An AI-led capture applies the AI pick on the server as
/// vanilla does; a player-led capture is parked here until that player's Devastate/Pillage/Mercy
/// choice arrives, so the RNG effects run exactly once, on the server.
/// </summary>
[HarmonyPatch]
internal class SiegeAftermathPatches
{
    internal class PendingAftermath
    {
        public readonly MobileParty LeaderParty;
        public readonly Clan PreviousOwnerClan;
        public readonly Dictionary<MobileParty, float> Contributions;
        public readonly CampaignTime ParkedAt;

        public PendingAftermath(MobileParty leaderParty, Clan previousOwnerClan, Dictionary<MobileParty, float> contributions)
        {
            LeaderParty = leaderParty;
            PreviousOwnerClan = previousOwnerClan;
            Contributions = contributions;
            ParkedAt = CampaignTime.Now;
        }
    }

    // Keyed by settlement so two simultaneous player-led captures cannot clobber each other's
    // contribution snapshots (the vanilla behavior stores them in single-slot instance fields).
    internal static readonly ConcurrentDictionary<Settlement, PendingAftermath> PendingAftermaths = new ConcurrentDictionary<Settlement, PendingAftermath>();

    [HarmonyPatch(typeof(SiegeAftermathCampaignBehavior), nameof(SiegeAftermathCampaignBehavior.OnSiegeAftermathApplied))]
    [HarmonyPrefix]
    private static bool OnSiegeAftermathAppliedPrefix() => ModInformation.IsServer;

    // Harmony statics outlive the Campaign; RegisterEvents runs once per campaign start/load, so
    // clearing here drops any entry left by a previous campaign before the hourly eviction could
    // deref its disposed Settlement/MobileParty.
    [HarmonyPatch(typeof(SiegeAftermathCampaignBehavior), nameof(SiegeAftermathCampaignBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix()
    {
        PendingAftermaths.Clear();
    }

    // Vanilla skips the capture relation penalty when the new owner is Clan.PlayerClan, which is null
    // on the dedicated host, so every player-clan siege capture would wrongly eat the -10/-6 hit.
    // Reimplemented with the co-op player check; the whole vanilla body is just this penalty.
    [HarmonyPatch(typeof(SiegeAftermathCampaignBehavior), nameof(SiegeAftermathCampaignBehavior.OnSettlementOwnerChanged))]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix(Settlement settlement, Hero oldOwner, Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (ModInformation.IsClient) return false;

        if (settlement.IsFortification && detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege
            && capturerHero != null && settlement.OwnerClan != null
            && !settlement.OwnerClan.Leader.IsPlayerHero() && !oldOwner.IsDead)
        {
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(oldOwner, capturerHero, -10);
            if (capturerHero.MapFaction.Leader != capturerHero && settlement.OwnerClan.Leader != capturerHero.MapFaction.Leader)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(oldOwner, capturerHero.MapFaction.Leader, -6);
            }
        }

        return false;
    }

    [HarmonyPatch(typeof(SiegeAftermathCampaignBehavior), nameof(SiegeAftermathCampaignBehavior.OnMapEventEnded))]
    [HarmonyPrefix]
    private static bool OnMapEventEndedPrefix(SiegeAftermathCampaignBehavior __instance, MapEvent mapEvent)
    {
        var battleSide = (!mapEvent.IsSallyOut && !mapEvent.IsBlockadeSallyOut) ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
        if ((!mapEvent.IsSiegeAssault && !mapEvent.IsSiegeOutside && !mapEvent.IsSallyOut && !mapEvent.IsBlockadeSallyOut)
            || mapEvent.WinningSide != battleSide || mapEvent.MapEventSettlement == null)
        {
            return false;
        }

        var settlement = mapEvent.MapEventSettlement;
        var winningSide = mapEvent.GetMapEventSide(battleSide);
        var leaderParty = winningSide.LeaderParty?.MobileParty;

        var contributions = new Dictionary<MobileParty, float>();
        foreach (var item in __instance.GetLootPercentagesOfPartiesOnSideForSiegeAftermath(mapEvent, battleSide))
        {
            if (item.Item1.IsMobile && !contributions.ContainsKey(item.Item1.MobileParty))
            {
                contributions.Add(item.Item1.MobileParty, item.Item2);
            }
        }

        if (ModInformation.IsClient)
        {
            // Populate the fields the settlement-taken menus read on this machine; the apply itself
            // is server-owned and the choice consequence routes through the action patch below. The
            // narration field (_playerEncounterAftermath) arrives via the applied-aftermath broadcast.
            if (winningSide.IsMainPartyAmongParties())
            {
                __instance._siegeEventPartyContributions.Clear();
                foreach (var pair in contributions)
                {
                    __instance._siegeEventPartyContributions.Add(pair.Key, pair.Value);
                }

                __instance._playerEncounterAftermathDamagedBuildings.Clear();
                __instance._besiegerParty = leaderParty;
                __instance._prevSettlementOwnerClan = settlement.OwnerClan;
                // Vanilla's army-member test: in the besieger's own army, or in an army the player
                // does not lead.
                __instance._wasPlayerArmyMember = leaderParty != MobileParty.MainParty
                    && ((leaderParty?.Army != null && leaderParty.Army.Parties.Contains(MobileParty.MainParty))
                        || (MobileParty.MainParty.Army != null && MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty));
                // Don't guess _playerEncounterAftermath locally: DetermineAISiegeAftermath is a weighted
                // RNG draw that would diverge from the server's pick. The applied-aftermath broadcast
                // sets it (and re-renders the menu if it's already open).
            }

            return false;
        }

        if (leaderParty?.LeaderHero != null && leaderParty.LeaderHero.IsPlayerHero())
        {
            PendingAftermaths[settlement] = new PendingAftermath(leaderParty, settlement.OwnerClan, contributions);
            // Make sure the leading player actually gets the choice menu: their local encounter flow
            // usually opens it, but if it doesn't (encounter torn down first), the server would wait
            // on this pending entry forever.
            MessageBroker.Instance.Publish(null, new SiegeAftermathChoicePrompted(leaderParty, settlement));
            return false;
        }

        var aftermath = __instance.DetermineAISiegeAftermath(leaderParty, settlement);
        SiegeAftermathAction.ApplyAftermath(leaderParty, settlement, aftermath, settlement.OwnerClan, contributions);
        return false;
    }

    // A parked aftermath whose leader never answers (the player disconnected at the settlement-taken
    // menu) would otherwise strand its effects forever; fall back to the AI pick after a day.
    internal static void EvictStalePending(SiegeAftermathCampaignBehavior behavior)
    {
        foreach (var pair in PendingAftermaths)
        {
            if (pair.Value.ParkedAt.ElapsedHoursUntilNow < CampaignTime.HoursInDay) continue;
            if (!PendingAftermaths.TryRemove(pair.Key, out var stale)) continue;

            var aftermath = behavior.DetermineAISiegeAftermath(stale.LeaderParty, pair.Key);
            SiegeAftermathAction.ApplyAftermath(stale.LeaderParty, pair.Key, aftermath, stale.PreviousOwnerClan, stale.Contributions);
        }
    }

    [HarmonyPatch(typeof(SiegeAftermathAction), nameof(SiegeAftermathAction.ApplyAftermath))]
    [HarmonyPrefix]
    private static bool ApplyAftermathPrefix(MobileParty attackerParty, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermathType)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            MessageBroker.Instance.Publish(null, new SiegeAftermathAttempted(attackerParty, settlement, (int)aftermathType));
            return false;
        }

        return true;
    }

    // A parked aftermath is normally resolved by the leader's request; this backstop revisits stale
    // entries once per campaign hour so a disconnected leader cannot strand the effects.
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void HourlyTickPostfix()
    {
        if (ModInformation.IsClient) return;
        if (PendingAftermaths.IsEmpty) return;

        var behavior = Campaign.Current?.GetCampaignBehavior<SiegeAftermathCampaignBehavior>();
        if (behavior == null) return;

        EvictStalePending(behavior);
    }

    // Broadcast the pick the server actually applied so client menus narrate the right choice.
    [HarmonyPatch(typeof(SiegeAftermathAction), nameof(SiegeAftermathAction.ApplyAftermath))]
    [HarmonyPostfix]
    private static void ApplyAftermathPostfix(MobileParty attackerParty, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermathType)
    {
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(null, new SiegeAftermathApplied(settlement, (int)aftermathType));
    }

    // Vanilla's player relation effect is gated on MobileParty.MainParty, which is null on the
    // dedicated host, so a player-led capture would silently lose the relation changes with the lords
    // who fought alongside. The trait XP branch stays skipped: TraitLevelingHelper works on the local
    // main hero only.
    [HarmonyPatch(typeof(SiegeAftermathCampaignBehavior), nameof(SiegeAftermathCampaignBehavior.OnSiegeAftermathApplied))]
    [HarmonyPostfix]
    private static void OnSiegeAftermathAppliedPostfix(SiegeAftermathCampaignBehavior __instance,
        MobileParty attackerParty, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermathType,
        Dictionary<MobileParty, float> partyContributions)
    {
        if (ModInformation.IsClient) return;
        if (attackerParty?.LeaderHero == null || !attackerParty.LeaderHero.IsPlayerHero()) return;
        if (aftermathType == SiegeAftermathAction.SiegeAftermath.Pillage) return;
        if (attackerParty.MapFaction.Culture == settlement.Culture) return;

        foreach (var contribution in partyContributions)
        {
            var party = contribution.Key;
            if (party == attackerParty || party.LeaderHero == null) continue;

            int relationChange = __instance.GetSiegeAftermathRelationChangeWithLord(party.LeaderHero, aftermathType);
            if (relationChange != 0)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(attackerParty.LeaderHero, party.LeaderHero, relationChange);
            }
        }
    }
}
