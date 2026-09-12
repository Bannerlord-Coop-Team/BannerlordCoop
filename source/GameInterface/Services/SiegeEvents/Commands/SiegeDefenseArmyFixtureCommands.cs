#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using GameInterface.Services.Armies;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.SiegeEvents.Commands;

// Commands share one captured campaign fixture; JSON carries immutable expectations between peers.
internal static class SiegeDefenseArmyFixtureCommands
{
    private static FixtureState fixture;

    public sealed class FixtureStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defense_army_fixture_state";
        public string Description => "Inspect the siege defense fixture and current party identities.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controllerId", "The connected player controller id."),
            new ExpectedArgs("settlementId", "The settlement id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (args.Count != 2) return Failed("Expected 2 arguments");
            return FixtureStateCommand(args);
        }
    }

    public sealed class CaptureFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "capture_defense_army_fixture";
        public string Description => "Capture four clean parties for the paused siege defense fixture.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controllerId", "The connected player controller id."),
            new ExpectedArgs("settlementId", "The settlement id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This command can only be used by the server");
            if (args.Count != 2) return Failed("Expected 2 arguments");
            return CaptureFixture(args);
        }
    }

    public sealed class StageFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "stage_defense_army_fixture";
        public string Description => "Stage the captured player-led army and hostile siege assault.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("capturedState", "The complete LIVE_TEST_JSON object from capture."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This command can only be used by the server");
            if (args.Count != 1) return Failed("Expected 1 arguments");
            return StageFixture(args);
        }
    }

    public sealed class DefenseArmyStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defense_army_state";
        public string Description => "Assert baseline, joined, unstuck, or restored against captured identities.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controllerId", "The connected player controller id."),
            new ExpectedArgs("settlementId", "The settlement id."),
            new ExpectedArgs("state", "baseline, joined, unstuck, or restored."),
            new ExpectedArgs("expectedState", "The complete staged LIVE_TEST_JSON object, including for restored."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (args.Count != 4) return Failed("Expected 4 arguments");
            return DefenseArmyState(args);
        }
    }

    public sealed class RestoreFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "restore_defense_army_fixture";
        public string Description => "Restore only the captured fixture parties and owned siege.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("capturedState", "The complete LIVE_TEST_JSON object from capture or stage."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This command can only be used by the server");
            if (args.Count != 1) return Failed("Expected 1 arguments");
            return RestoreFixture(args);
        }
    }

    public sealed class VerifyFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "verify_defense_army_fixture";
        public string Description => "Verify all captured party movement and fixture cleanup.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("capturedState", "The complete LIVE_TEST_JSON object from capture or stage."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("This command can only be used by the server");
            if (args.Count != 1) return Failed("Expected 1 arguments");
            return VerifyFixture(args);
        }
    }

    private static CoopCommandResult FixtureStateCommand(ICoopCommandArgs args)
    {
        if (!TryResolveContext(args[0], args[1], out var player, out var settlement, out var manager, out var error))
            return Failed(error);
        var expected = fixture?.Campaign == Campaign.Current && fixture.ControllerId == args[0] && fixture.Settlement == settlement
            ? fixture.StagedExpectation ?? fixture.CapturedExpectation : null;
        return FormatState("Siege defense army fixture state", player, settlement, manager, expected);
    }

    private static CoopCommandResult CaptureFixture(ICoopCommandArgs args)
    {
        if (!TryResolveContext(args[0], args[1], out var player, out var settlement, out var manager, out var error))
            return Failed(error);
        if (!TryRequirePause(out error)) return Failed(error);
        if (fixture != null && fixture.Campaign == Campaign.Current && !fixture.Verified)
        {
            if (fixture.ControllerId != args[0] || fixture.Settlement != settlement || fixture.Player.Party != player)
                return Failed("A different siege defense army fixture is already active");
            return FormatState("Captured siege defense army fixture", player, settlement, manager, fixture.CapturedExpectation);
        }
        if (!IsCleanParty(player) || player.LeaderHero == null)
            return Failed("The player must have a leader and be clean on land, with no army or attachments");
        if (!settlement.IsFortification || settlement.SiegeEvent != null || settlement.Party.MapEvent != null)
            return Failed("The target must be a fortification without a siege or map event");
        if (player.MapFaction is not Kingdom kingdom || settlement.MapFaction != kingdom)
            return Failed("The player party and settlement must belong to the same kingdom");
        if (!TryGetId(manager, settlement, out var settlementId)
            || !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviors))
            return Failed("Unable to resolve the registered settlement or fixture snapshot service");

        var followers = MobileParty.AllLordParties
            .Where(p => p != player && p.MapFaction == kingdom && p.LeaderHero != null
                && !p.IsPlayerParty() && IsCleanParty(p) && TryGetId(manager, p, out _))
            .OrderByDescending(p => p.Party.CalculateCurrentStrength())
            .ThenBy(p => GetId(manager, p), StringComparer.Ordinal)
            .Take(2).ToArray();
        var besieger = MobileParty.AllLordParties
            .Where(p => p != player && p.LeaderHero != null && !p.IsPlayerParty()
                && IsCleanParty(p) && p.MapFaction?.IsAtWarWith(kingdom) == true
                && TryGetId(manager, p, out _))
            .OrderByDescending(p => p.Party.CalculateCurrentStrength())
            .ThenBy(p => GetId(manager, p), StringComparer.Ordinal).FirstOrDefault();
        if (followers.Length != 2 || besieger == null)
            return Failed("Need two clean allied AI lord parties and one clean hostile AI lord party");

        var snapshots = new[] { player, besieger }.Concat(followers)
            .Select(p => CaptureParty(p, manager, behaviors)).ToArray();
        if (snapshots.Any(p => p == null) || snapshots.Select(p => p.Id).Distinct().Count() != 4)
            return Failed("Every fixture participant must have a distinct registered id and a movement snapshot");
        fixture = new FixtureState(Guid.NewGuid().ToString("N"), args[0], settlement, settlementId, snapshots);
        fixture.CapturedExpectation = CreateExpectation(fixture, manager);
        return FormatState("Captured siege defense army fixture", player, settlement, manager, fixture.CapturedExpectation);
    }

    private static CoopCommandResult StageFixture(ICoopCommandArgs args)
    {
        if (!TryValidateToken(args[0], out var error)) return Failed(error);
        if (!TryRequirePause(out error)) return Failed(error);
        if (fixture.Staged)
            return FormatOwnedState("Fixture already staged", fixture.StagedExpectation, "baseline");
        if (fixture.MutationStarted)
            return Failed("The previous stage needs successful restoration and verification before recapture");
        if (!TryPreflightStage(out var manager, out var kingdom, out error)) return Failed(error);

        var player = fixture.Player.Party;
        var besieger = fixture.Besieger.Party;
        var settlement = fixture.Settlement;
        var originalArmies = kingdom.Armies.ToArray();
        fixture.MutationStarted = true;
        try
        {
            try
            {
                kingdom.CreateArmy(player.LeaderHero, settlement, Army.ArmyTypes.Defender);
            }
            finally
            {
                fixture.Army = kingdom.Armies.Except(originalArmies).SingleOrDefault(a => a.LeaderParty == player)
                    ?? player.Army;
                fixture.ArmyId = GetId(manager, fixture.Army);
            }
            if (fixture.Army?.LeaderParty != player || !TryGetId(manager, fixture.Army, out _))
                throw new InvalidOperationException("The registered player-led fixture army could not be created.");
            var position = settlement.GatePosition;
            StageAtHold(player, position);
            foreach (var follower in fixture.Followers)
            {
                StageAtHold(follower.Party, position);
                follower.Party.Army = fixture.Army;
                fixture.Army.AddPartyToMergedParties(follower.Party);
            }
            StageAtHold(besieger, settlement.GatePosition);
            besieger.SetMoveBesiegeSettlement(settlement, MobileParty.NavigationType.Default);
            try
            {
                Campaign.Current.SiegeEventManager.StartSiegeEvent(settlement, besieger);
            }
            finally
            {
                fixture.SiegeEvent = settlement.SiegeEvent ?? besieger.BesiegerCamp?.SiegeEvent;
                fixture.SiegeEventId = GetId(manager, fixture.SiegeEvent);
            }
            if (fixture.SiegeEvent?.BesiegerCamp?.LeaderParty != besieger
                || !TryGetId(manager, fixture.SiegeEvent, out _))
                throw new InvalidOperationException("The registered hostile AI siege could not be created.");
            try
            {
                StartBattleAction.ApplyStartAssaultAgainstWalls(besieger, settlement);
            }
            finally
            {
                fixture.MapEvent = settlement.Party.MapEvent ?? besieger.MapEvent;
                fixture.MapEventId = GetId(manager, fixture.MapEvent);
                fixture.InitialEventParties = fixture.MapEvent?.InvolvedParties.ToArray();
            }
            if (fixture.MapEvent?.IsSiegeAssault != true || !TryGetId(manager, fixture.MapEvent, out _))
                throw new InvalidOperationException("The registered hostile AI wall assault could not be created.");
            fixture.StagedExpectation = CreateExpectation(fixture, manager);
            fixture.Staged = true;
            var result = FormatOwnedState("Staged siege defense army fixture", fixture.StagedExpectation, "baseline");
            if (!result.Succeeded) throw new InvalidOperationException(result.Output);
            return result;
        }
        catch (Exception exception)
        {
            try
            {
                RestoreFixtureState();
                fixture.Restored = FormatOwnedState("Fixture rollback", fixture.CapturedExpectation, "restored").Succeeded;
            }
            catch (Exception cleanupException)
            {
                return Failed($"Fixture staging failed: {exception.Message}. Cleanup failed: {cleanupException.Message}. Keep the captured JSON for retry or reload the disposable save.");
            }
            return Failed(fixture.Restored
                ? $"Fixture staging failed: {exception.Message}. Cleanup completed; verify the captured JSON before recapturing."
                : $"Fixture staging failed: {exception.Message}. Cleanup verification failed; retry restore with the captured JSON or reload the disposable save.");
        }
    }

    private static bool TryPreflightStage(out IObjectManager manager, out Kingdom kingdom, out string error)
    {
        kingdom = null;
        if (!TryResolveContext(fixture.ControllerId, fixture.Settlement.StringId,
            out var player, out var settlement, out manager, out error)) return false;
        if (player != fixture.Player.Party || settlement != fixture.Settlement
            || GetId(manager, settlement) != fixture.SettlementId)
        {
            error = "The captured player or settlement identity changed";
            return false;
        }
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviors)
            || !ContainerProvider.TryResolve<ISiegeEventInterface>(out _)
            || !ContainerProvider.TryResolve<IArmyDisbander>(out _))
        {
            error = "Unable to resolve all staging and restoration services";
            return false;
        }
        kingdom = player.MapFaction as Kingdom;
        var capturedKingdom = kingdom;
        if (kingdom == null || settlement.MapFaction != kingdom || settlement.SiegeEvent != null
            || settlement.Party.MapEvent != null || player.LeaderHero == null
            || fixture.Besieger.Party.MapFaction?.IsAtWarWith(kingdom) != true
            || fixture.Followers.Any(p => p.Party.MapFaction != capturedKingdom || p.Party.LeaderHero == null)
            || fixture.Besieger.Party.LeaderHero == null)
        {
            error = "The captured settlement or participant factions are no longer ready";
            return false;
        }
        foreach (var snapshot in fixture.AllParties)
        {
            if (!manager.TryGetObject<MobileParty>(snapshot.Id, out var resolved) || resolved != snapshot.Party
                || !IsCleanParty(resolved) || !TryVerifyParty(behaviors, snapshot))
            {
                error = $"Captured party {snapshot.Id} changed identity, readiness, or movement state";
                return false;
            }
        }
        error = null;
        return true;
    }

    private static CoopCommandResult DefenseArmyState(ICoopCommandArgs args)
    {
        if (!TryReadExpectation(args[3], args[0], args[1], true, out var expected, out var error))
            return Failed(error);
        if (ModInformation.IsServer && !TryValidateToken(args[3], out error)) return Failed(error);
        if (!TryResolveContext(args[0], args[1], out var player, out var settlement, out var manager, out error))
            return Failed(error);
        return FormatState($"Siege defense army {args[2]} state", player, settlement, manager, expected, args[2]);
    }

    private static CoopCommandResult RestoreFixture(ICoopCommandArgs args)
    {
        if (!TryValidateToken(args[0], out var error)) return Failed(error);
        if (!TryRequirePause(out error)) return Failed(error);
        try
        {
            if (!fixture.Restored)
            {
                RestoreFixtureState();
            }
            var result = FormatOwnedState("Restored siege defense army fixture", fixture.CapturedExpectation, "restored");
            fixture.Restored = result.Succeeded;
            return result;
        }
        catch (Exception exception)
        {
            return Failed($"Fixture restoration failed: {exception.Message}. Retain the captured JSON for retry or reload the disposable save.");
        }
    }

    private static CoopCommandResult VerifyFixture(ICoopCommandArgs args)
    {
        if (!TryValidateToken(args[0], out var error)) return Failed(error);
        if (!fixture.Restored) return Failed("Restore the siege defense army fixture before verifying it");
        var result = FormatOwnedState("Verified siege defense army fixture restoration", fixture.CapturedExpectation, "restored");
        fixture.Verified = result.Succeeded;
        return result;
    }

    private static void RestoreFixtureState()
    {
        if (!TryRequirePause(out var error)) throw new InvalidOperationException(error);
        if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var sieges)
            || !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviors)
            || !ContainerProvider.TryResolve<IArmyDisbander>(out var armies)
            || !ContainerProvider.TryResolve<IObjectManager>(out var manager))
            throw new InvalidOperationException("Unable to resolve the fixture restoration services.");
        ValidateCleanupOwnership(manager);
        if (fixture.MapEvent != null && !fixture.MapEvent.IsFinalized)
        {
            // Normal finalization also closes the joined players' encounters.
            MessageBroker.Instance.Publish(fixture.MapEvent, new MapEventFinalizeAttempted(fixture.MapEvent));
            if (!fixture.MapEvent.IsFinalized)
                throw new InvalidOperationException("The fixture map event did not finalize.");
        }
        if (fixture.AllParties.Any(p => p.Party.MapEvent != null))
            throw new InvalidOperationException("A captured party still belongs to the finalized map event.");
        if (fixture.Settlement.SiegeEvent != null)
        {
            foreach (var party in fixture.SiegeEvent.BesiegerCamp?._besiegerParties.ToArray() ?? Array.Empty<MobileParty>())
                sieges.BreakSiege(party);
            if (fixture.Settlement.SiegeEvent == fixture.SiegeEvent)
                fixture.SiegeEvent.FinalizeSiegeEvent();
        }
        if (fixture.Army != null && (fixture.Army.Parties.Count > 0 || manager.Contains(fixture.Army)))
            armies.Disband(fixture.Army, Army.ArmyDispersionReason.ObjectiveFinished);
        foreach (var snapshot in fixture.AllParties)
        {
            if (snapshot.Party.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(snapshot.Party);
            if (!behaviors.TryApply(snapshot.Party, snapshot.Behavior, out _))
                throw new InvalidOperationException($"Unable to restore movement state for {snapshot.Id}.");
            snapshot.Party.Position = snapshot.Behavior.PartyPosition;
            PublishForcedPosition(snapshot.Party);
        }
    }

    private static void ValidateCleanupOwnership(IObjectManager manager)
    {
        if (GetId(manager, fixture.Settlement) != fixture.SettlementId
            || (fixture.Settlement.SiegeEvent != null && fixture.Settlement.SiegeEvent != fixture.SiegeEvent)
            || (fixture.Settlement.Party.MapEvent != null && fixture.Settlement.Party.MapEvent != fixture.MapEvent))
            throw new InvalidOperationException("The settlement now belongs to a different siege or map event.");
        var allies = new[] { fixture.Player }.Concat(fixture.Followers).Select(p => p.Party).ToArray();
        if (fixture.Army != null && fixture.Army.Parties.Count > 0
            && (fixture.Army.LeaderParty != fixture.Player.Party || fixture.Army.Parties.Any(p => !allies.Contains(p))))
            throw new InvalidOperationException("The fixture army acquired an unrelated leader or party.");
        if (fixture.SiegeEvent?.BesiegerCamp?._besiegerParties.Any(p => p != fixture.Besieger.Party) == true)
            throw new InvalidOperationException("The fixture siege acquired an unrelated besieger.");
        var allowedEventParties = (fixture.InitialEventParties ?? Array.Empty<PartyBase>())
            .Concat(fixture.AllParties.Select(p => p.Party.Party)).ToArray();
        if (fixture.MapEvent?.InvolvedParties.Any(p => !allowedEventParties.Contains(p)) == true)
            throw new InvalidOperationException("The fixture map event acquired an unrelated party.");
        foreach (var snapshot in fixture.AllParties)
        {
            var party = snapshot.Party;
            if (!manager.TryGetObject<MobileParty>(snapshot.Id, out var resolved) || resolved != party
                || (party.Army != null && (party.Army != fixture.Army || !allies.Contains(party)))
                || (party.MapEvent != null && party.MapEvent != fixture.MapEvent)
                || (party.CurrentSettlement != null && party.CurrentSettlement != fixture.Settlement)
                || (party.BesiegerCamp != null && party.BesiegerCamp != fixture.SiegeEvent?.BesiegerCamp)
                || (party.AttachedTo != null && (party.AttachedTo != fixture.Player.Party || !allies.Contains(party)))
                || party.AttachedParties.Any(p => !allies.Contains(p)))
                throw new InvalidOperationException($"Captured party {snapshot.Id} has unrelated state; cleanup cannot overwrite it.");
        }
    }

    private static bool TryResolveContext(string controllerId, string settlementId, out MobileParty playerParty,
        out Settlement settlement, out IObjectManager manager, out string error)
    {
        playerParty = null;
        settlement = null;
        error = null;
        if (!ContainerProvider.TryResolve<IObjectManager>(out manager)
            || !ContainerProvider.TryResolve<IPlayerManager>(out var players) || Campaign.Current == null)
        {
            error = "Unable to resolve the siege defense fixture campaign services";
            return false;
        }
        if (!players.TryGetPlayer(controllerId, out var player) || (ModInformation.IsServer && !players.IsConnected(player))
            || !manager.TryGetObject<MobileParty>(player.MobilePartyId, out playerParty))
        {
            error = $"Unable to resolve connected player {controllerId}";
            return false;
        }
        if (!manager.TryGetObject<Settlement>(settlementId, out settlement))
        {
            error = $"Settlement with id {settlementId} not found";
            return false;
        }
        return true;
    }

    internal static bool IsCleanParty(MobileParty party) =>
        party?.IsActive == true && party.Position.IsOnLand && !party.IsCurrentlyAtSea
        && party.Army == null && party.AttachedTo == null && party.AttachedParties.Count == 0
        && party.CurrentSettlement == null && party.BesiegerCamp == null
        && party.MapEvent == null && !party.IsTransitionInProgress;

    private static bool TryRequirePause(out string error)
    {
        error = null;
        if (ContainerProvider.TryResolve<ITimeControlInterface>(out var time)
            && time.GetTimeControl() == TimeControlEnum.Pause) return true;
        error = "Pause the authoritative campaign before capture, staging, assertions, or cleanup";
        return false;
    }

    private static PartySnapshot CaptureParty(MobileParty party, IObjectManager manager, IMobilePartyBehaviorSnapshot behaviors) =>
        TryGetId(manager, party, out var id) && behaviors.TryCreate(party, out var behavior)
            && behavior.MobilePartyId == id ? new PartySnapshot(party, id, behavior) : null;

    private static void StageAtHold(MobileParty party, CampaignVec2 position)
    {
        party.Position = position;
        party.SetMoveModeHold();
        party.SetNavigationModeHold();
        PublishForcedPosition(party);
    }

    private static void PublishForcedPosition(MobileParty party) =>
        MessageBroker.Instance.Publish(typeof(SiegeDefenseArmyFixtureCommands),
            new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: party.IsCurrentlyAtSea));

    private static bool TryVerifyParty(IMobilePartyBehaviorSnapshot behaviors, PartySnapshot snapshot) =>
        behaviors.TryCreate(snapshot.Party, out var actual)
        && JToken.DeepEquals(JToken.FromObject(GetBehaviorProof(actual)), JToken.FromObject(GetBehaviorProof(snapshot.Behavior)));

    private static JObject CreateExpectation(FixtureState state, IObjectManager manager) => JObject.FromObject(new
    {
        schemaVersion = 1,
        buildVersion = ModInformation.BuildVersion,
        commit = ModInformation.Commit,
        fixtureToken = state.Token,
        controllerId = state.ControllerId,
        settlementId = state.Settlement.StringId,
        settlementNetworkId = state.SettlementId,
        playerPartyId = state.Player.Id,
        besiegerPartyId = state.Besieger.Id,
        followerPartyIds = state.Followers.Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
        armyId = state.ArmyId,
        siegeEventId = state.SiegeEventId,
        mapEventId = state.MapEventId,
        capturedParties = state.AllParties.Select(p => new { partyId = p.Id, behavior = GetBehaviorProof(p.Behavior) }).ToArray(),
    });

    internal static bool TryReadExpectation(string json, string controllerId, string settlementId, bool staged,
        out JObject expected, out string error)
    {
        expected = null;
        error = "Expected the complete captured or staged LIVE_TEST_JSON object with matching identities";
        try
        {
            expected = JObject.Parse(json)["expectation"] as JObject;
            if (expected == null || expected.Value<int?>("schemaVersion") != 1
                || !Guid.TryParseExact(expected.Value<string>("fixtureToken"), "N", out _)
                || expected.Value<string>("controllerId") != controllerId
                || expected.Value<string>("settlementId") != settlementId
                || string.IsNullOrWhiteSpace(expected.Value<string>("settlementNetworkId"))
                || string.IsNullOrWhiteSpace(expected.Value<string>("buildVersion"))
                || string.IsNullOrWhiteSpace(expected.Value<string>("commit"))) return false;
            var ids = GetExpectedPartyIds(expected);
            if (ids.Length != 4 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct().Count() != 4) return false;
            var captured = expected["capturedParties"] as JArray;
            if (captured?.Count != 4 || captured.Any(p => p["behavior"] is not JObject)
                || !SameIds(ids, captured.Select(p => p.Value<string>("partyId")))) return false;
            if (staged)
            {
                foreach (var name in new[] { "armyId", "siegeEventId", "mapEventId" })
                    if (string.IsNullOrWhiteSpace(expected.Value<string>(name))) return false;
            }
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is JsonException || exception is InvalidCastException || exception is FormatException)
        {
            return false;
        }
    }

    private static bool TryValidateToken(string json, out string error)
    {
        if (fixture == null || fixture.Campaign != Campaign.Current)
        {
            error = "The siege defense army fixture is not active in this campaign; capture the reloaded baseline again";
            return false;
        }
        if (!TryReadExpectation(json, fixture.ControllerId, fixture.Settlement.StringId, false, out var expected, out error))
            return false;
        return MatchesCapture(fixture.CapturedExpectation, expected, out error);
    }

    internal static bool MatchesCapture(JObject captured, JObject expected, out string error)
    {
        error = null;
        foreach (var name in new[] { "buildVersion", "commit", "fixtureToken", "controllerId", "settlementId", "settlementNetworkId", "playerPartyId",
            "besiegerPartyId", "followerPartyIds", "capturedParties" })
        {
            if (!JToken.DeepEquals(expected[name], captured[name]))
            {
                error = $"The captured fixture {name} does not match the active fixture";
                return false;
            }
        }
        return true;
    }

    private static string[] GetExpectedPartyIds(JObject expected) =>
        new[] { expected.Value<string>("playerPartyId"), expected.Value<string>("besiegerPartyId") }
            .Concat((expected["followerPartyIds"] as JArray)?.Values<string>() ?? Array.Empty<string>()).ToArray();

    private static bool SameIds(IEnumerable<string> first, IEnumerable<string> second) =>
        first.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(second.OrderBy(id => id, StringComparer.Ordinal));

    internal static bool EvaluateState(JObject expected, JObject observed, string state, out string error)
    {
        error = null;
        if (state != "baseline" && state != "joined" && state != "unstuck" && state != "restored")
            error = $"Unknown defense army state '{state}'";
        else if (observed.Value<string>("buildVersion") != expected.Value<string>("buildVersion")
            || observed.Value<string>("commit") != expected.Value<string>("commit"))
            error = "The observed build does not match the captured source identity";
        else if (observed.Value<bool?>("paused") != true)
            error = "The campaign must remain paused";
        else if (observed.Value<string>("playerPartyId") != expected.Value<string>("playerPartyId")
            || observed.Value<string>("settlementNetworkId") != expected.Value<string>("settlementNetworkId"))
            error = "Player or settlement identity differs from capture";
        if (error != null) return false;

        var records = observed["parties"] as JArray;
        var ids = GetExpectedPartyIds(expected);
        if (records == null || records.Count != 4 || !SameIds(ids, records.Select(p => p.Value<string>("partyId")))
            || records.Any(p => p.Value<bool?>("exists") != true || p.Value<bool?>("active") != true))
        {
            error = "A captured fixture participant is missing or has a different identity";
            return false;
        }
        if (state == "restored")
        {
            if (observed.Value<bool?>("ownedArmyRegistered") != false
                || observed.Value<bool?>("ownedSiegeRegistered") != false
                || observed.Value<bool?>("ownedMapEventRegistered") != false)
            {
                error = "A fixture-owned army, siege, or map event remains registered";
                return false;
            }
            foreach (var record in records)
            {
                var original = expected["capturedParties"].Single(p => p.Value<string>("partyId") == record.Value<string>("partyId"));
                if (!IsDetachedRecord(record) || record.Value<bool?>("armyActive") != false
                    || record.Value<bool?>("attachedToActive") != false || record["attachedPartyIds"]?.Count() != 0
                    || !JToken.DeepEquals(record["behavior"], original["behavior"]))
                {
                    error = $"Party {record.Value<string>("partyId")} did not restore its captured movement or clean state";
                    return false;
                }
            }
            if (observed.Value<bool?>("siegeActive") != false || observed.Value<bool?>("settlementMapEventActive") != false
                || observed.Value<bool?>("encounterActive") != false || observed.Value<string>("menu") != null)
                error = "The fixture siege, map event, or local encounter/menu remains active";
            return error == null;
        }

        var playerId = expected.Value<string>("playerPartyId");
        var armyIds = new[] { playerId }.Concat(expected["followerPartyIds"].Values<string>()).ToArray();
        var armyId = expected.Value<string>("armyId");
        if (string.IsNullOrWhiteSpace(armyId) || observed.Value<string>("armyId") != armyId
            || observed.Value<string>("armyLeaderPartyId") != playerId
            || !SameIds(armyIds, (observed["armyPartyIds"] as JArray)?.Values<string>() ?? Array.Empty<string>()))
        {
            error = "The captured army identity, leader, or exact member set changed";
            return false;
        }
        foreach (var record in records.Where(p => armyIds.Contains(p.Value<string>("partyId"))))
        {
            bool isLeader = record.Value<string>("partyId") == playerId;
            var attachedIds = (record["attachedPartyIds"] as JArray)?.Values<string>() ?? Array.Empty<string>();
            if (record.Value<string>("armyId") != armyId
                || record.Value<string>("attachedToId") != (isLeader ? null : playerId)
                || !SameIds(isLeader ? armyIds.Where(id => id != playerId) : Array.Empty<string>(), attachedIds))
            {
                error = $"Party {record.Value<string>("partyId")} lost its army or attachment identity";
                return false;
            }
            if (state == "joined")
            {
                if (record.Value<string>("mapEventId") != expected.Value<string>("mapEventId")
                    || record.Value<string>("canonicalSide") != "Defender")
                    error = $"Party {record.Value<string>("partyId")} is not on the staged canonical defender side";
            }
            else if (!IsDetachedRecord(record))
                error = $"Party {record.Value<string>("partyId")} still belongs to an event, settlement, or camp";
            if (error != null) return false;
        }
        if (state == "baseline" || state == "joined")
        {
            if (observed.Value<string>("siegeEventId") != expected.Value<string>("siegeEventId")
                || observed.Value<string>("settlementMapEventId") != expected.Value<string>("mapEventId")
                || (state == "baseline" && observed.Value<bool?>("siegeAssault") != true)
                || (state == "joined" && observed.Value<string>("battleType") != MapEvent.BattleTypes.Siege.ToString()
                    && observed.Value<string>("battleType") != MapEvent.BattleTypes.SiegeOutside.ToString()))
                error = "The staged siege identity or defending battle type changed";
        }
        else if (observed.Value<bool?>("encounterActive") != false || observed.Value<string>("menu") != null)
            error = "The client's encounter remains active or its menu remains open after unstuck";
        return error == null;
    }

    private static bool IsDetachedRecord(JToken record) => record.Value<bool?>("mapEventActive") == false
        && record.Value<bool?>("settlementActive") == false && record.Value<bool?>("campActive") == false;

    private static JObject Observe(MobileParty player, Settlement settlement, IObjectManager manager, JObject expected)
    {
        ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviors);
        var ids = expected != null ? GetExpectedPartyIds(expected) : new[] { GetId(manager, player) };
        bool isLocalPlayer = ModInformation.IsClient && MobileParty.MainParty == player;
        return JObject.FromObject(new
        {
            buildVersion = ModInformation.BuildVersion,
            commit = ModInformation.Commit,
            paused = TryRequirePause(out _),
            playerPartyId = GetId(manager, player),
            settlementNetworkId = GetId(manager, settlement),
            siegeActive = settlement.SiegeEvent != null,
            siegeEventId = GetId(manager, settlement.SiegeEvent),
            settlementMapEventActive = settlement.Party.MapEvent != null,
            settlementMapEventId = GetId(manager, settlement.Party.MapEvent),
            siegeAssault = settlement.Party.MapEvent?.IsSiegeAssault == true,
            battleType = settlement.Party.MapEvent?.EventType.ToString(),
            armyId = GetId(manager, player.Army),
            armyLeaderPartyId = GetId(manager, player.Army?.LeaderParty),
            armyPartyIds = player.Army?.Parties.Select(p => GetId(manager, p)).ToArray() ?? Array.Empty<string>(),
            encounterActive = isLocalPlayer && PlayerEncounter.Current != null,
            menu = isLocalPlayer ? Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId : null,
            ownedArmyRegistered = IsOwnedRegistered(manager, expected, "armyId", fixture?.Army),
            ownedSiegeRegistered = IsOwnedRegistered(manager, expected, "siegeEventId", fixture?.SiegeEvent),
            ownedMapEventRegistered = IsOwnedRegistered(manager, expected, "mapEventId", fixture?.MapEvent),
            parties = ids.Select(id => ObserveParty(id, manager, behaviors)).ToArray(),
        });
    }

    private static bool IsOwnedRegistered(IObjectManager manager, JObject expected, string idField, object owned)
    {
        var id = expected?.Value<string>(idField);
        if (!string.IsNullOrWhiteSpace(id) && manager.Contains(id)) return true;
        return ModInformation.IsServer && fixture?.Campaign == Campaign.Current
            && fixture.Token == expected?.Value<string>("fixtureToken") && owned != null && manager.Contains(owned);
    }

    private static object ObserveParty(string id, IObjectManager manager, IMobilePartyBehaviorSnapshot behaviors)
    {
        manager.TryGetObject<MobileParty>(id, out var party);
        return new
        {
            partyId = id,
            exists = party != null && GetId(manager, party) == id,
            active = party?.IsActive == true,
            name = party?.Name?.ToString(),
            armyActive = party?.Army != null,
            armyId = GetId(manager, party?.Army),
            attachedToActive = party?.AttachedTo != null,
            attachedToId = GetId(manager, party?.AttachedTo),
            attachedPartyIds = party?.AttachedParties.Select(p => GetId(manager, p)).ToArray() ?? Array.Empty<string>(),
            mapEventActive = party?.MapEvent != null,
            mapEventId = GetId(manager, party?.MapEvent),
            canonicalSide = GetCanonicalSide(party?.MapEvent, party?.Party.MapEventSide)?.ToString(),
            missionSide = party?.Party.MapEventSide?.MissionSide.ToString(),
            settlementActive = party?.CurrentSettlement != null,
            settlementId = GetId(manager, party?.CurrentSettlement),
            campActive = party?.BesiegerCamp != null,
            campSiegeEventId = GetId(manager, party?.BesiegerCamp?.SiegeEvent),
            behavior = party != null && behaviors != null && behaviors.TryCreate(party, out var behavior)
                ? GetBehaviorProof(behavior) : null,
        };
    }

    private static CoopCommandResult FormatOwnedState(string label, JObject expected, string state)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var manager)) return Failed("Unable to resolve object manager");
        if (state == "restored")
        {
            expected = (JObject)fixture.CapturedExpectation.DeepClone();
            expected["armyId"] = fixture.ArmyId;
            expected["siegeEventId"] = fixture.SiegeEventId;
            expected["mapEventId"] = fixture.MapEventId;
        }
        return FormatState(label, fixture.Player.Party, fixture.Settlement, manager, expected, state);
    }

    private static CoopCommandResult FormatState(string label, MobileParty player, Settlement settlement,
        IObjectManager manager, JObject expected, string state = null)
    {
        var observed = Observe(player, settlement, manager, expected);
        return FormatStateResult(label, expected, observed, state);
    }

    internal static CoopCommandResult FormatStateResult(string label, JObject expected, JObject observed, string state)
    {
        string error = null;
        bool success = state == null || EvaluateState(expected, observed, state, out error);
        var result = new
        {
            success, label, expectedState = state, error, expectation = expected, observed,
            fixtureTopologyRestored = state == "restored" && success,
            saveBaselineRestored = false,
        };
        return new CoopCommandResult(success, label + Environment.NewLine + "LIVE_TEST_JSON="
            + JsonConvert.SerializeObject(result), success ? null : "fixture_assertion_failed");
    }

    private static CoopCommandResult Failed(string error) => new CoopCommandResult(false, error, "command_failed");

    private static BattleSideEnum? GetCanonicalSide(MapEvent mapEvent, MapEventSide side)
    {
        if (mapEvent == null || side == null) return null;
        if (ReferenceEquals(mapEvent.DefenderSide, side)) return BattleSideEnum.Defender;
        if (ReferenceEquals(mapEvent.AttackerSide, side)) return BattleSideEnum.Attacker;
        return null;
    }

    private static string GetId(IObjectManager manager, object value) =>
        TryGetId(manager, value, out var id) ? id : null;

    private static bool TryGetId(IObjectManager manager, object value, out string id)
    {
        id = null;
        return value != null && manager.TryGetId(value, out id) && !string.IsNullOrWhiteSpace(id);
    }
    private static object GetBehaviorProof(PartyBehaviorUpdateData behavior) => new
    {
        behavior.MobilePartyId,
        newAiBehavior = behavior.NewAiBehavior.ToString(),
        behavior.InteractablePointId,
        bestTargetPoint = GetPositionProof(behavior.BestTargetPoint),
        partyPosition = GetPositionProof(behavior.PartyPosition),
        defaultBehavior = behavior.DefaultBehavior.ToString(),
        targetPosition = GetPositionProof(behavior.TargetPosition),
        desiredAiNavigationType = behavior.DesiredAiNavigationType.ToString(),
        behavior.TargetPartyId,
        behavior.TargetSettlementId,
        moveTargetPoint = GetPositionProof(behavior.MoveTargetPoint),
        behavior.IsTargetingPort,
        partyMoveMode = behavior.PartyMoveMode.ToString(),
        behavior.MoveTargetPartyId,
        behavior.IsInteractableAnchor,
        behavior.IsCurrentlyAtSea,
    };

    private static object GetPositionProof(CampaignVec2 position) => new
    {
        position.X,
        position.Y,
        position.IsOnLand,
    };


    private sealed class FixtureState
    {
        public Campaign Campaign { get; }
        public string Token { get; }
        public string ControllerId { get; }
        public Settlement Settlement { get; }
        public string SettlementId { get; }
        public PartySnapshot[] AllParties { get; }
        public PartySnapshot Player => AllParties[0];
        public PartySnapshot Besieger => AllParties[1];
        public IEnumerable<PartySnapshot> Followers => AllParties.Skip(2);
        public string ArmyId { get; set; }
        public string SiegeEventId { get; set; }
        public string MapEventId { get; set; }
        public Army Army { get; set; }
        public SiegeEvent SiegeEvent { get; set; }
        public MapEvent MapEvent { get; set; }
        public PartyBase[] InitialEventParties { get; set; }
        public bool MutationStarted { get; set; }
        public bool Staged { get; set; }
        public bool Restored { get; set; }
        public bool Verified { get; set; }
        public JObject CapturedExpectation { get; set; }
        public JObject StagedExpectation { get; set; }

        public FixtureState(string token, string controllerId, Settlement settlement, string settlementId, PartySnapshot[] parties)
        {
            Campaign = TaleWorlds.CampaignSystem.Campaign.Current;
            Token = token;
            ControllerId = controllerId;
            Settlement = settlement;
            SettlementId = settlementId;
            AllParties = parties;
        }
    }

    private sealed class PartySnapshot
    {
        public MobileParty Party { get; }
        public string Id { get; }
        public PartyBehaviorUpdateData Behavior { get; }

        public PartySnapshot(MobileParty party, string id, PartyBehaviorUpdateData behavior)
        {
            Party = party;
            Id = id;
            Behavior = behavior;
        }
    }
}
#endif
