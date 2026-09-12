using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    private const string PendingAftermathSaveKey = "_coop_pending_siege_aftermaths";
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeAftermathPatches>();

    internal class PendingAftermath
    {
        public readonly MobileParty LeaderParty;
        public readonly Hero LeaderHero;
        public readonly Clan PreviousOwnerClan;
        public readonly Dictionary<MobileParty, float> Contributions;
        public readonly CampaignTime ParkedAt;
        public Clan CaptureOwnerClan { get; private set; }
        public Clan CapturerClan { get; private set; }

        public PendingAftermath(MobileParty leaderParty, Clan previousOwnerClan, Dictionary<MobileParty, float> contributions)
            : this(leaderParty, leaderParty?.LeaderHero, previousOwnerClan, contributions, CampaignTime.Now)
        {
        }

        internal PendingAftermath(MobileParty leaderParty, Hero leaderHero, Clan previousOwnerClan,
            Dictionary<MobileParty, float> contributions, CampaignTime parkedAt)
        {
            LeaderParty = leaderParty;
            LeaderHero = leaderHero;
            PreviousOwnerClan = previousOwnerClan;
            Contributions = contributions;
            ParkedAt = parkedAt;
        }

        internal bool IsCaptureBound => CaptureOwnerClan != null && CapturerClan != null;

        internal bool IsOriginalCaptureTransition(Settlement settlement, Hero newOwner, Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            return !IsCaptureBound
                && detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege
                && settlement?.OwnerClan == PreviousOwnerClan
                && newOwner?.Clan != null
                && capturerHero?.Clan != null
                && capturerHero == LeaderHero;
        }

        internal bool TryBindCapture(Hero newOwner, Hero capturerHero)
        {
            if (IsCaptureBound) return false;
            if (newOwner?.Clan == null || capturerHero?.Clan == null) return false;
            if (capturerHero != LeaderHero) return false;

            CaptureOwnerClan = newOwner.Clan;
            CapturerClan = capturerHero.Clan;
            return true;
        }

        internal bool TryRestoreCaptureBinding(Clan captureOwnerClan, Clan capturerClan)
        {
            if (IsCaptureBound || captureOwnerClan == null || capturerClan == null) return false;

            CaptureOwnerClan = captureOwnerClan;
            CapturerClan = capturerClan;
            return true;
        }

        internal bool MatchesCapture(Clan currentOwnerClan, Clan lastCapturedBy)
        {
            return IsCaptureBound
                && currentOwnerClan == CaptureOwnerClan
                && lastCapturedBy == CapturerClan;
        }

        internal bool MatchesCurrentCapture(Settlement settlement)
        {
            return settlement != null && MatchesCapture(settlement.OwnerClan, settlement.Town?.LastCapturedBy);
        }
    }

    // Keyed by settlement so two simultaneous player-led captures cannot clobber each other's
    // contribution snapshots (the vanilla behavior stores them in single-slot instance fields).
    internal static readonly ConcurrentDictionary<Settlement, PendingAftermath> PendingAftermaths = new ConcurrentDictionary<Settlement, PendingAftermath>();

    /// <summary>
    /// True when this map event captured a settlement for a co-op player. The same winner/settlement
    /// computation OnMapEventEndedPrefix uses; BattleFinalizeHandler calls it to keep the capturing
    /// player's encounter open (it needs the settlement-taken flow, not the plain close).
    /// </summary>
    internal static bool TryGetPlayerCaptureLeader(MapEvent mapEvent, out MobileParty leaderParty, out Settlement settlement)
    {
        leaderParty = null;
        settlement = null;
        if (mapEvent == null) return false;

        var battleSide = (!mapEvent.IsSallyOut && !mapEvent.IsBlockadeSallyOut) ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
        if ((!mapEvent.IsSiegeAssault && !mapEvent.IsSiegeOutside && !mapEvent.IsSallyOut && !mapEvent.IsBlockadeSallyOut)
            || mapEvent.WinningSide != battleSide || mapEvent.MapEventSettlement == null)
        {
            return false;
        }

        var lp = mapEvent.GetMapEventSide(battleSide).LeaderParty?.MobileParty;
        if (lp?.LeaderHero == null || !lp.LeaderHero.IsPlayerHero()) return false;

        leaderParty = lp;
        settlement = mapEvent.MapEventSettlement;
        return true;
    }

    [HarmonyPatch(typeof(SiegeAftermathCampaignBehavior), nameof(SiegeAftermathCampaignBehavior.OnSiegeAftermathApplied))]
    [HarmonyPrefix]
    private static bool OnSiegeAftermathAppliedPrefix() => ModInformation.IsServer;

    // Harmony statics outlive the Campaign. RegisterEvents runs before behavior SyncData restores this
    // campaign's saved entries, so clear process-lifetime leftovers before an hourly tick can dereference
    // Settlement/MobileParty instances from the prior campaign.
    [HarmonyPatch(typeof(SiegeAftermathCampaignBehavior), nameof(SiegeAftermathCampaignBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix()
    {
        PendingAftermaths.Clear();
    }

    // A pending entry is created by OnMapEventEnded before vanilla transfers the settlement. Bind it to
    // that exact owner/capturer transition. Any later transfer first resolves the old capture while the
    // old owner is still current, so the aftermath can neither be lost nor applied to the next owner.
    [HarmonyPatch(typeof(ChangeOwnerOfSettlementAction), "ApplyInternal")]
    [HarmonyPrefix]
    private static void ChangeOwnerOfSettlementPrefix(Settlement settlement, Hero newOwner, Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (ModInformation.IsClient || settlement == null) return;
        if (!PendingAftermaths.TryGetValue(settlement, out var pending)) return;

        if (pending.IsOriginalCaptureTransition(settlement, newOwner, capturerHero, detail))
        {
            pending.TryBindCapture(newOwner, capturerHero);
            return;
        }

        var behavior = Campaign.Current?.GetCampaignBehavior<SiegeAftermathCampaignBehavior>();
        if (behavior != null)
        {
            ResolvePending(behavior, settlement, "settlement ownership changed");
        }
        else
        {
            PendingAftermaths.TryRemove(settlement, out _);
            Logger.Error("Discarded pending siege aftermath for {Settlement}: behavior unavailable before owner transfer",
                settlement.Name?.ToString());
        }
    }

    // Vanilla skips the capture relation penalty when the new owner is Clan.PlayerClan, which is null
    // on the dedicated host, so every player-clan siege capture would wrongly eat the -10/-6 hit.
    // Reimplemented with the co-op player check; the whole vanilla body is just this penalty.
    [HarmonyPatch(typeof(SiegeAftermathCampaignBehavior), nameof(SiegeAftermathCampaignBehavior.OnSettlementOwnerChanged))]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix(Settlement settlement, Hero newOwner, Hero oldOwner, Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (ModInformation.IsClient) return false;

        ValidatePendingAfterOwnerChange(settlement, newOwner, capturerHero, detail);

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

        // OnMapEventEnded runs before vanilla changes ownership. Resolve an older capture now, against
        // its still-current owner, before this capture can replace the entry or an AI recapture can leave
        // it behind to fire later against the wrong owner.
        if (!PendingAftermaths.IsEmpty)
        {
            ResolvePending(__instance, settlement, "a newer settlement capture began");
        }

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
                if (ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
                {
                    siegeEventInterface.SetLocalAftermathNarrationContext(settlement);
                }
                // Don't guess _playerEncounterAftermath locally: DetermineAISiegeAftermath is a weighted
                // RNG draw that would diverge from the server's pick. The applied-aftermath broadcast
                // sets it (and re-renders the menu if it's already open).
            }

            return false;
        }

        if (leaderParty?.LeaderHero != null && leaderParty.LeaderHero.IsPlayerHero())
        {
            if (!PendingAftermaths.TryAdd(settlement,
                    new PendingAftermath(leaderParty, settlement.OwnerClan, contributions)))
            {
                Logger.Error("Could not park siege aftermath for {Settlement}: another capture is still pending",
                    settlement.Name?.ToString());
                return false;
            }
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
        foreach (var pair in PendingAftermaths.ToArray())
        {
            if (!pair.Value.MatchesCurrentCapture(pair.Key))
            {
                if (PendingAftermaths.TryRemove(pair.Key, out _))
                {
                    Logger.Warning("Discarded stale siege aftermath for {Settlement}: capture identity no longer matches",
                        pair.Key?.Name?.ToString());
                }
                continue;
            }

            if (pair.Value.ParkedAt.ElapsedHoursUntilNow < CampaignTime.HoursInDay) continue;
            ResolvePending(behavior, pair.Key, "the player choice timed out");
        }
    }

    // The vanilla behavior already owns the campaign-behavior save record, so append the co-op pending
    // generations to that same record. A save (including a join snapshot) must be observational: it may
    // preserve an unanswered player choice, but must never silently replace it with an AI choice.
    [HarmonyPatch(typeof(SiegeAftermathCampaignBehavior), nameof(SiegeAftermathCampaignBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        SyncPendingAftermaths(dataStore, ModInformation.IsClient);
    }

    [HarmonyPatch(typeof(SiegeAftermathAction), nameof(SiegeAftermathAction.ApplyAftermath))]
    [HarmonyPrefix]
    private static bool ApplyAftermathPrefix(MobileParty attackerParty, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermathType)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            // The player made the choice; stop holding the menu (SiegeCaptureMenuHoldPatch) so the
            // encounter can advance to the settlement normally.
            SiegeCaptureMenuHoldPatch.Release(settlement);
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

    private static void ValidatePendingAfterOwnerChange(Settlement settlement, Hero newOwner, Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (settlement == null || !PendingAftermaths.TryGetValue(settlement, out var pending)) return;

        // Fallback for another Harmony prefix ordering or a direct event invocation: bind the original
        // siege capture after the owner changed if the pre-change ApplyInternal prefix did not run first.
        if (!pending.IsCaptureBound
            && detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege
            && capturerHero == pending.LeaderHero)
        {
            pending.TryBindCapture(newOwner, capturerHero);
        }

        if (pending.MatchesCurrentCapture(settlement)) return;

        PendingAftermaths.TryRemove(settlement, out _);
        Logger.Warning("Invalidated pending siege aftermath for {Settlement}: settlement ownership no longer matches the capture",
            settlement.Name?.ToString());
    }

    internal static void SyncPendingAftermaths(IDataStore dataStore, bool isClient)
    {
        List<PendingAftermathSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            saveData = isClient
                ? new List<PendingAftermathSaveData>()
                : PendingAftermaths.Select(pair => new PendingAftermathSaveData(
                    pair.Key,
                    pair.Value.LeaderParty,
                    pair.Value.LeaderHero,
                    pair.Value.PreviousOwnerClan,
                    pair.Value.Contributions,
                    pair.Value.ParkedAt,
                    pair.Value.CaptureOwnerClan,
                    pair.Value.CapturerClan)).ToList();
        }

        dataStore.SyncData(PendingAftermathSaveKey, ref saveData);
        if (!dataStore.IsLoading) return;

        PendingAftermaths.Clear();
        // A transferred server save contains this record on every client. Read it so the behavior
        // store stays schema-compatible, but keep the authoritative pending state server-only.
        if (isClient || saveData == null) return;

        foreach (var entry in saveData)
        {
            if (entry?.Settlement == null || entry.LeaderParty == null || entry.LeaderHero == null
                || entry.PreviousOwnerClan == null || entry.Contributions == null)
            {
                Logger.Warning("Discarded incomplete saved siege aftermath entry");
                continue;
            }

            var pending = new PendingAftermath(
                entry.LeaderParty,
                entry.LeaderHero,
                entry.PreviousOwnerClan,
                new Dictionary<MobileParty, float>(entry.Contributions),
                entry.ParkedAt);
            if (!pending.TryRestoreCaptureBinding(entry.CaptureOwnerClan, entry.CapturerClan)
                || !pending.MatchesCurrentCapture(entry.Settlement))
            {
                Logger.Warning("Discarded stale saved siege aftermath for {Settlement}",
                    entry.Settlement.Name?.ToString());
                continue;
            }

            if (!PendingAftermaths.TryAdd(entry.Settlement, pending))
            {
                Logger.Warning("Discarded duplicate saved siege aftermath for {Settlement}",
                    entry.Settlement.Name?.ToString());
            }
        }
    }

    internal static bool ResolvePending(SiegeAftermathCampaignBehavior behavior, Settlement settlement, string reason,
        Func<SiegeAftermathCampaignBehavior, MobileParty, Settlement, SiegeAftermathAction.SiegeAftermath> determineAftermath = null,
        Action<MobileParty, Settlement, SiegeAftermathAction.SiegeAftermath, Clan,
            Dictionary<MobileParty, float>> applyAftermath = null)
    {
        if (behavior == null || settlement == null) return false;
        if (!PendingAftermaths.TryRemove(settlement, out var pending)) return false;

        if (!pending.MatchesCurrentCapture(settlement))
        {
            Logger.Warning("Discarded pending siege aftermath for {Settlement} ({Reason}): capture identity no longer matches",
                settlement.Name?.ToString(), reason);
            return false;
        }

        determineAftermath ??= static (aftermathBehavior, leaderParty, capturedSettlement) =>
            aftermathBehavior.DetermineAISiegeAftermath(leaderParty, capturedSettlement);
        applyAftermath ??= static (leaderParty, capturedSettlement, aftermath, previousOwner, contributions) =>
            SiegeAftermathAction.ApplyAftermath(leaderParty, capturedSettlement, aftermath, previousOwner, contributions);

        var aftermath = determineAftermath(behavior, pending.LeaderParty, settlement);
        applyAftermath(pending.LeaderParty, settlement, aftermath, pending.PreviousOwnerClan, pending.Contributions);
        Logger.Information("Resolved pending siege aftermath for {Settlement} because {Reason}",
            settlement.Name?.ToString(), reason);
        return true;
    }
}
