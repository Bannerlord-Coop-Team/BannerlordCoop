#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Villages.Commands;
using GameInterface.Services.MapEvents.Messages.Leave;
using LiteNetLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Interfaces;
using Newtonsoft.Json;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.SiegeEvents.Commands;

public interface IDefenderSiegeContextFixture
{
    CoopCommandResult Capture();
    CoopCommandResult Start();
    CoopCommandResult EndMissions();
    CoopCommandResult Restore();
    CoopCommandResult Verify();
}

internal sealed class DefenderSiegeContextFixture : IDefenderSiegeContextFixture
{
    private const string SettlementId = "castle_ES1";
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IMobilePartyBehaviorSnapshot behavior;
    private readonly IDefenderFixtureBehaviorIdentity behaviorIdentity;
    private DefenderFixtureBehaviorReferences originalBehaviorReferences;
    private readonly ISiegeEventInterface siege;
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly Dictionary<string, Player> defenderPlayers = new Dictionary<string, Player>();
    private readonly Dictionary<string, MobileParty> defenderParties = new Dictionary<string, MobileParty>();
    private readonly Dictionary<string, NetPeer> defenderPeers = new Dictionary<string, NetPeer>();
    private readonly HashSet<PartyBase> expectedBattleParties = new HashSet<PartyBase>();
    private Hero relationFirst;
    private Hero relationSecond;
    private int originalRelation;
    private int stagedRelation;
    private bool relationRestored;
    private bool missionExitRequested;
    private Campaign campaign;
    private Settlement settlement;
    private MobileParty besieger;
    private bool createdBesieger;
    private bool createdBesiegerCleanupStarted;
    private Hero createdBesiegerHero;
    private Clan createdBesiegerClan;
    private string partyId;
    private CampaignVec2 position;
    private Vec2 bearing;
    private PartyBehaviorUpdateData originalBehavior;
    private SiegeEvent createdSiege;
    private bool startAttempted;
    private bool restored;

    public DefenderSiegeContextFixture(IObjectManager objects, IPlayerManager players,
        IMobilePartyBehaviorSnapshot behavior, ISiegeEventInterface siege, IMessageBroker broker, INetwork network,
        IDefenderFixtureBehaviorIdentity behaviorIdentity)
    {
        this.objects = objects;
        this.players = players;
        this.behavior = behavior;
        this.behaviorIdentity = behaviorIdentity;
        this.siege = siege;
        this.broker = broker;
        this.network = network;
    }

