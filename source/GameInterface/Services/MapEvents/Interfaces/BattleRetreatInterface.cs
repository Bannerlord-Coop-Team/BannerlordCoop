using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Interfaces;

/// <summary>
/// Applies a "Try to get away." retreat authoritatively, for a named party rather than "the" player.
/// </summary>
public interface IBattleRetreatInterface : IGameAbstraction
{
    /// <summary>
    /// [Server, game thread] Validates and applies the retreat. Returns false if the request is not
    /// legitimate, in which case nothing was mutated.
    /// </summary>
    bool TryApplyRetreat(MobileParty requester, MapEvent battle, out string[] campClearedPartyIds);

    /// <summary>
    /// [Server, game thread] Applies the troop loss for a party that broke into a besieged settlement.
    /// </summary>
    void ApplyBreakInCasualties(MobileParty requester, Settlement settlement);
}

internal class BattleRetreatInterface : IBattleRetreatInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleRetreatInterface>();

    public bool TryApplyRetreat(MobileParty requester, MapEvent battle, out string[] campClearedPartyIds)
    {
        campClearedPartyIds = System.Array.Empty<string>();

        if (requester == null || battle == null) return false;
        if (battle.HasWinner) return false;

        // Vanilla only offers "Try to get away." to the battle's defender-side leader, so anything else is
        // either a stale click or a forged request.
        if (requester.Party?.MapEventSide == null) return false;
        if (battle.DefenderSide?.LeaderParty != requester.Party) return false;

        var sacrificed = GetTroopsSacrificed(battle);
        var commanded = CollectCommandedParties(requester);

        ApplyTroopSacrifice(requester, commanded, sacrificed);
        RemoveGoods(requester);

        campClearedPartyIds = ClearBesiegerCamps(requester, commanded);

        // Leave the battle before anything reports success. Without this the server still holds the party
        // on the defender side, so it stays in the encounter and a second click runs the whole retreat
        // again - sacrificing troops and goods a second time for a party that already got away.
        // Everything above needs the battle intact (the loss model reads it, camps are cleared from it),
        // so the removal happens here rather than at the top.
        LeaveBattle(requester, commanded);

        requester.TeleportPartyToOutSideOfEncounterRadius();
        requester.IgnoreByOtherPartiesTill(CampaignTime.HoursFromNow(1f));

        // MobileParty.Position is not AutoSynced, so the teleport only exists on the server until the
        // behaviour snapshot carries it. IgnoreByOtherPartiesTill needs no replication - only server AI reads it.
        PublishPosition(requester);

        Logger.Debug("Applied retreat for {PartyId}: {Sacrificed} troops, {Cleared} camps cleared",
            requester.StringId, sacrificed, campClearedPartyIds.Length);

        return true;
    }

    /// <summary>
    /// Detaches the retreating force from its map event.
    /// </summary>
    /// <remarks>
    /// PartyBase's MapEventSide setter is the whole removal: assigning null calls RemovePartyInternal on
    /// the side it was on, and cascades to attached parties on its own. Commanded parties are cleared too,
    /// because they left with the requester - leaving them behind would keep the battle alive around
    /// parties that are no longer there.
    ///
    /// RemovePartyInternal uses RemoveAt, which bypasses the collection sync, so none of this replicates on
    /// its own. Without the explicit broadcast below the retreating force disappears on the server while every
    /// other player still fighting keeps it in their local map event - counted in their battle, and still on
    /// their side's roster. The ordinary leave path has the same problem and solves it the same way
    /// (BattleJoinLeaveHandler.RemovePartyFromBattleAndBroadcast), so this reuses that message rather than
    /// inventing a second removal protocol.
    /// </remarks>
    private static void LeaveBattle(MobileParty requester, List<MobileParty> commanded)
    {
        var left = new List<PartyBase>();

        // Read BEFORE anything detaches. Clearing the requester's MapEventSide cascades to its attached
        // parties AND clears the attachment list, so by the time the broadcast is built there is nothing
        // left to enumerate.
        var attachedPlayers = CollectAttachedPlayerParties(requester);

        foreach (var party in commanded)
        {
            if (Detach(party?.Party)) left.Add(party.Party);
        }

        if (Detach(requester.Party)) left.Add(requester.Party);

        // CollectCommandedParties deliberately skips player parties - one player's retreat must not spend
        // another player's troops or clear their camp. But the cascade removes them from the map event all
        // the same, so they still need telling; otherwise the one case this broadcast exists for is the one
        // case it misses, and that player sits in the battle UI with no party in the battle.
        foreach (var party in attachedPlayers)
        {
            if (!left.Contains(party)) left.Add(party);
        }

        BroadcastLeft(left, requester.Party);
    }

    /// <summary>
    /// The player-led parties attached to <paramref name="requester"/> that the detach cascade will remove.
    /// </summary>
    private static List<PartyBase> CollectAttachedPlayerParties(MobileParty requester)
    {
        var attachedPlayers = new List<PartyBase>();

        var army = requester.Army;
        if (army?.LeaderParty != requester) return attachedPlayers;

        var attached = requester.AttachedParties?.ToArray();
        if (attached == null) return attachedPlayers;

        foreach (var party in attached)
        {
            if (party == null || party == requester) continue;
            if (!party.IsPlayerParty()) continue;
            if (party.Party?.MapEventSide == null) continue;

            attachedPlayers.Add(party.Party);
        }

        return attachedPlayers;
    }

    private static bool Detach(PartyBase party)
    {
        if (party?.MapEventSide == null) return false;

        party.MapEventSide = null;
        return true;
    }

    /// <summary>
    /// Tells every client to drop the parties that just retreated from their copy of the battle.
    /// </summary>
    /// <remarks>
    /// FinishLocalMenus is per-party, and the requester is the exception rather than the rule. The retreating
    /// player's own teardown is driven by the NetworkBattleRetreatResolved that follows, so asking for menus to
    /// be finished here as well would close a menu that client has already moved past - hence false for it.
    ///
    /// Every OTHER party gets true. A commanded or attached party is dropped from the map event by the
    /// cascade, but nothing else tells its owner: NetworkBattleRetreatResolved only closes the REQUESTER's
    /// encounter, so another player pulled out by their army leader's retreat would lose their party from the
    /// battle while still sitting in the battle UI. ApplyNetworkLeave gates the teardown on
    /// <c>isMainParty</c>, so true here reaches exactly the one client that owns each party and nobody else.
    ///
    /// LeaveSiege stays false throughout: the retreat clears its own besieger camps and reports them through
    /// campClearedPartyIds, so re-running the siege leave would clear them a second time.
    /// </remarks>
    private static void BroadcastLeft(List<PartyBase> left, PartyBase requester)
    {
        if (left.Count == 0) return;
        if (!ContainerProvider.TryResolve<INetwork>(out var network)) return;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;

        foreach (var party in left)
        {
            if (!objectManager.TryGetId(party, out var partyId)) continue;

            network.SendAll(new NetworkPartyLeftBattle(
                partyId,
                leaveSiege: false,
                finishLocalMenus: party != requester));
        }
    }

    private static void PublishPosition(MobileParty party)
    {
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var snapshot)) return;
        if (!snapshot.TryCreate(party, out PartyBehaviorUpdateData data)) return;

        data.ForcePosition = true;
        data.ResetMovementToHold = true;
        MessageBroker.Instance.Publish(null, new PartyBehaviorUpdated(ref data));
    }

    /// <summary>
    /// Runs vanilla's own break-in loss against the requester, by making it the acting party for the call.
    /// </summary>
    /// <remarks>
    /// Vanilla's BreakInOutBesiegedSettlementAction mutates MobileParty.MainParty directly, which the
    /// headless host does not have, so the action itself cannot be reused here. The loss it applies is a
    /// simple proportion of the party's regular troops, so it is reproduced with the same shape - and
    /// crucially against the REQUESTING party, so one player breaking in never costs another player troops.
    /// </remarks>
    public void ApplyBreakInCasualties(MobileParty requester, Settlement settlement)
    {
        if (requester?.Party == null) return;

        var siegeEvent = settlement?.SiegeEvent;
        if (siegeEvent == null) return;

        var model = Campaign.Current?.Models?.TroopSacrificeModel;
        if (model == null) return;

        // Same model call vanilla's ApplyInternal makes, but for the requesting party rather than MainParty.
        var lost = model.GetLostTroopCountForBreakingInBesiegedSettlement(requester, siegeEvent)
            .RoundedResultNumber;
        if (lost <= 0) return;

        var total = requester.Party.NumberOfRegularMembers;
        if (total <= 0) return;

        var behavior = Campaign.Current?.GetCampaignBehavior<EncounterGameMenuBehavior>();
        if (behavior == null) return;

        // Vanilla removes `lost` troops one at a time with its own MBRandom draw; SacrificeTroopsWithRatio is
        // the same selection expressed as a proportion, and is the one helper that takes an explicit party.
        // The roll happens here once and reaches every client as ordinary roster deltas.
        behavior.SacrificeTroopsWithRatio(requester, MathF.Min(lost, total) / (float)total);

        Logger.Debug("Applied break-in losses for {PartyId} entering {SettlementId}: {Lost} of {Total}",
            requester.StringId, settlement.StringId, lost, total);
    }

    /// <summary>
    /// Vanilla's count, from the same model the client's menu used. Negative means "no cost".
    /// </summary>
    private static int GetTroopsSacrificed(MapEvent battle)
    {
        var model = Campaign.Current?.Models?.TroopSacrificeModel;
        if (model == null) return 0;

        var count = model.GetNumberOfTroopsSacrificedForTryingToGetAway(BattleSideEnum.Defender, battle);
        return count > 0 ? count : 0;
    }

    /// <summary>
    /// The parties this retreat may spend troops from: the requester, plus followers attached to it when it
    /// leads the army - but never another CONNECTED player's party. Vanilla's RemoveTroopsForTryToGetAway
    /// spreads the loss across the army leader's AttachedParties, which in co-op would let one player delete
    /// another player's troops.
    /// </summary>
    private static List<MobileParty> CollectCommandedParties(MobileParty requester)
    {
        var commanded = new List<MobileParty>();

        var army = requester.Army;
        if (army?.LeaderParty != requester) return commanded;

        var attached = requester.AttachedParties?.ToArray();
        if (attached == null) return commanded;

        foreach (var party in attached)
        {
            if (party == null || party == requester) continue;
            if (party.IsPlayerParty()) continue;
            commanded.Add(party);
        }

        return commanded;
    }

    private static void ApplyTroopSacrifice(MobileParty requester, List<MobileParty> commanded, int numberOfTroops)
    {
        if (numberOfTroops <= 0) return;

        var total = requester.Party.NumberOfRegularMembers;
        foreach (var party in commanded) total += party.Party.NumberOfRegularMembers;
        if (total <= 0) return;

        // Clamp the model's count to what actually exists, then apply vanilla's own per-party ratio.
        var ratio = MathF.Min(numberOfTroops, total) / (float)total;

        var behavior = Campaign.Current?.GetCampaignBehavior<EncounterGameMenuBehavior>();
        if (behavior == null) return;

        // SacrificeTroopsWithRatio is fully party-parameterised in vanilla (no MainParty in its body), so the
        // server can run vanilla's own selection - including its MBRandom draw - against the requester's party.
        // The roll happens here, once, and reaches every client as ordinary TroopRoster deltas.
        behavior.SacrificeTroopsWithRatio(requester, ratio);
        foreach (var party in commanded) behavior.SacrificeTroopsWithRatio(party, ratio);
    }

    /// <summary>
    /// Vanilla's CalculateAndRemoveItemsForTryToGetAway, against the requester instead of MainParty:
    /// 15% off every merchandise stack, banner items exempt. Deterministic, so no roll is involved.
    /// </summary>
    private static void RemoveGoods(MobileParty requester)
    {
        var roster = requester.Party?.ItemRoster;
        if (roster == null) return;

        foreach (var element in new ItemRoster(roster))
        {
            var item = element.EquipmentElement.Item;
            if (item == null || item.NotMerchandise || item.IsBannerItem) continue;

            var lost = (int)MathF.Floor(element.Amount * 0.15f);
            if (lost > 0) roster.AddToCounts(element.EquipmentElement, -lost);
        }
    }

    /// <summary>
    /// Clears the retreating party's siege camp, and those of the followers it commands. Vanilla clears the
    /// camp of EVERY party on the defender side, which in co-op would end another player's siege for them.
    /// </summary>
    private static string[] ClearBesiegerCamps(MobileParty requester, List<MobileParty> commanded)
    {
        var cleared = new List<string>();

        if (requester.BesiegerCamp != null)
        {
            requester.BesiegerCamp = null;
            cleared.Add(requester.StringId);
        }

        foreach (var party in commanded)
        {
            if (party.BesiegerCamp == null) continue;
            party.BesiegerCamp = null;
            cleared.Add(party.StringId);
        }

        return cleared.ToArray();
    }
}