    public CoopCommandResult Capture()
    {
        if (ModInformation.IsClient) return Result(false, "server_required");
        if (createdBesieger && campaign == null)
        {
            if (!TryRemoveCreatedBesieger()) return Result(false, "created_besieger_cleanup_failed");
            ReleaseCreatedBesiegerOwnership();
        }
        if (campaign != null) return Result(false, "fixture_already_captured");
        if (Campaign.Current == null || !objects.TryGetObject<Settlement>(SettlementId, out var target) ||
            !target.IsCastle || target.SiegeEvent != null || target.Party.MapEvent != null)
            return Result(false, "castle_not_clean");
        var defenders = players.Players.Where(players.IsConnected).ToArray();
        if (defenders.Length != 2 ||
            !defenders.Any(player => player.ControllerId == "testclient") ||
            !defenders.Any(player => player.ControllerId == "testclient2"))
            return Result(false, "two_expected_defenders_required");
        var defenderParties = defenders.Select(player =>
            objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ? party : null).ToArray();
        if (defenderParties.Any(party => party == null || party.CurrentSettlement != target ||
                party.MapEvent != null || party.MapFaction == null))
            return Result(false, "inside_defenders_required");
        var peers = new Dictionary<string, NetPeer>();
        for (int index = 0; index < defenders.Length; index++)
        {
            if (!players.TryGetPeer(defenders[index].ControllerId, out var peer) || peer == null)
                return Result(false, "defender_peer_unavailable");
            peers[defenders[index].ControllerId] = peer;
        }
        var candidate = FindRestorableBesieger(target, defenderParties);
        Hero fixtureHero = null;
        Clan fixtureClan = null;
        bool fixtureCleanupFailed = false;
        bool usesCreatedBesieger = candidate == null;
        if (usesCreatedBesieger)
        {
            if (!TryCreateFixtureBesieger(
                    target,
                    defenderParties,
                    out candidate,
                    out fixtureHero,
                    out fixtureClan,
                    out fixtureCleanupFailed))
            {
                if (fixtureCleanupFailed)
                {
                    createdBesieger = true;
                    createdBesiegerCleanupStarted = true;
                    createdBesiegerHero = fixtureHero;
                    createdBesiegerClan = fixtureClan;
                    besieger = candidate;
                }
                return Result(false, fixtureCleanupFailed
                    ? "created_besieger_cleanup_failed"
                    : "no_restorable_hostile_besieger");
            }
        }
        if (usesCreatedBesieger)
        {
            createdBesieger = true;
            createdBesiegerCleanupStarted = false;
            createdBesiegerHero = fixtureHero;
            createdBesiegerClan = fixtureClan;
            besieger = candidate;
        }
        try
        {
            if (!objects.TryGetId(candidate, out string candidateId) ||
                !behavior.TryCreate(candidate, out var snapshot))
                return CaptureFailure("no_restorable_hostile_besieger");
            var references = behaviorIdentity.Capture(candidate, snapshot);
            if (!behaviorIdentity.IsCurrent(references))
                return CaptureFailure("besieger_behavior_identity_unavailable");
            Hero capturedRelationFirst = null;
            Hero capturedRelationSecond = null;
            int capturedOriginalRelation = 0;
            if (target.OwnerClan != null && target.OwnerClan != Clan.PlayerClan)
            {
                Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(
                    target.OwnerClan.Leader, candidate.LeaderHero, out capturedRelationFirst, out capturedRelationSecond);
                if (capturedRelationFirst == null || capturedRelationSecond == null)
                    return CaptureFailure("relation_pair_unavailable");
                capturedOriginalRelation = CharacterRelationManager.GetHeroRelation(capturedRelationFirst, capturedRelationSecond);
            }
            for (int index = 0; index < defenders.Length; index++)
            {
                defenderPlayers[defenders[index].ControllerId] = defenders[index];
                this.defenderParties[defenders[index].ControllerId] = defenderParties[index];
                defenderPeers[defenders[index].ControllerId] = peers[defenders[index].ControllerId];
            }
            expectedBattleParties.Add(target.Party);
            expectedBattleParties.Add(candidate.Party);
            if (target.Town?.GarrisonParty != null) expectedBattleParties.Add(target.Town.GarrisonParty.Party);
            if (target.MilitiaPartyComponent?.MobileParty != null)
                expectedBattleParties.Add(target.MilitiaPartyComponent.MobileParty.Party);
            foreach (var party in defenderParties) expectedBattleParties.Add(party.Party);
            besieger = candidate;
            partyId = candidateId;
            position = candidate.Position;
            bearing = candidate.Bearing;
            originalBehavior = snapshot;
            originalBehaviorReferences = references;
            relationFirst = capturedRelationFirst;
            relationSecond = capturedRelationSecond;
            originalRelation = capturedOriginalRelation;
            stagedRelation = capturedOriginalRelation;
            campaign = Campaign.Current;
            settlement = target;
            return Result(true, "captured");
        }
        catch
        {
            if (!createdBesieger) throw;
            return CaptureFailure("created_besieger_capture_exception");
        }
    }

    public CoopCommandResult Start()
    {
        if (ModInformation.IsClient || !IdentityCurrent() || !behaviorIdentity.IsCurrent(originalBehaviorReferences) ||
            !DefendersCurrent(requireInside: true) || !HasOnlyCapturedAssaultDefenders(settlement.Parties) ||
            startAttempted || restored ||
            settlement.SiegeEvent != null || settlement.Party.MapEvent != null ||
            besieger.MapEvent != null || besieger.BesiegerCamp != null || !besieger.IsActive ||
            besieger.CurrentSettlement != null || besieger.IsCurrentlyAtSea || !besieger.Position.IsOnLand ||
            besieger.IsTransitionInProgress || besieger.Army != null || besieger.AttachedTo != null ||
            besieger.AttachedParties.Count != 0 ||
            besieger.MapFaction?.IsAtWarWith(settlement.MapFaction) != true)
            return Result(false, "start_precondition_changed");
        startAttempted = true;
        // Keep the capture even if native start throws after moving or creating the camp.
        try
        {
            besieger.Position = settlement.GatePosition;
            besieger.SetMoveBesiegeSettlement(settlement, MobileParty.NavigationType.Default);
            siege.StartSiegeEvent(besieger, settlement);
        }
        finally
        {
            if (relationFirst != null)
                stagedRelation = CharacterRelationManager.GetHeroRelation(relationFirst, relationSecond);
            var candidate = settlement.SiegeEvent;
            if (candidate?.BesiegerCamp?.LeaderParty == besieger) createdSiege = candidate;
        }
        return Result(createdSiege != null, createdSiege != null ? "started" : "start_incomplete");
    }

    public CoopCommandResult EndMissions()
    {
        if (ModInformation.IsClient || !IdentityCurrent() ||
            missionExitRequested || !TryGetOwnedAssault(out var assault) ||
            !objects.TryGetId(assault, out string mapEventId))
            return Result(false, "mission_exit_precondition_changed");
        var connectedPeers = new List<NetPeer>();
        foreach (var player in defenderPlayers.Values)
        {
            if (!players.IsConnected(player)) continue;
            if (!players.TryGetPeer(player.ControllerId, out var peer) || peer == null ||
                !players.TryGetPlayer(peer, out var boundPlayer) || !ReferenceEquals(boundPlayer, player))
                return Result(false, "mission_exit_peer_changed");
            connectedPeers.Add(peer);
        }
        foreach (var peer in connectedPeers)
            network.Send(peer, new NetworkEndLateJoinModeFixtureMission(mapEventId));
        missionExitRequested = true;
        return Result(true, "mission_exit_requested");
    }

    public CoopCommandResult Restore()
    {
        if (ModInformation.IsClient) return Result(false, "server_required");
        if (createdBesieger && campaign == null)
        {
            if (!TryRemoveCreatedBesieger()) return Result(false, "created_besieger_cleanup_failed");
            ReleaseCreatedBesiegerOwnership();
            return Result(true, "created_besieger_cleaned_without_capture");
        }
        if (!ContextIdentityCurrent() || !behaviorIdentity.IsCurrent(originalBehaviorReferences))
            return Result(false, "restore_identity_changed");
        if (restored) return Verify();
        if (createdBesieger && createdBesiegerCleanupStarted)
        {
            if (settlement.SiegeEvent != null || settlement.Party.MapEvent != null ||
                besieger?.MapEvent != null || besieger?.BesiegerCamp != null)
                return Result(false, "siege_context_not_released");
            if (!TryRemoveCreatedBesieger()) return Result(false, "created_besieger_cleanup_failed");
            restored = IsCreatedBesiegerRestored(
                besieger?.IsActive == true,
                createdBesiegerHero?.CompanionOf != null,
                createdBesiegerHero?.DeathMark ?? KillCharacterAction.KillCharacterActionDetail.None) && relationRestored;
            return Result(restored, restored ? "restored" : "behavior_restore_mismatch");
        }
        if (!IdentityCurrent()) return Result(false, "restore_identity_changed");
        if (!startAttempted)
        {
            if (relationFirst != null)
            {
                CharacterRelationManager.SetHeroRelation(relationFirst, relationSecond, originalRelation);
                stagedRelation = originalRelation;
            }
            relationRestored = relationFirst == null ||
                CharacterRelationManager.GetHeroRelation(relationFirst, relationSecond) == originalRelation;
            if (!relationRestored) return Result(false, "relation_restore_mismatch");
            if (!createdBesieger)
            {
                restored = true;
                return Result(true, "restored_without_mutation");
            }
            createdBesiegerCleanupStarted = true;
            if (!TryRemoveCreatedBesieger()) return Result(false, "created_besieger_cleanup_failed");
            restored = IsCreatedBesiegerRestored(
                besieger?.IsActive == true,
                createdBesiegerHero?.CompanionOf != null,
                createdBesiegerHero?.DeathMark ?? KillCharacterAction.KillCharacterActionDetail.None) && relationRestored;
            return Result(restored, restored ? "created_besieger_removed_without_siege" : "behavior_restore_mismatch");
        }
        if (relationFirst != null && CharacterRelationManager.GetHeroRelation(relationFirst, relationSecond) != stagedRelation)
            return Result(false, "relation_changed_after_staging");
        if (settlement.SiegeEvent != null && !ReferenceEquals(settlement.SiegeEvent, createdSiege))
            return Result(false, "another_siege_is_active");
        if (createdSiege != null && settlement.SiegeEvent != null &&
            createdSiege.BesiegerCamp._besiegerParties.Any(party => party != besieger))
            return Result(false, "uncaptured_besieger_joined");
        var assault = settlement.Party.MapEvent ?? besieger.MapEvent;
        if (assault != null)
        {
            if (!missionExitRequested || !TryGetOwnedAssault(out var ownedAssault) ||
                !ReferenceEquals(assault, ownedAssault) || !objects.TryGetId(assault, out string mapEventId))
                return Result(false, "uncaptured_or_resolved_battle_is_active");
            broker.Publish(this, new NetworkMapEventFinalizeAttempted(mapEventId));
        }
        if (!IdentityCurrent() || !behaviorIdentity.IsCurrent(originalBehaviorReferences))
            return Result(false, "restore_identity_changed");
        if (settlement.SiegeEvent != null) siege.BreakSiege(besieger);
        if (settlement.SiegeEvent != null || settlement.Party.MapEvent != null ||
            besieger.MapEvent != null || besieger.BesiegerCamp != null || !besieger.IsActive)
            return Result(false, "siege_context_not_released");
        if (!IdentityCurrent() || !behaviorIdentity.IsCurrent(originalBehaviorReferences))
            return Result(false, "restore_identity_changed");
        if (!createdBesieger)
        {
            if (!behavior.TryApply(besieger, originalBehavior, out _))
                return Result(false, "original_behavior_unavailable");
            if (!IdentityCurrent() || !behaviorIdentity.IsCurrent(originalBehaviorReferences))
                return Result(false, "restore_identity_changed");
            besieger.Bearing = bearing;
            besieger.Position = position;
            broker.Publish(this, new PartyBehaviorChangeAttempted(besieger, forcePosition: true,
                isCurrentlyAtSea: besieger.IsCurrentlyAtSea));
        }
        if (relationFirst != null)
        {
            CharacterRelationManager.SetHeroRelation(relationFirst, relationSecond, originalRelation);
            stagedRelation = originalRelation;
        }
        relationRestored = relationFirst == null ||
            CharacterRelationManager.GetHeroRelation(relationFirst, relationSecond) == originalRelation;
        if (!relationRestored) return Result(false, "relation_restore_mismatch");
        if (createdBesieger)
        {
            createdBesiegerCleanupStarted = true;
            if (!TryRemoveCreatedBesieger()) return Result(false, "created_besieger_cleanup_failed");
        }
        restored = createdBesieger
            ? IsCreatedBesiegerRestored(
                besieger?.IsActive == true,
                createdBesiegerHero?.CompanionOf != null,
                createdBesiegerHero?.DeathMark ?? KillCharacterAction.KillCharacterActionDetail.None) &&
                relationRestored
            : behavior.TryCreate(besieger, out var applied) && SameBehavior(originalBehavior, applied) &&
                relationRestored;
        return Result(restored, restored ? "restored" : "behavior_restore_mismatch");
    }

    public CoopCommandResult Verify()
    {
        bool createdBesiegerRestored = createdBesieger && IsCreatedBesiegerRestored(
            besieger?.IsActive == true,
            createdBesiegerHero?.CompanionOf != null,
            createdBesiegerHero?.DeathMark ?? KillCharacterAction.KillCharacterActionDetail.None);
        bool ordinaryBesiegerRestored = !createdBesieger && IdentityCurrent() &&
            besieger.MapEvent == null && besieger.BesiegerCamp == null && besieger.IsActive;
        bool success = ModInformation.IsServer && ContextIdentityCurrent() &&
            behaviorIdentity.IsCurrent(originalBehaviorReferences) && restored &&
            settlement.SiegeEvent == null && settlement.Party.MapEvent == null &&
            (createdBesiegerRestored || ordinaryBesiegerRestored) &&
            (createdBesieger ? relationRestored :
                relationFirst == null || CharacterRelationManager.GetHeroRelation(relationFirst, relationSecond) == originalRelation);
        return Result(success, success ? "restore_verified" : "restore_not_verified");
    }

    private bool DefenderIdentitiesCurrent()
    {
        if (defenderPlayers.Count != 2 || defenderParties.Count != 2) return false;
        foreach (var pair in defenderPlayers)
        {
            if (!players.TryGetPlayer(pair.Key, out var player) || !ReferenceEquals(player, pair.Value) ||
                !objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ||
                !defenderParties.TryGetValue(pair.Key, out var capturedParty) || !ReferenceEquals(party, capturedParty))
                return false;
        }
        return true;
    }

    private bool DefendersCurrent(bool requireInside)
    {
        if (!DefenderIdentitiesCurrent()) return false;
        if (players.Players.Count(players.IsConnected) != defenderParties.Count) return false;
        foreach (var pair in defenderParties)
        {
            if (!players.TryGetPlayer(pair.Key, out var player) || !players.IsConnected(player) ||
                !players.TryGetPeer(pair.Key, out var peer) || !ReferenceEquals(peer, defenderPeers[pair.Key]) ||
                !objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party) || party != pair.Value ||
                (requireInside && (party.CurrentSettlement != settlement || party.MapEvent != null))) return false;
        }
        return defenderParties.Count == 2;
    }

    // Match vanilla siege defenders before the siege needed by Town.GetDefenderParties exists.
    internal bool HasOnlyCapturedAssaultDefenders(IEnumerable<MobileParty> parties) =>
        parties.All(party => expectedBattleParties.Contains(party.Party) || !party.IsActive ||
            party.IsVillager || party.IsCaravan || (party.IsMilitia && settlement.Town.InRebelliousState) ||
            party.MapFaction?.IsAtWarWith(besieger.MapFaction) != true);

    private bool TryGetOwnedAssault(out MapEvent assault)
    {
        assault = settlement.Party.MapEvent;
        if (createdSiege == null || assault == null || !assault.IsSiegeAssault ||
            assault.IsFinalized || assault.BattleState != BattleState.None || assault.MapEventSettlement != settlement ||
            besieger.MapEvent != assault || !DefenderIdentitiesCurrent()) return false;
        foreach (var party in defenderParties.Values)
            if (party.MapEvent != assault || party.MapEventSide != assault.DefenderSide) return false;
        return assault.InvolvedParties.All(expectedBattleParties.Contains);
    }

    private bool ContextIdentityCurrent() => campaign != null && campaign == Campaign.Current && DefenderIdentitiesCurrent() &&
        objects.TryGetObject<Settlement>(SettlementId, out var currentSettlement) && currentSettlement == settlement;

    private bool IdentityCurrent() => ContextIdentityCurrent() &&
        objects.TryGetObject<MobileParty>(partyId, out var currentParty) && currentParty == besieger;

    private static MobileParty FindRestorableBesieger(Settlement target, IEnumerable<MobileParty> defenders) =>
        MobileParty.AllLordParties.Where(party => IsRestorableBesieger(party, target, defenders))
            .OrderByDescending(party => party.Party.CalculateCurrentStrength()).FirstOrDefault();

    private static bool IsRestorableBesieger(MobileParty party, Settlement target, IEnumerable<MobileParty> defenders) =>
        party?.IsActive == true && !party.IsPlayerParty() && party.LeaderHero != null &&
        party.CurrentSettlement == null && party.MapEvent == null && party.BesiegerCamp == null &&
        party.Army == null && party.AttachedTo == null && party.AttachedParties.Count == 0 &&
        party.Position.IsOnLand && !party.IsCurrentlyAtSea && !party.IsTransitionInProgress &&
        party.MapFaction?.IsAtWarWith(target.MapFaction) == true &&
        defenders.All(defender => party.MapFaction.IsAtWarWith(defender.MapFaction));

    private static bool TryCreateFixtureBesieger(
        Settlement target,
        IEnumerable<MobileParty> defenders,
        out MobileParty candidate,
        out Hero fixtureHero,
        out Clan fixtureClan,
        out bool cleanupFailed)
    {
        candidate = null;
        fixtureHero = null;
        cleanupFailed = false;
        fixtureClan = Clan.All
            .Where(clan => clan != null && clan != Clan.PlayerClan && clan.Leader?.CharacterObject != null &&
                clan.DefaultPartyTemplate != null && clan.MapFaction != null && target.MapFaction != null &&
                clan.MapFaction.IsAtWarWith(target.MapFaction) &&
                defenders.All(defender => clan.MapFaction.IsAtWarWith(defender.MapFaction)))
            .OrderByDescending(clan => clan.Tier)
            .FirstOrDefault();
        if (fixtureClan == null) return false;

        MobileParty fixtureParty = null;
        try
        {
            fixtureHero = HeroCreator.CreateSpecialHero(fixtureClan.Leader.CharacterObject, target, age: 30);
            var fixtureName = new TextObject("Defender context fixture besieger");
            fixtureHero.SetName(fixtureName, fixtureName);
            fixtureHero.SetNewOccupation(Occupation.Wanderer);
            AddCompanionAction.Apply(fixtureClan, fixtureHero);
            if (!ReferenceEquals(fixtureHero.CompanionOf, fixtureClan)) return false;
            fixtureParty = LordPartyComponent.CreateLordParty(
                "defender_context_fixture_" + Guid.NewGuid().ToString("N"),
                fixtureHero,
                target.GatePosition,
                2f,
                null,
                fixtureHero);
            if (!IsRestorableBesieger(fixtureParty, target, defenders)) return false;
            candidate = fixtureParty;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (candidate == null)
            {
                cleanupFailed = !TryCleanupCreatedBesieger(fixtureParty, fixtureHero, fixtureClan);
                if (cleanupFailed) candidate = fixtureParty;
            }
        }
    }

    private CoopCommandResult CaptureFailure(string status)
    {
        if (!createdBesieger) return Result(false, status);
        bool cleanupComplete = TryRemoveCreatedBesieger();
        if (cleanupComplete && campaign == null) ReleaseCreatedBesiegerOwnership();
        return Result(false, cleanupComplete ? status : "created_besieger_cleanup_failed");
    }

    private bool TryRemoveCreatedBesieger() =>
        TryCleanupCreatedBesieger(besieger, createdBesiegerHero, createdBesiegerClan);

    private static bool TryCleanupCreatedBesieger(MobileParty party, Hero hero, Clan clan)
    {
        if (hero == null) return party?.IsActive != true;
        if (clan == null) return false;
        try
        {
            if (party?.IsActive == true)
            {
                if (!ReferenceEquals(party.LeaderHero, hero) || !ReferenceEquals(party.ActualClan, clan)) return false;
                DestroyPartyAction.Apply(null, party);
            }
            if (hero.CompanionOf != null)
            {
                if (!ReferenceEquals(hero.CompanionOf, clan)) return false;
                RemoveCompanionAction.ApplyByFire(clan, hero);
            }
            if (hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
                KillCharacterAction.ApplyByRemove(hero, false, true);
            return IsCreatedBesiegerRestored(
                party?.IsActive == true,
                hero.CompanionOf != null,
                hero.DeathMark);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void ReleaseCreatedBesiegerOwnership()
    {
        createdBesieger = false;
        createdBesiegerCleanupStarted = false;
        createdBesiegerHero = null;
        createdBesiegerClan = null;
        besieger = null;
        partyId = null;
    }

    internal static bool IsCreatedBesiegerRestored(
        bool partyActive,
        bool companionAttached,
        KillCharacterAction.KillCharacterActionDetail deathMark) =>
        !partyActive && !companionAttached && deathMark != KillCharacterAction.KillCharacterActionDetail.None;

    internal static bool SameBehavior(PartyBehaviorUpdateData expected, PartyBehaviorUpdateData actual) =>
        expected.MobilePartyId == actual.MobilePartyId &&
        expected.BestTargetPoint == actual.BestTargetPoint && expected.PartyPosition == actual.PartyPosition &&
        expected.IsInteractableAnchor == actual.IsInteractableAnchor &&
        expected.IsCurrentlyAtSea == actual.IsCurrentlyAtSea &&
        expected.NewAiBehavior == actual.NewAiBehavior && expected.DefaultBehavior == actual.DefaultBehavior &&
        expected.InteractablePointId == actual.InteractablePointId && expected.TargetPartyId == actual.TargetPartyId &&
        expected.TargetSettlementId == actual.TargetSettlementId && expected.MoveTargetPartyId == actual.MoveTargetPartyId &&
        expected.TargetPosition == actual.TargetPosition && expected.MoveTargetPoint == actual.MoveTargetPoint &&
        expected.DesiredAiNavigationType == actual.DesiredAiNavigationType &&
        expected.PartyMoveMode == actual.PartyMoveMode && expected.IsTargetingPort == actual.IsTargetingPort;

    private CoopCommandResult Result(bool success, string status) => new CoopCommandResult(success,
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            success, status, settlementId = SettlementId, besiegerPartyId = partyId,
            startAttempted, missionExitRequested, restored, captured = campaign != null,
            originalRelation, stagedRelation
        }), success ? null : "defender_context_failed");

    public sealed class CaptureCoopCommand : ICoopCommand
    {
        private readonly IDefenderSiegeContextFixture fixture;
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_context_capture";
        public string Description => "Capture the AI besieger before staging the siege.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CaptureCoopCommand(IDefenderSiegeContextFixture fixture) => this.fixture = fixture;
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Capture();
    }

    public sealed class StartCoopCommand : ICoopCommand
    {
        private readonly IDefenderSiegeContextFixture fixture;
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_context_start";
        public string Description => "Start the captured AI siege context.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public StartCoopCommand(IDefenderSiegeContextFixture fixture) => this.fixture = fixture;
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Start();
    }

    public sealed class EndMissionsCoopCommand : ICoopCommand
    {
        private readonly IDefenderSiegeContextFixture fixture;
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_context_end_missions";
        public string Description => "Exit the captured defenders before releasing their assault.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public EndMissionsCoopCommand(IDefenderSiegeContextFixture fixture) => this.fixture = fixture;
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.EndMissions();
    }

    public sealed class RestoreCoopCommand : ICoopCommand
    {
        private readonly IDefenderSiegeContextFixture fixture;
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_context_restore";
        public string Description => "Restore the captured AI siege context.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public RestoreCoopCommand(IDefenderSiegeContextFixture fixture) => this.fixture = fixture;
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Restore();
    }

    public sealed class VerifyCoopCommand : ICoopCommand
    {
        private readonly IDefenderSiegeContextFixture fixture;
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_context_verify_restore";
        public string Description => "Verify the captured AI siege context.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public VerifyCoopCommand(IDefenderSiegeContextFixture fixture) => this.fixture = fixture;
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) => fixture.Verify();
    }
}
#endif
