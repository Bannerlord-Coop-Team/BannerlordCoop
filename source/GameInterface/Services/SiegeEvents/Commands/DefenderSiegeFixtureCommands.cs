using Autofac;
using Common;
using Common.Commands;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI;
using HarmonyLib;
using Newtonsoft.Json;
using SandBox.View.Map.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.SiegeEvents.Commands;

/// <summary>Stages two connected defender parties inside Odrysa Castle for a restorable siege fixture.</summary>
internal static class DefenderSiegeFixtureCommands
{
#if DEBUG
    public sealed class CaptureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_fixture_capture";
        public string Description => "Captures both defender parties before staging.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("first_controller_id", "First defender controller."),
            new ExpectedArgs("second_controller_id", "Second defender controller.")
        };
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = Capture(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class CaptureRosterFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_roster_fixture_capture";
        public string Description => "Captures the defender roster before normalization.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("first_controller_id", "First defender controller."),
            new ExpectedArgs("second_controller_id", "Second defender controller.")
        };
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = CaptureRosterFixture(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class PrepareRosterCaptiveBaselineCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_roster_fixture_prepare_captive_baseline";
        public string Description => "Creates one reversible captive baseline for defender roster verification.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("first_controller_id", "First defender controller."),
            new ExpectedArgs("second_controller_id", "Second defender controller.")
        };
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = PrepareRosterCaptiveBaseline(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class RestoreRosterCaptiveBaselineCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_roster_fixture_restore_captive_baseline";
        public string Description => "Restores the pre-setup defender roster baseline.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = RestoreRosterCaptiveBaseline(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class VerifyRosterCaptiveBaselineRestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_roster_fixture_verify_captive_baseline_restore";
        public string Description => "Verifies restoration of the pre-setup defender roster baseline.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = VerifyRosterCaptiveBaselineRestore(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class ObserveRosterFixtureReadinessCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_roster_fixture_readiness";
        public string Description => "Reports defender roster capture readiness without changing fixture state.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("first_controller_id", "First defender controller."),
            new ExpectedArgs("second_controller_id", "Second defender controller.")
        };
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = ObserveRosterFixtureReadiness(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class NormalizeRosterFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_roster_fixture_normalize";
        public string Description => "Normalizes the captured defender roster.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = NormalizeRosterFixture(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class RestoreRosterFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_roster_fixture_restore";
        public string Description => "Restores the captured defender roster.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = RestoreRosterFixture(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class VerifyRosterFixtureRestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_roster_fixture_verify_restore";
        public string Description => "Verifies defender roster restoration.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = VerifyRosterFixtureRestore(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class StageCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_fixture_stage";
        public string Description => "Stages both defenders inside the castle.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = Stage(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class RestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_fixture_restore";
        public string Description => "Restores both captured defender parties.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = Restore(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class VerifyRestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_fixture_verify_restore";
        public string Description => "Verifies defender party restoration.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = VerifyRestore(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }

    public sealed class PreAssaultAcknowledgementCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.siege";
        public string Name => "defender_preassault_ack";
        public string Description => "Reports replicated inside-castle defender readiness.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("first_controller_id", "First defender controller."),
            new ExpectedArgs("second_controller_id", "Second defender controller.")
        };
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string output = PreAssaultAcknowledgement(args.ToList());
            bool success = Newtonsoft.Json.Linq.JObject.Parse(output.Substring("LIVE_TEST_JSON=".Length)).Value<bool>("success");
            return new CoopCommandResult(success, output, success ? null : "fixture_failed");
        }
    }
#endif

    private const string SettlementId = "castle_ES1";
    private const int ExpectedPlayerCount = 2;

    private static DefenderSiegeFixture pendingCapture;
    private static DefenderSiegeFixture activeFixture;
    private static DefenderSiegeFixture restoredFixture;

#if DEBUG
    private static DefenderRosterFixture rosterFixture;
    private static DefenderRosterCaptiveBaselineFixture rosterCaptiveBaselineFixture;

    [ThreadStatic]
    private static DefenderRosterFixturePlayer captivityLogPlayer;
    [ThreadStatic]
    private static bool captivityLogRelease;
#endif

    public static string Capture(List<string> args)
    {
        if (ModInformation.IsClient)
            return Failure("capture", "Command can only be run on the server.");
        if (!TryGetExpectedControllerIds(args, out string[] expectedControllerIds, out string error))
            return Failure("capture", error);
        if (activeFixture != null || restoredFixture != null)
            return Failure("capture", "A defender fixture lifecycle is already active.");
        if (pendingCapture != null)
        {
            if (!pendingCapture.HasExpectedControllers(expectedControllerIds) ||
                !IsCaptureCurrent(pendingCapture))
            {
                return Failure("capture", "The pending defender fixture capture is stale or belongs to other controllers.");
            }

            return FixtureResult(pendingCapture, "capture", success: true, reason: null);
        }
        if (!TryCreateFixture(expectedControllerIds, out DefenderSiegeFixture fixture, out error))
            return Failure("capture", error);

        pendingCapture = fixture;
        return FixtureResult(fixture, "capture", success: true, reason: null);
    }

#if DEBUG
    public static string CaptureRosterFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return RosterFixtureFailure("capture", "Command can only be run on the server.");
        if (!TryGetExpectedControllerIds(args, out string[] expectedControllerIds, out _))
        {
            return RosterFixtureFailure("capture",
                "Usage: coop.debug.siege.defender_roster_fixture_capture <firstControllerId> <secondControllerId>");
        }
        if (rosterFixture != null || pendingCapture != null || activeFixture != null || restoredFixture != null)
        {
            return RosterFixtureFailure("capture",
                "A defender roster or siege fixture lifecycle is already active.");
        }
        if (rosterCaptiveBaselineFixture != null)
        {
            if (!rosterCaptiveBaselineFixture.HasExpectedControllers(expectedControllerIds))
            {
                return RosterFixtureFailure("capture",
                    "The prepared captive baseline is stale or belongs to other controllers.");
            }
            if (!IsPreparedRosterCaptiveBaselineCurrent(rosterCaptiveBaselineFixture, out string baselineError))
            {
                return RosterFixtureFailure("capture",
                    baselineError ?? "The prepared captive baseline is stale or belongs to other controllers.");
            }
        }
        if (!TryCreateRosterFixture(
                expectedControllerIds,
                requireCaptive: true,
                out DefenderRosterFixture fixture,
                out string error))
            return RosterFixtureFailure("capture", error);

        rosterFixture = fixture;
        return RosterFixtureResult(fixture, "capture", success: true, reason: null);
    }

    public static string PrepareRosterCaptiveBaseline(List<string> args)
    {
        if (ModInformation.IsClient)
            return RosterCaptiveBaselineFailure("prepare", "Command can only be run on the server.");
        if (!TryGetExpectedControllerIds(args, out string[] expectedControllerIds, out _))
        {
            return RosterCaptiveBaselineFailure("prepare",
                "Usage: coop.debug.siege.defender_roster_fixture_prepare_captive_baseline <firstControllerId> <secondControllerId>");
        }
        if (rosterCaptiveBaselineFixture != null)
        {
            if (!rosterCaptiveBaselineFixture.HasExpectedControllers(expectedControllerIds))
            {
                return RosterCaptiveBaselineResult(
                    rosterCaptiveBaselineFixture,
                    "prepare",
                    success: false,
                    reason: "The prepared captive baseline is stale or belongs to other controllers.");
            }
            if (!IsPreparedRosterCaptiveBaselineCurrent(rosterCaptiveBaselineFixture, out string currentError))
            {
                return RosterCaptiveBaselineResult(
                    rosterCaptiveBaselineFixture,
                    "prepare",
                    success: false,
                    reason: currentError ?? "The prepared captive baseline is stale or belongs to other controllers.");
            }

            return RosterCaptiveBaselineResult(rosterCaptiveBaselineFixture, "prepare", success: true, reason: null);
        }
        if (rosterFixture != null || pendingCapture != null || activeFixture != null || restoredFixture != null)
        {
            return RosterCaptiveBaselineFailure("prepare",
                "A defender roster or siege fixture lifecycle is already active.");
        }
        if (!TryCreateRosterFixture(
                expectedControllerIds,
                requireCaptive: false,
                out DefenderRosterFixture fixture,
                out string error))
        {
            return RosterCaptiveBaselineFailure("prepare", error);
        }
        if (fixture.Players.Any(player => player.WasCaptive))
        {
            return RosterCaptiveBaselineFailure("prepare",
                "The selected roster already has a captive baseline; do not create a second captivity setup.");
        }

        DefenderRosterFixturePlayer captive = fixture.Players[0];
        DefenderRosterFixturePlayer captor = fixture.Players[1];
        if (!CanPrepareRosterCaptiveBaseline(fixture, captive, captor, out error))
            return RosterCaptiveBaselineFailure("prepare", error);

        captive.CaptorParty = captor.Party.Party;
        var baseline = new DefenderRosterCaptiveBaselineFixture(fixture, captive, captor);
        rosterCaptiveBaselineFixture = baseline;
        baseline.CaptureAttempted = true;
        try
        {
            RunFixtureCaptivityAction(captive, release: false);
        }
        catch (Exception exception)
        {
            if (IsRosterCaptiveBaselineRestored(baseline, out _))
            {
                rosterCaptiveBaselineFixture = null;
                return RosterCaptiveBaselineFailure(
                    "prepare",
                    "The authoritative captivity setup for " + captive.ControllerId + " threw " +
                    exception.GetType().Name + " before changing the prepared baseline.");
            }

            return RosterCaptiveBaselineResult(
                baseline,
                "prepare",
                success: false,
                reason: "The authoritative captivity setup for " + captive.ControllerId + " threw " +
                        exception.GetType().Name + "; preserve the retained baseline for explicit recovery.");
        }

        if (!IsPreparedRosterCaptiveBaselineCurrent(baseline, out error))
        {
            if (IsRosterCaptiveBaselineRestored(baseline, out _))
            {
                rosterCaptiveBaselineFixture = null;
                return RosterCaptiveBaselineFailure(
                    "prepare",
                    "The authoritative captivity setup did not create a captive baseline.");
            }

            return RosterCaptiveBaselineResult(
                baseline,
                "prepare",
                success: false,
                reason: error ?? "The authoritative captivity setup left an ambiguous baseline.");
        }

        return RosterCaptiveBaselineResult(baseline, "prepare", success: true, reason: null);
    }

    public static string RestoreRosterCaptiveBaseline(List<string> args)
    {
        if (ModInformation.IsClient)
            return RosterCaptiveBaselineFailure("restore", "Command can only be run on the server.");
        if (args.Count != 0)
        {
            return RosterCaptiveBaselineFailure("restore",
                "Usage: coop.debug.siege.defender_roster_fixture_restore_captive_baseline");
        }
        if (rosterCaptiveBaselineFixture == null)
        {
            return RosterCaptiveBaselineFailure("restore",
                "No prepared defender captive baseline is active.");
        }
        if (rosterFixture != null || pendingCapture != null || activeFixture != null || restoredFixture != null)
        {
            return RosterCaptiveBaselineResult(
                rosterCaptiveBaselineFixture,
                "restore",
                success: false,
                reason: "Restore and verify the defender roster and siege fixture before restoring the prepared baseline.");
        }

        DefenderRosterCaptiveBaselineFixture baseline = rosterCaptiveBaselineFixture;
        if (!HasCurrentRosterIdentities(baseline.Fixture, out string error))
            return RosterCaptiveBaselineResult(baseline, "restore", success: false, reason: error);
        if (baseline.RestoredPendingVerification)
        {
            bool restored = IsRosterCaptiveBaselineRestored(baseline, out error);
            return RosterCaptiveBaselineResult(baseline, "restore", restored, error);
        }

        if (IsPreparedRosterCaptiveBaselineCurrent(baseline, out _))
        {
            try
            {
                RunFixtureCaptivityAction(baseline.Captive, release: true);
            }
            catch (Exception exception)
            {
                if (!IsReleasedForRestoration(baseline.Captive))
                {
                    return RosterCaptiveBaselineResult(
                        baseline,
                        "restore",
                        success: false,
                        reason: "The authoritative captivity restore for " + baseline.Captive.ControllerId + " threw " +
                                exception.GetType().Name + "; the prepared baseline remains retained.");
                }
            }
        }
        else if (!IsReleasedForRestoration(baseline.Captive))
        {
            return RosterCaptiveBaselineResult(
                baseline,
                "restore",
                success: false,
                reason: "The prepared captive baseline is no longer safe to restore.");
        }

        if (!TryRestoreRecapturedPartySnapshot(baseline.Fixture, baseline.Captive, out error) ||
            !RestoreCapturedPartyVisibility(baseline.Captive, out error) ||
            !IsRosterCaptiveBaselineRestored(baseline, out error))
        {
            return RosterCaptiveBaselineResult(baseline, "restore", success: false, reason: error);
        }

        baseline.RestoredPendingVerification = true;
        return RosterCaptiveBaselineResult(baseline, "restore", success: true, reason: null);
    }

    public static string VerifyRosterCaptiveBaselineRestore(List<string> args)
    {
        if (ModInformation.IsClient)
            return RosterCaptiveBaselineFailure("verify-restore", "Command can only be run on the server.");
        if (args.Count != 0)
        {
            return RosterCaptiveBaselineFailure("verify-restore",
                "Usage: coop.debug.siege.defender_roster_fixture_verify_captive_baseline_restore");
        }
        if (rosterCaptiveBaselineFixture == null || !rosterCaptiveBaselineFixture.RestoredPendingVerification)
        {
            return RosterCaptiveBaselineFailure("verify-restore",
                "No restored defender captive baseline is awaiting verification.");
        }

        DefenderRosterCaptiveBaselineFixture baseline = rosterCaptiveBaselineFixture;
        bool restored = IsRosterCaptiveBaselineRestored(baseline, out string error);
        if (restored)
            rosterCaptiveBaselineFixture = null;
        return RosterCaptiveBaselineResult(baseline, "verify-restore", restored, error);
    }

    public static string ObserveRosterFixtureReadiness(List<string> args)
    {
        if (ModInformation.IsClient)
            return RosterFixtureFailure("readiness", "Command can only be run on the server.");
        if (!TryGetExpectedControllerIds(args, out string[] expectedControllerIds, out _))
        {
            return RosterFixtureFailure("readiness",
                "Usage: coop.debug.siege.defender_roster_fixture_readiness <firstControllerId> <secondControllerId>");
        }
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            return RosterFixtureFailure("readiness", "The defender roster fixture services are unavailable.");
        }

        bool campaignPresent = Campaign.Current != null;
        bool lifecycleClear = rosterFixture == null && pendingCapture == null &&
            activeFixture == null && restoredFixture == null;
        bool synchronizedControllers = HasExpectedSynchronizedControllers(playerManager, expectedControllerIds);
        bool observationComplete = true;
        var players = new List<object>(ExpectedPlayerCount);
        foreach (string controllerId in expectedControllerIds)
        {
            Player player = null;
            Hero hero = null;
            MobileParty party = null;
            string partyId = null;
            bool playerResolved = playerManager.TryGetPlayer(controllerId, out player);
            bool heroResolved = playerResolved && objectManager.TryGetObject<Hero>(player.HeroId, out hero);
            bool partyResolved = playerResolved && objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out party);
            bool partyIdentityCurrent = partyResolved && objectManager.TryGetId(party, out partyId) &&
                partyId == player.MobilePartyId;
            if (!playerResolved || !heroResolved || !partyIdentityCurrent || party.Party == null)
            {
                observationComplete = false;
                players.Add(new
                {
                    controllerId,
                    playerResolved,
                    heroResolved,
                    partyResolved,
                    partyIdentityCurrent,
                    partyBaseResolved = partyResolved && party?.Party != null,
                    captureReady = (bool?)null
                });
                continue;
            }

            bool heroIsPrisoner = hero.IsPrisoner;
            bool heroHasCaptor = hero.PartyBelongedToAsPrisoner != null;
            bool heroBelongsToPlayerParty = ReferenceEquals(hero.PartyBelongedTo, party);
            bool heroStateIsActive = hero.HeroState == Hero.CharacterStates.Active;
            bool partyActive = party.IsActive;
            bool partyVisible = party.IsVisible;
            bool visualManagerPresent = MobilePartyVisualManager.Current != null;
            bool partyHasVisual = party.Party.GetPartyVisual() != null;
            bool partyLeaderIsHero = ReferenceEquals(party.LeaderHero, hero);
            bool partyHasMapEvent = party.MapEvent != null;
            bool partyHasBesiegerCamp = party.BesiegerCamp != null;
            bool partyIsTransitioning = party.IsTransitionInProgress;
            bool partyHasArmy = party.Army != null;
            bool partyHasAttachedTo = party.AttachedTo != null;
            bool partyHasAttachedParties = party.AttachedParties?.Count > 0;
            bool partyIsAtSea = party.IsCurrentlyAtSea;
            bool captureReady = DefenderRosterFixtureContract.IsUncapturedPlayerReady(
                heroIsPrisoner,
                heroHasCaptor,
                heroBelongsToPlayerParty,
                heroStateIsActive,
                partyActive,
                partyVisible,
                visualManagerPresent,
                partyHasVisual,
                partyLeaderIsHero,
                partyHasMapEvent,
                partyHasBesiegerCamp,
                partyIsTransitioning,
                partyHasArmy,
                partyHasAttachedTo,
                partyHasAttachedParties,
                partyIsAtSea);
            string[] failedConditions = new[]
            {
                heroIsPrisoner ? "heroIsPrisoner" : null,
                heroHasCaptor ? "heroHasCaptor" : null,
                !heroBelongsToPlayerParty ? "heroBelongsToPlayerParty" : null,
                !heroStateIsActive ? "heroStateIsActive" : null,
                !partyActive ? "partyActive" : null,
                !partyVisible ? "partyVisible" : null,
                visualManagerPresent && !partyHasVisual ? "partyHasVisual" : null,
                !partyLeaderIsHero ? "partyLeaderIsHero" : null,
                partyHasMapEvent ? "partyHasMapEvent" : null,
                partyHasBesiegerCamp ? "partyHasBesiegerCamp" : null,
                partyIsTransitioning ? "partyIsTransitioning" : null,
                partyHasArmy ? "partyHasArmy" : null,
                partyHasAttachedTo ? "partyHasAttachedTo" : null,
                partyHasAttachedParties ? "partyHasAttachedParties" : null,
                partyIsAtSea ? "partyIsAtSea" : null
            }.Where(condition => condition != null).ToArray();
            players.Add(new
            {
                controllerId,
                playerResolved,
                heroResolved,
                partyResolved,
                partyIdentityCurrent,
                partyBaseResolved = true,
                heroId = player.HeroId,
                partyId,
                heroIsPrisoner,
                heroHasCaptor,
                heroBelongsToPlayerParty,
                heroStateIsActive,
                partyActive,
                partyVisible,
                visualManagerPresent,
                partyHasVisual,
                partyLeaderIsHero,
                partyHasMapEvent,
                partyHasBesiegerCamp,
                partyIsTransitioning,
                partyHasArmy,
                partyHasAttachedTo,
                partyHasAttachedParties,
                partyIsAtSea,
                uncapturedGuardApplicable = !heroIsPrisoner && !heroHasCaptor,
                captureReady,
                failedConditions
            });
        }

        return JsonResult(new
        {
            success = true,
            phase = "readiness",
            expectedPlayerCount = ExpectedPlayerCount,
            expectedControllerIds,
            requiresReadinessRecheck = true,
            campaignPresent,
            lifecycleClear,
            synchronizedControllers,
            observationComplete,
            preparedCaptiveBaselineActive = rosterCaptiveBaselineFixture != null,
            preparedCaptiveControllerId = rosterCaptiveBaselineFixture?.Captive.ControllerId,
            capturePreconditionsCurrent = campaignPresent && lifecycleClear &&
                synchronizedControllers && observationComplete,
            players = players.ToArray()
        });
    }

    public static string NormalizeRosterFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return RosterFixtureFailure("normalize", "Command can only be run on the server.");
        if (args.Count != 0)
            return RosterFixtureFailure("normalize",
                "Usage: coop.debug.siege.defender_roster_fixture_normalize");
        if (rosterFixture == null)
            return RosterFixtureFailure("normalize", "No defender roster fixture is captured.");
        if (rosterFixture.IsNormalized && !rosterFixture.NormalizationUnsafe)
        {
            bool normalizedStateCurrent = IsNormalizedRosterCurrent(rosterFixture, out string normalizedStateError);
            if (DefenderRosterFixtureContract.CanReportNormalizedSuccess(
                    rosterFixture.IsNormalized,
                    rosterFixture.NormalizationUnsafe,
                    normalizedStateCurrent))
            {
                return RosterFixtureResult(rosterFixture, "normalize", success: true, reason: null);
            }

            return RosterFixtureResult(
                rosterFixture,
                "normalize",
                success: false,
                reason: "The normalized defender roster is no longer current: " + normalizedStateError);
        }
        if (!DefenderRosterFixtureContract.IsNormalizationRetryable(
                rosterFixture.IsNormalized,
                rosterFixture.NormalizationUnsafe))
        {

            return RosterFixtureResult(
                rosterFixture,
                "normalize",
                success: false,
                reason: "A prior defender roster normalization left an ambiguous state; automatic fixture actions are disabled until the captured roster is resolved.");
        }
        if (!IsRosterCaptureCurrent(rosterFixture, out string error))
            return RosterFixtureResult(rosterFixture, "normalize", success: false, reason: error);

        var normalized = new List<DefenderRosterFixturePlayer>();
        foreach (DefenderRosterFixturePlayer player in rosterFixture.Players.Where(player => player.WasCaptive))
        {
            if (!HasCurrentRosterIdentities(rosterFixture, out string readinessError) ||
                !IsCaptorReadyForRelease(player.CaptorParty))
            {
                string rollbackError = RestoreNormalizedRosterPlayers(rosterFixture, normalized);
                return RosterFixtureResult(rosterFixture, "normalize", success: false,
                    reason: (readinessError ?? "The captured mobile captor is no longer on land.") +
                        (rollbackError == null ? " Prior releases were restored." : " Rollback failed: " + rollbackError));
            }
            try
            {
                RunFixtureCaptivityAction(player, release: true);
            }
            catch (Exception exception)
            {
                bool captured = MatchesCapturedCaptivityBaseline(player);
                bool released = IsReleasedForRestoration(player);
                bool releasedStateRestorable = released &&
                    IsReleasedRosterRestorable(rosterFixture, player, out _);
                if (!captured && !releasedStateRestorable)
                {
                    string ambiguousRollbackError = RestoreNormalizedRosterPlayers(
                        rosterFixture,
                        normalized,
                        preserveUnsafeState: true);
                    rosterFixture.IsNormalized = true;
                    rosterFixture.NormalizationUnsafe = true;
                    return RosterFixtureResult(
                        rosterFixture,
                        "normalize",
                        success: false,
                        reason: ambiguousRollbackError == null
                            ? "The authoritative captivity release for " + player.ControllerId + " threw " +
                              exception.GetType().Name + " and left an ambiguous state; prior released players " +
                              "were restored, but automatic recapture was rejected."
                            : "The authoritative captivity release for " + player.ControllerId + " threw " +
                              exception.GetType().Name + " and left an ambiguous state; rollback also failed: " +
                              ambiguousRollbackError);
                }

                if (releasedStateRestorable)
                    normalized.Add(player);
                string releaseRollbackError = RestoreNormalizedRosterPlayers(rosterFixture, normalized);
                return RosterFixtureResult(
                    rosterFixture,
                    "normalize",
                    success: false,
                    reason: releaseRollbackError == null
                        ? "The authoritative captivity release for " + player.ControllerId + " threw " +
                          exception.GetType().Name + "; the captured baseline was restored."
                        : "The authoritative captivity release for " + player.ControllerId + " threw " +
                          exception.GetType().Name + "; rollback also failed: " + releaseRollbackError);
            }

            if (!HasCurrentRosterRegistryIdentities(rosterFixture, out string identityError))
            {
                normalized.Add(player);
                string rollbackError = RestoreNormalizedRosterPlayers(rosterFixture, normalized);
                return RosterFixtureResult(rosterFixture, "normalize", success: false,
                    reason: identityError + " Rollback failed: " + rollbackError);
            }

            bool releasedForReadiness = IsReleasedForReadiness(player);
            bool releasedForRestoration = IsReleasedForRestoration(player);
            string restorationError = null;
            bool releasedRestorable = releasedForRestoration &&
                IsReleasedRosterRestorable(rosterFixture, player, out restorationError);
            if (DefenderRosterFixtureContract.CanAcceptNormalizedPlayer(
                    releasedForReadiness,
                    releasedRestorable))
            {
                normalized.Add(player);
                continue;
            }

            bool capturedBaseline = MatchesCapturedCaptivityBaseline(player);
            if (!capturedBaseline && !releasedRestorable)
            {
                string ambiguousReadinessRollbackError = RestoreNormalizedRosterPlayers(
                    rosterFixture,
                    normalized,
                    preserveUnsafeState: true);
                return RosterFixtureResult(
                    rosterFixture,
                    "normalize",
                    success: false,
                    reason: ambiguousReadinessRollbackError == null
                        ? "The authoritative captivity release left " + player.ControllerId +
                          " in a state that cannot be restored safely: " + restorationError +
                          " Prior released players were restored, but automatic fixture actions are disabled."
                        : "The authoritative captivity release left " + player.ControllerId +
                          " in a state that cannot be restored safely: " + restorationError +
                          " Rollback also failed: " + ambiguousReadinessRollbackError);
            }

            if (releasedRestorable)
                normalized.Add(player);
            string readinessRollbackError = RestoreNormalizedRosterPlayers(rosterFixture, normalized);
            return RosterFixtureResult(
                rosterFixture,
                "normalize",
                success: false,
                reason: readinessRollbackError == null
                    ? "The authoritative captivity release did not make " + player.ControllerId +
                      " eligible; the captured baseline was restored."
                    : "The authoritative captivity release did not make " + player.ControllerId +
                      " eligible; rollback also failed: " + readinessRollbackError);

        }

        rosterFixture.IsNormalized = true;
        return RosterFixtureResult(rosterFixture, "normalize", success: true, reason: null);
    }

    public static string RestoreRosterFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return RosterFixtureFailure("restore", "Command can only be run on the server.");
        if (args.Count != 0)
            return RosterFixtureFailure("restore",
                "Usage: coop.debug.siege.defender_roster_fixture_restore");
        if (rosterFixture == null)
            return RosterFixtureFailure("restore", "No defender roster fixture is active.");
        if (rosterFixture.NormalizationUnsafe)
        {
            return RosterFixtureResult(
                rosterFixture,
                "restore",
                success: false,
                reason: "The defender roster fixture is in an ambiguous normalization state; automatic restore was rejected.");
        }
        if (!rosterFixture.IsNormalized)
        {
            if (!IsRosterFixtureRestored(rosterFixture, out string unmutatedError))
            {
                return RosterFixtureResult(rosterFixture, "restore", success: false,
                    reason: "The defender roster fixture was not normalized and its captured state changed: " +
                            unmutatedError);
            }

            rosterFixture.RestoredPendingVerification = true;
            return RosterFixtureResult(rosterFixture, "restore", success: true,
                reason: "The defender roster fixture was captured but never normalized.");
        }
        if (pendingCapture != null || activeFixture != null || restoredFixture != null)
        {
            return RosterFixtureResult(rosterFixture, "restore", success: false,
                reason: "Restore the defender siege fixture before restoring the roster fixture.");
        }
        if (!CanRestoreRosterFixture(rosterFixture, out string error))
            return RosterFixtureResult(rosterFixture, "restore", success: false, reason: error);

        foreach (DefenderRosterFixturePlayer player in rosterFixture.Players.Where(player => player.WasCaptive))
        {
            if (!HasCurrentRosterRegistryIdentities(rosterFixture, out error))
                return RosterFixtureResult(rosterFixture, "restore", success: false, reason: error);
            if (MatchesCapturedCaptivityBaseline(player))
            {
                player.RestoreCompleted = true;
                continue;
            }
            if (!TryRestoreCaptivePlayer(rosterFixture, player, out error))
            {
                if (!HasCurrentRosterRegistryIdentities(rosterFixture, out string identityError))
                    return RosterFixtureResult(rosterFixture, "restore", success: false, reason: identityError);
                if (MatchesCapturedCaptivityBaseline(player))
                {
                    player.RestoreCompleted = true;
                    continue;
                }
                bool recapturedSnapshotReplaySafe = IsRecapturedSnapshotReplaySafe(
                    rosterFixture,
                    player,
                    out _);
                bool releasedForRestoration = IsReleasedRosterRestorable(rosterFixture, player, out _);
                if (DefenderRosterFixtureContract.RequiresRestoreFailureEscalation(
                        capturedBaseline: false,
                        recapturedSnapshotReplaySafe: recapturedSnapshotReplaySafe,
                        releasedForRestoration: releasedForRestoration))
                {
                    rosterFixture.NormalizationUnsafe = true;
                    return RosterFixtureResult(
                        rosterFixture,
                        "restore",
                        success: false,
                        reason: error + " The resulting roster state is ambiguous; automatic fixture actions are disabled.");
                }

                return RosterFixtureResult(rosterFixture, "restore", success: false, reason: error);
            }

            player.RestoreCompleted = true;
        }

        if (!HasCurrentRosterRegistryIdentities(rosterFixture, out error))
            return RosterFixtureResult(rosterFixture, "restore", success: false, reason: error);
        rosterFixture.RestoredPendingVerification = true;
        return RosterFixtureResult(rosterFixture, "restore", success: true, reason: null);
    }

    public static string VerifyRosterFixtureRestore(List<string> args)
    {
        if (ModInformation.IsClient)
            return RosterFixtureFailure("verify-restore", "Command can only be run on the server.");
        if (args.Count != 0)
        {
            return RosterFixtureFailure("verify-restore",
                "Usage: coop.debug.siege.defender_roster_fixture_verify_restore");
        }
        if (rosterFixture == null || !rosterFixture.RestoredPendingVerification)
        {
            return RosterFixtureFailure("verify-restore",
                "No restored defender roster fixture is awaiting verification.");
        }

        DefenderRosterFixture fixture = rosterFixture;
        bool restored = IsRosterFixtureRestored(fixture, out string error);
        if (restored)
            rosterFixture = null;
        return RosterFixtureResult(fixture, "verify-restore", restored, error);
    }
#endif

    public static string Stage(List<string> args)
    {
        if (ModInformation.IsClient)
            return Failure("stage", "Command can only be run on the server.");
        if (args.Count != 0)
            return Failure("stage", "Usage: coop.debug.siege.defender_fixture_stage");
        if (activeFixture != null)
            return Failure("stage", "The defender fixture is already active; restore it before staging again.");
        if (pendingCapture == null)
            return Failure("stage", "No defender fixture capture is waiting to be staged.");
        if (!IsCaptureCurrent(pendingCapture))
            return Failure("stage", "The captured defender fixture changed before staging.");
        if (!CanStage(pendingCapture, out string error))
            return FixtureResult(pendingCapture, "stage", success: false, reason: error);

        DefenderSiegeFixture fixture = pendingCapture;
        pendingCapture = null;
        activeFixture = fixture;
        try
        {
            foreach (DefenderFixtureParty party in fixture.Parties)
            {
                if (!StagePartyInsideSettlement(fixture, party.Party))
                    return FixtureResult(fixture, "stage", success: false, reason: "The captured defender identity changed.");
            }

            if (!IsStaged(fixture))
                return FixtureResult(fixture, "stage", success: false,
                    reason: "The defender parties did not reach the required inside-settlement state.");

            fixture.IsStaged = true;
            return FixtureResult(fixture, "stage", success: true, reason: null);
        }
        catch (Exception exception)
        {
            return FixtureResult(fixture, "stage", success: false,
                reason: "Staging threw " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    public static string Restore(List<string> args)
    {
        if (ModInformation.IsClient)
            return Failure("restore", "Command can only be run on the server.");
        if (args.Count != 0)
            return Failure("restore", "Usage: coop.debug.siege.defender_fixture_restore");
        if (restoredFixture != null)
            return Failure("restore", "A defender fixture is already awaiting restore verification.");
        if (activeFixture == null)
        {
            if (pendingCapture == null)
                return Failure("restore", "No defender fixture is active.");
            if (!IsCaptureCurrent(pendingCapture))
                return Failure("restore", "The un-staged defender fixture capture is no longer clean.");

            restoredFixture = pendingCapture;
            pendingCapture = null;
            return FixtureResult(restoredFixture, "restore", success: true,
                reason: "The captured fixture was never mutated.");
        }
        if (!CanRestore(activeFixture, out string error))
            return FixtureResult(activeFixture, "restore", success: false, reason: error);
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return FixtureResult(activeFixture, "restore", success: false,
                reason: "The mobile-party behavior snapshot service is unavailable.");

        DefenderSiegeFixture fixture = activeFixture;
        try
        {
            foreach (DefenderFixtureParty party in fixture.Parties)
            {
                if (!RestoreParty(fixture, party, behaviorSnapshot))
                {
                    return FixtureResult(fixture, "restore", success: false,
                        reason: "The original behavior could not be restored for " + party.ControllerId + ".");
                }
            }

            if (!IsRestored(fixture))
                return FixtureResult(fixture, "restore", success: false,
                    reason: "The defender fixture did not return to its captured state.");

            activeFixture = null;
            restoredFixture = fixture;
            return FixtureResult(fixture, "restore", success: true, reason: null);
        }
        catch (Exception exception)
        {
            return FixtureResult(fixture, "restore", success: false,
                reason: "Restore threw " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    public static string VerifyRestore(List<string> args)
    {
        if (ModInformation.IsClient)
            return Failure("verify-restore", "Command can only be run on the server.");
        if (args.Count != 0)
            return Failure("verify-restore", "Usage: coop.debug.siege.defender_fixture_verify_restore");
        if (restoredFixture == null)
            return Failure("verify-restore", "No restored defender fixture is awaiting verification.");

        DefenderSiegeFixture fixture = restoredFixture;
        bool restored = IsRestored(fixture);
        if (restored)
            restoredFixture = null;
        return FixtureResult(fixture, "verify-restore", restored,
            restored ? null : "The defender fixture no longer matches the captured state.");
    }

    public static string PreAssaultAcknowledgement(List<string> args)
    {
        if (!TryGetExpectedControllerIds(args, out string[] expectedControllerIds, out string error))
            return PreAssaultResult(expectedControllerIds: null, success: false, reason: error,
                settlement: null, playerManager: null);
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            return PreAssaultResult(expectedControllerIds, success: false,
                reason: "The player or object registry is unavailable.", settlement: null,
                playerManager: null);
        }
        if (!objectManager.TryGetObject<Settlement>(SettlementId, out var settlement))
        {
            return PreAssaultResult(expectedControllerIds, success: false,
                reason: "Odrysa Castle is unavailable from the object registry.", settlement: null,
                playerManager: playerManager);
        }

        Player[] expectedPlayers = expectedControllerIds
            .Select(controllerId => playerManager.TryGetPlayer(controllerId, out Player player)
                ? player
                : null)
            .ToArray();
        MobileParty[] expectedParties = expectedPlayers
            .Select(player => player != null &&
                objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out MobileParty party)
                    ? party
                : null)
            .ToArray();
        bool expectedPlayersResolved = expectedPlayers.All(player => player != null) &&
            expectedParties.All(party => party != null);
        string[] registeredControllerIds = playerManager.Players
            .Select(player => player.ControllerId)
            .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
            .ToArray();

        bool isServer = ModInformation.IsServer;
        int connectedPlayerCount;
        string[] connectedControllerIds;
        string connectedControllerIdsSource;
        bool connectedControllersExact;
        if (isServer)
        {
            connectedControllerIds = playerManager.Players
                .Where(playerManager.IsConnected)
                .Select(player => player.ControllerId)
                .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
                .ToArray();
            connectedPlayerCount = connectedControllerIds.Length;
            connectedControllerIdsSource = "server-peer-registry";
            connectedControllersExact = DefenderSiegeFixtureContract.HasExactControllerIds(
                connectedControllerIds, expectedControllerIds);
        }
        else if (ContainerProvider.TryResolve<IConnectedPlayerCountService>(out var connectedPlayerCountService))
        {
            connectedPlayerCount = connectedPlayerCountService.ConnectedPlayers;
            connectedControllerIds = Array.Empty<string>();
            connectedControllerIdsSource = "unavailable-on-client";
            connectedControllersExact = false;
        }
        else
        {
            connectedPlayerCount = -1;
            connectedControllerIds = Array.Empty<string>();
            connectedControllerIdsSource = "unavailable";
            connectedControllersExact = false;
        }

        string localControllerId = null;
        MobileParty localParty = null;
        if (!isServer)
        {
            ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
            localControllerId = controllerIdProvider?.ControllerId;
            localParty = MobileParty.MainParty;
        }
        string localPartyId = localParty != null && objectManager.TryGetId(localParty, out string resolvedLocalPartyId)
            ? resolvedLocalPartyId
            : null;
        bool localPartyReady = isServer || IsExpectedLocalParty(
            localControllerId, localParty, expectedControllerIds, expectedPlayers, expectedParties);
        bool noMapEvent = settlement.Party.MapEvent == null &&
            expectedParties.All(party => party?.MapEvent == null);
        bool noBesiegerCamp = settlement.SiegeEvent?.BesiegerCamp == null &&
            expectedParties.All(party => party?.BesiegerCamp == null);
        bool insideSettlement = expectedParties.All(party => party?.CurrentSettlement == settlement);
        bool fixtureStaged = !isServer || activeFixture != null &&
            activeFixture.HasExpectedControllers(expectedControllerIds) && IsStaged(activeFixture);
        bool connectionReady = DefenderSiegeFixtureContract.IsConnectionReadinessSatisfied(
            isServer,
            connectedPlayerCount,
            connectedControllersExact);
        bool success = DefenderSiegeFixtureContract.IsPreAssaultReady(
            fixtureStaged,
            connectionReady,
            expectedPlayersResolved,
            noMapEvent,
            noBesiegerCamp,
            insideSettlement,
            localPartyReady);

        string reason = success
            ? null
            : "The defender pre-assault topology is not ready.";
        return JsonResult(new
        {
            success,
            reason,
            role = isServer ? "server" : "client",
            settlementId = settlement.StringId,
            expectedPlayerCount = ExpectedPlayerCount,
            expectedControllerIds,
            registeredPlayerCount = registeredControllerIds.Length,
            registeredControllerIds,
            connectedPlayerCount,
            connectedControllerIds,
            connectedControllerIdsSource,
            connectedIdentityAuthority = isServer ? "server-peer-registry" : "server-only",
            localControllerId,
            localPartyId,
            localPartyStringId = localParty?.StringId,
            noMapEvent,
            noBesiegerCamp,
            insideSettlement,
            fixtureStaged = isServer ? fixtureStaged : (bool?)null,
            parties = expectedControllerIds.Select((controllerId, index) => new
            {
                controllerId,
                partyId = expectedPlayers[index]?.MobilePartyId,
                partyStringId = expectedParties[index]?.StringId,
                currentSettlementId = expectedParties[index]?.CurrentSettlement?.StringId,
                hasMapEvent = expectedParties[index]?.MapEvent != null,
                hasBesiegerCamp = expectedParties[index]?.BesiegerCamp != null
            }).ToArray()
        });
    }

    private static bool HasExpectedConnectedControllers(
        IPlayerManager playerManager,
        IEnumerable<string> expectedControllerIds) =>
        DefenderSiegeFixtureContract.HasExactControllerIds(
            playerManager.Players
                .Where(playerManager.IsConnected)
                .Select(player => player.ControllerId)
                .OrderBy(controllerId => controllerId, StringComparer.Ordinal),
            expectedControllerIds);

    private static bool HasExpectedSynchronizedControllers(
        IPlayerManager playerManager,
        IEnumerable<string> expectedControllerIds)
    {
        if (!HasExpectedConnectedControllers(playerManager, expectedControllerIds) ||
            !ContainerProvider.TryResolve<ICampaignSynchronization>(out var synchronization))
            return false;

        return expectedControllerIds.All(controllerId =>
            playerManager.TryGetPlayer(controllerId, out Player player) &&
            playerManager.TryGetPeer(controllerId, out var peer) &&
            peer != null && peer.ConnectionState == LiteNetLib.ConnectionState.Connected &&
            playerManager.TryGetPlayer(peer, out Player peerPlayer) &&
            ReferenceEquals(player, peerPlayer) &&
            synchronization.HasCompletedCampaignSynchronization(peer));
    }

    private static bool TryCreateFixture(
        string[] expectedControllerIds,
        out DefenderSiegeFixture fixture,
        out string error)
    {
        fixture = null;
        error = null;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !ContainerProvider.TryResolve<IDefenderFixtureBehaviorIdentity>(out var behaviorIdentity))
        {
            error = "The defender fixture services are unavailable.";
            return false;
        }
        if (Campaign.Current == null ||
            !objectManager.TryGetObject<Settlement>(SettlementId, out var settlement) ||
            !settlement.IsFortification)
        {
            error = "Odrysa Castle is not available as a fortification.";
            return false;
        }
        if (settlement.Party.MapEvent != null || settlement.SiegeEvent != null)
        {
            error = "Odrysa Castle already has a map event or besieger camp.";
            return false;
        }

        if (!HasExpectedSynchronizedControllers(playerManager, expectedControllerIds))
        {
            error = "Exactly the two expected player controllers must finish campaign synchronization before capture.";
            return false;
        }

        var parties = new List<DefenderFixtureParty>(ExpectedPlayerCount);
        foreach (string controllerId in expectedControllerIds)
        {
            if (!playerManager.TryGetPlayer(controllerId, out Player player) ||
                !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out MobileParty party))
            {
                error = "The player party for " + controllerId + " is unavailable.";
                return false;
            }
            if (!objectManager.TryGetId(party, out string partyId) || partyId != player.MobilePartyId)
            {
                error = "The player party for " + controllerId + " is not registered under its player identity.";
                return false;
            }
            if (WouldUpdateOwnerVisit(party, settlement))
            {
                error = "The player party for " + controllerId +
                    " would mutate Odrysa Castle's owner visit timestamp.";
                return false;
            }
            if (party.CurrentSettlement != null &&
                WouldUpdateOwnerVisit(party, party.CurrentSettlement))
            {
                error = "The player party for " + controllerId +
                    " would mutate its original settlement's owner visit timestamp.";
                return false;
            }
            DefenderFixturePartyState state = ReadPartyState(partyId, party);
            if (!DefenderSiegeFixtureContract.IsCleanForCapture(state))
            {
                error = "The player party for " + controllerId + " is not clean for defender staging.";
                return false;
            }
            if (!behaviorSnapshot.TryCreate(party, out PartyBehaviorUpdateData behavior))
            {
                error = "The original movement state for " + controllerId + " could not be captured.";
                return false;
            }

            string originalSettlementId = null;
            string lastVisitedSettlementId = null;
            if ((party.CurrentSettlement != null &&
                    !objectManager.TryGetId(party.CurrentSettlement, out originalSettlementId)) ||
                (party.LastVisitedSettlement != null &&
                    !objectManager.TryGetId(party.LastVisitedSettlement, out lastVisitedSettlementId)))
            {
                error = "The original settlements for " + controllerId + " are not registered.";
                return false;
            }
            parties.Add(new DefenderFixtureParty(
                player,
                partyId,
                party,
                party.CurrentSettlement,
                party.LastVisitedSettlement,
                originalSettlementId,
                lastVisitedSettlementId,
                party.Position,
                party.Bearing,
                behavior,
                behaviorIdentity.Capture(party, behavior),
                state));
        }

        fixture = new DefenderSiegeFixture(expectedControllerIds, settlement, parties.ToArray());
        if (HasCurrentFixtureIdentities(fixture)) return true;
        fixture = null;
        error = "The captured defender movement references are unavailable.";
        return false;
    }

#if DEBUG
    private static bool TryCreateRosterFixture(
        string[] expectedControllerIds,
        bool requireCaptive,
        out DefenderRosterFixture fixture,
        out string error)
    {
        fixture = null;
        error = null;
        if (Campaign.Current == null)
        {
            error = "A campaign is required before roster capture.";
            return false;
        }
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
        {
            error = "The defender roster fixture services are unavailable.";
            return false;
        }

        if (!HasExpectedSynchronizedControllers(playerManager, expectedControllerIds))
        {
            error = "Exactly the two expected player controllers must be connected and campaign-synchronized before roster capture.";
            return false;
        }

        DefenderRosterCaptiveBaselineFixture preparedBaseline = rosterCaptiveBaselineFixture;
        if (preparedBaseline != null)
        {
            if (!preparedBaseline.HasExpectedControllers(expectedControllerIds) ||
                !IsPreparedRosterCaptiveBaselineCurrent(preparedBaseline, out error))
            {
                if (error == null)
                    error = "The prepared captive baseline is stale or belongs to other controllers.";
                return false;
            }
        }

        var players = new List<DefenderRosterFixturePlayer>(ExpectedPlayerCount);
        foreach (string controllerId in expectedControllerIds)
        {
            if (!playerManager.TryGetPlayer(controllerId, out Player player) ||
                !objectManager.TryGetObject<Hero>(player.HeroId, out Hero hero) ||
                !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out MobileParty party) ||
                !objectManager.TryGetId(party, out string partyId) || partyId != player.MobilePartyId)
            {
                error = "The registered player, hero, or party for " + controllerId + " is unavailable.";
                return false;
            }

            bool wasCaptive = hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null;
            if (wasCaptive && (!hero.IsPrisoner || hero.PartyBelongedToAsPrisoner == null))
            {
                error = "The captivity state for " + controllerId + " is ambiguous.";
                return false;
            }

            PartyBase captorParty = hero.PartyBelongedToAsPrisoner;
            bool hasVisual = party.Party.GetPartyVisual() != null;
            DefenderFixturePartyState partyState = ReadPartyState(partyId, party);
            if (wasCaptive)
            {
                if (!IsCaptorReadyForRelease(captorParty))
                {
                    error = "The mobile captor for " + controllerId + " must be on land before roster capture.";
                    return false;
                }
                if (!TryGetCaptorHeroPrisonerElement(captorParty, hero, out TroopRosterElement captorHeroElement))
                {
                    error = "The captive player hero for " + controllerId +
                        " is absent from its captor's prisoner roster.";
                    return false;
                }

                if (!DefenderRosterFixtureContract.IsCaptiveBaselineRestorable(
                        hero.IsPrisoner,
                        hero.PartyBelongedToAsPrisoner != null,
                        captorParty.IsActive,
                        hero.PartyBelongedTo != null,
                        hero.HeroState == Hero.CharacterStates.Prisoner,
                        captorHeroElement.Number,
                        captorHeroElement.WoundedNumber,
                        captorHeroElement.Xp,
                        party.IsActive,
                        party.IsVisible,
                        hasVisual,
                        party.LeaderHero != null,
                        party.MemberRoster.GetTroopCount(hero.CharacterObject),
                        party.MemberRoster.TotalManCount,
                        party.PrisonRoster.TotalManCount,
                        party.CurrentSettlement != null,
                        party.MapEvent != null,
                        party.BesiegerCamp != null,
                        party.IsTransitionInProgress,
                        party.Army != null,
                        party.AttachedTo != null,
                        party.AttachedParties?.Count > 0,
                        party.IsCurrentlyAtSea,
                        hero.StayingInSettlement != null))
                {
                    error = "The captive player party for " + controllerId +
                        " is not in a normal restorable captivity state.";
                    return false;
                }
                if (!behaviorSnapshot.TryCreate(party, out PartyBehaviorUpdateData behavior))
                {
                    error = "The original movement state for captive player " + controllerId +
                        " could not be captured.";
                    return false;
                }

                AttackProtectionSnapshot[] protections = CaptureAttackProtections(party);
                FactionAttackProtectionSnapshot[] factionProtections = CaptureFactionAttackProtections(party);
                if (protections.Any(protection =>
                        ReferenceEquals(protection.AttackerParty, captorParty.MobileParty) &&
                        ReferenceEquals(protection.TargetParty, party)))
                {
                    error = "The captive player party for " + controllerId +
                        " already has a former-captor attack protection.";
                    return false;
                }

                players.Add(new DefenderRosterFixturePlayer(
                    controllerId,
                    player,
                    hero,
                    partyId,
                    party,
                    captorParty,
                    hero.PartyBelongedTo,
                    party.LeaderHero,
                    partyState,
                    party.LastVisitedSettlement,
                    party.Bearing,
                    behavior,
                    hero.HeroState,
                    hero.CaptivityStartTime,
                    party._ignoredUntilTime,
                    party.IsVisible,
                    party.IsInspected,
                    hasVisual,
                    party.MemberRoster.GetTroopCount(hero.CharacterObject),
                    party.MemberRoster.TotalManCount,
                    party.PrisonRoster.TotalManCount,
                    protections,
                    factionProtections,
                    wasCaptive: true,
                    ownedCaptiveDeltaHero: null));
                continue;
            }

            if (!DefenderRosterFixtureContract.IsUncapturedPlayerReady(
                    hero.IsPrisoner,
                    hero.PartyBelongedToAsPrisoner != null,
                    ReferenceEquals(hero.PartyBelongedTo, party),
                    hero.HeroState == Hero.CharacterStates.Active,
                    party.IsActive,
                    party.IsVisible,
                    MobilePartyVisualManager.Current != null,
                    party.Party.GetPartyVisual() != null,
                    ReferenceEquals(party.LeaderHero, hero),
                    party.MapEvent != null,
                    party.BesiegerCamp != null,
                    party.IsTransitionInProgress,
                    party.Army != null,
                    party.AttachedTo != null,
                    party.AttachedParties?.Count > 0,
                    party.IsCurrentlyAtSea))
            {
                error = "The non-captive player party for " + controllerId +
                    " is not ready for defender roster capture.";
                return false;
            }

            PartyBehaviorUpdateData nonCaptiveBehavior = default;
            if (!requireCaptive && !behaviorSnapshot.TryCreate(party, out nonCaptiveBehavior))
            {
                error = "The original movement state for player " + controllerId +
                    " could not be captured for a reversible captive baseline.";
                return false;
            }

            Hero ownedCaptiveDeltaHero = preparedBaseline != null &&
                ReferenceEquals(player, preparedBaseline.Captor.Player)
                ? preparedBaseline.Captive.Hero
                : null;
            var fixturePlayer = new DefenderRosterFixturePlayer(
                controllerId,
                player,
                hero,
                partyId,
                party,
                null,
                hero.PartyBelongedTo,
                party.LeaderHero,
                partyState,
                party.LastVisitedSettlement,
                party.Bearing,
                nonCaptiveBehavior,
                hero.HeroState,
                hero.CaptivityStartTime,
                party._ignoredUntilTime,
                party.IsVisible,
                party.IsInspected,
                hasVisual,
                party.MemberRoster.GetTroopCount(hero.CharacterObject),
                party.MemberRoster.TotalManCount,
                party.PrisonRoster.TotalManCount,
                CaptureAttackProtections(party),
                CaptureFactionAttackProtections(party),
                wasCaptive: false,
                ownedCaptiveDeltaHero: ownedCaptiveDeltaHero);
            if (ownedCaptiveDeltaHero != null &&
                !MatchesUncapturedBaselineWithOwnedCaptive(fixturePlayer, ownedCaptiveDeltaHero))
            {
                error = "The selected captive baseline no longer owns exactly its authorized prisoner delta.";
                return false;
            }

            players.Add(fixturePlayer);
        }

        if (requireCaptive && !players.Any(player => player.WasCaptive))
        {
            error = "The selected roster has no captive player to normalize.";
            return false;
        }
        if (preparedBaseline != null &&
            (!players.Any(player => player.WasCaptive &&
                                    ReferenceEquals(player.Hero, preparedBaseline.Captive.Hero)) ||
             !players.Any(player => ReferenceEquals(player.Player, preparedBaseline.Captor.Player) &&
                                    ReferenceEquals(player.OwnedCaptiveDeltaHero, preparedBaseline.Captive.Hero))))
        {
            error = "The prepared captive baseline no longer maps to the selected player roster.";
            return false;
        }

        fixture = new DefenderRosterFixture(expectedControllerIds, players.ToArray(), behaviorSnapshot);
        return true;
    }

    private static bool CanPrepareRosterCaptiveBaseline(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer captive,
        DefenderRosterFixturePlayer captor,
        out string error)
    {
        if (!HasCurrentRosterIdentities(fixture, out error))
            return false;
        if (fixture.Players.Any(player => player.WasCaptive || !MatchesUncapturedBaseline(player)))
        {
            error = "Every selected player must be in the exact uncaptured baseline before captive setup.";
            return false;
        }
        if (ReferenceEquals(captive, captor) || captor.Party?.Party == null ||
            !IsCaptorReadyForRelease(captor.Party.Party))
        {
            error = "The selected captive and its mobile captor must be distinct and on land.";
            return false;
        }
        if (captive.OriginalHeroMemberCount != 1 || captive.OriginalMemberCount != 1 ||
            captive.OriginalPrisonerCount != 0 || captor.OriginalPrisonerCount != 0)
        {
            error = "The selected captive baseline must contain only its player hero and no prisoners.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsPreparedRosterCaptiveBaselineCurrent(
        DefenderRosterCaptiveBaselineFixture baseline,
        out string error)
    {
        if (!HasCurrentRosterIdentities(baseline.Fixture, out error))
            return false;
        if (!ReferenceEquals(baseline.Captive.CaptorParty, baseline.Captor.Party.Party) ||
            !MatchesNormalRecapture(baseline.Captive))
        {
            error = "The prepared captive player state is no longer current.";
            return false;
        }
        if (!MatchesUncapturedBaselineWithOwnedCaptiveDelta(baseline.Captor, baseline.Captive.Hero))
        {
            error = "The prepared captor player no longer owns exactly the authorized captive delta.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsRosterCaptiveBaselineRestored(
        DefenderRosterCaptiveBaselineFixture baseline,
        out string error)
    {
        if (!HasCurrentRosterIdentities(baseline.Fixture, out error))
            return false;
        foreach (DefenderRosterFixturePlayer player in baseline.Fixture.Players)
        {
            if (MatchesUncapturedBaseline(player))
                continue;

            error = "The pre-setup player baseline for " + player.ControllerId + " was not restored.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsCaptorReadyForRelease(PartyBase captor) =>
        captor != null && (!captor.IsMobile ||
            captor.MobileParty != null && !captor.MobileParty.IsCurrentlyAtSea && captor.Position.IsOnLand);

    private static bool IsRosterCaptureCurrent(DefenderRosterFixture fixture, out string error)
    {
        if (!HasCurrentRosterIdentities(fixture, out error))
            return false;

        foreach (DefenderRosterFixturePlayer player in fixture.Players)
        {
            if (player.WasCaptive && (!IsCaptorReadyForRelease(player.CaptorParty) ||
                !MatchesCapturedCaptivityBaseline(player)))
            {
                error = "The captive baseline for " + player.ControllerId + " changed before normalization.";
                return false;
            }
            if (!player.WasCaptive && !MatchesCapturedRosterNonCaptiveBaseline(player))
            {
                error = "The non-captive baseline for " + player.ControllerId + " changed before normalization.";
                return false;
            }
        }

        return true;
    }

    private static bool IsNormalizedRosterCurrent(DefenderRosterFixture fixture, out string error)
    {
        if (!HasCurrentRosterIdentities(fixture, out error))
            return false;

        foreach (DefenderRosterFixturePlayer player in fixture.Players)
        {
            if (player.WasCaptive && !IsReleasedRosterRestorable(fixture, player, out error))
            {
                return false;
            }
            if (!player.WasCaptive && !MatchesNormalizedNonCaptiveBaseline(fixture, player))
            {
                error = "The non-captive baseline for " + player.ControllerId + " changed during normalization.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool HasCurrentRosterIdentities(DefenderRosterFixture fixture, out string error)
    {
        if (!HasCurrentRosterRegistryIdentities(fixture, out error))
            return false;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !HasExpectedSynchronizedControllers(playerManager, fixture.ExpectedControllerIds))
        {
            error = "The captured defender roster no longer has exactly the expected connected, campaign-synchronized players.";
            return false;
        }

        return true;
    }

    private static bool HasCurrentRosterRegistryIdentities(DefenderRosterFixture fixture, out string error)
    {
        error = null;
        if (fixture.CapturedCampaign == null || !ReferenceEquals(Campaign.Current, fixture.CapturedCampaign))
        {
            error = "The captured defender roster belongs to a different campaign.";
            return false;
        }
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            error = "The defender roster fixture services are unavailable.";
            return false;
        }
        foreach (DefenderRosterFixturePlayer player in fixture.Players)
        {
            if (!playerManager.TryGetPlayer(player.ControllerId, out Player currentPlayer) ||
                !ReferenceEquals(currentPlayer, player.Player) ||
                !objectManager.TryGetObject<Hero>(currentPlayer.HeroId, out Hero currentHero) ||
                !ReferenceEquals(currentHero, player.Hero) ||
                !objectManager.TryGetObject<MobileParty>(currentPlayer.MobilePartyId, out MobileParty currentParty) ||
                !ReferenceEquals(currentParty, player.Party))
            {
                error = "The captured player identity for " + player.ControllerId + " changed.";
                return false;
            }
        }

        return true;
    }

    private static bool CanRestoreRosterFixture(DefenderRosterFixture fixture, out string error)
    {
        if (!HasCurrentRosterRegistryIdentities(fixture, out error))
            return false;
        foreach (DefenderRosterFixturePlayer player in fixture.Players.Where(player => player.WasCaptive))
        {
            bool capturedBaseline = MatchesCapturedCaptivityBaseline(player);
            bool recapturedSnapshotReplaySafe = IsRecapturedSnapshotReplaySafe(
                fixture,
                player,
                out string recapturedError);
            bool releasedForRestoration = IsReleasedRosterRestorable(fixture, player, out string releasedError);
            if (!DefenderRosterFixtureContract.CanRestoreCapturedPlayer(
                    capturedBaseline,
                    recapturedSnapshotReplaySafe,
                    releasedForRestoration))
            {
                error = MatchesNormalRecapture(player) ? recapturedError : releasedError;
                return false;
            }
            if (capturedBaseline || recapturedSnapshotReplaySafe)
                continue;
        }

        foreach (DefenderRosterFixturePlayer player in fixture.Players.Where(player => !player.WasCaptive))
        {
            if (MatchesRestorableNonCaptiveBaseline(fixture, player))
                continue;

            error = "The non-captive player baseline for " + player.ControllerId + " changed.";
            return false;
        }

        return true;
    }

    private static bool IsReleasedRosterRestorable(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer player,
        out string error)
    {
        bool releasedForRestoration = IsReleasedForRestoration(player);
        bool behaviorRestorable = releasedForRestoration &&
            fixture.BehaviorSnapshot.CanApply(player.Party, player.OriginalBehavior);
        bool attackProtectionsCurrent = releasedForRestoration &&
            HasOnlyCapturedAndReleaseAttackProtections(player);
        if (DefenderRosterFixtureContract.IsReleasedRosterRestorable(
                releasedForRestoration,
                behaviorRestorable,
                attackProtectionsCurrent))
        {
            error = null;
            return true;
        }

        if (!releasedForRestoration)
        {
            error = "The released player party for " + player.ControllerId +
                " is not safe to return through the authoritative captivity action.";
            return false;
        }
        if (!behaviorRestorable)
        {
            error = "The original movement state for " + player.ControllerId +
                " cannot be restored safely.";
            return false;
        }

        error = "The former-captor attack protections for " + player.ControllerId +
            " changed during the fixture.";
        return false;
    }

    private static bool IsRecapturedSnapshotReplaySafe(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer player,
        out string error)
    {
        bool normallyRecaptured = MatchesNormalRecapture(player);
        bool behaviorRestorable = normallyRecaptured &&
            fixture.BehaviorSnapshot.CanApply(player.Party, player.OriginalBehavior);
        bool attackProtectionsCurrent = normallyRecaptured &&
            HasOnlyCapturedAndReleaseAttackProtections(player);
        if (DefenderRosterFixtureContract.IsRecapturedSnapshotReplaySafe(
                normallyRecaptured,
                behaviorRestorable,
                attackProtectionsCurrent))
        {
            error = null;
            return true;
        }

        if (!normallyRecaptured)
        {
            error = "The authoritative captivity restore did not recreate the captured state for " +
                player.ControllerId + ".";
            return false;
        }
        if (!behaviorRestorable)
        {
            error = "The original movement state for " + player.ControllerId +
                " cannot be restored safely.";
            return false;
        }

        error = "The former-captor attack protections for " + player.ControllerId +
            " changed during the fixture.";
        return false;
    }

    private static bool IsReleasedForReadiness(DefenderRosterFixturePlayer player) =>
        DefenderRosterFixtureContract.IsReleasedForReadiness(
            player.Hero.IsPrisoner,
            player.Hero.PartyBelongedToAsPrisoner != null,
            ReferenceEquals(player.Hero.PartyBelongedTo, player.Party),
            player.Hero.HeroState == Hero.CharacterStates.Active,
            player.Party.IsActive,
            player.Party.IsVisible,
            MobilePartyVisualManager.Current != null,
            player.Party.Party.GetPartyVisual() != null,
            ReferenceEquals(player.Party.LeaderHero, player.Hero),
            player.Party.MemberRoster.GetTroopCount(player.Hero.CharacterObject));

    private static bool IsReleasedForRestoration(DefenderRosterFixturePlayer player) =>
        DefenderRosterFixtureContract.IsReleasedForRestoration(
            player.Hero.IsPrisoner,
            player.Hero.PartyBelongedToAsPrisoner != null,
            ReferenceEquals(player.Hero.PartyBelongedTo, player.Party),
            player.Hero.HeroState == Hero.CharacterStates.Active,
            player.Party.IsActive,
            player.Party.IsVisible,
            MobilePartyVisualManager.Current != null,
            player.Party.Party.GetPartyVisual() != null,
            ReferenceEquals(player.Party.LeaderHero, player.Hero),
            player.Party.CurrentSettlement != null,
            player.Party.MapEvent != null,
            player.Party.BesiegerCamp != null,
            player.Party.IsTransitionInProgress,
            player.Party.Army != null,
            player.Party.AttachedTo != null,
            player.Party.AttachedParties?.Count > 0,
            player.Party.IsCurrentlyAtSea,
            player.Party.MemberRoster.GetTroopCount(player.Hero.CharacterObject),
            player.Party.MemberRoster.TotalManCount,
            player.Party.PrisonRoster.TotalManCount,
            player.CaptorParty.IsActive,
            player.CaptorParty.PrisonRoster.GetTroopCount(player.Hero.CharacterObject),
            playerDisconnected: IsDisconnectedRosterPlayer(player));

    private static bool IsDisconnectedRosterPlayer(DefenderRosterFixturePlayer player) =>
        ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) &&
        playerManager.TryGetPlayer(player.ControllerId, out Player currentPlayer) &&
        ReferenceEquals(currentPlayer, player.Player) && !playerManager.IsConnected(currentPlayer);

    private static string RestoreNormalizedRosterPlayers(
        DefenderRosterFixture fixture,
        IEnumerable<DefenderRosterFixturePlayer> normalizedPlayers,
        bool preserveUnsafeState = false)
    {
        var errors = new List<string>();
        bool normalizationUnsafe = preserveUnsafeState;
        foreach (DefenderRosterFixturePlayer player in normalizedPlayers.Reverse())
        {
            if (!TryRestoreCaptivePlayer(fixture, player, out string error))
            {
                errors.Add(error);
                bool recapturedSnapshotReplaySafe = IsRecapturedSnapshotReplaySafe(fixture, player, out _);
                bool releasedForRestoration = IsReleasedRosterRestorable(fixture, player, out _);
                if (!DefenderRosterFixtureContract.CanRestoreCapturedPlayer(
                        capturedBaseline: false,
                        recapturedSnapshotReplaySafe: recapturedSnapshotReplaySafe,
                        releasedForRestoration: releasedForRestoration))
                {
                    normalizationUnsafe = true;
                }
            }
        }

        fixture.IsNormalized = preserveUnsafeState || errors.Count > 0;
        fixture.NormalizationUnsafe = normalizationUnsafe;
        return errors.Count == 0 ? null : string.Join(" ", errors);
    }

    private static bool TryRestoreCaptivePlayer(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer player,
        out string error)
    {
        if (!HasCurrentRosterRegistryIdentities(fixture, out error))
            return false;
        if (MatchesCapturedCaptivityBaseline(player))
            return true;

        if (!IsRecapturedSnapshotReplaySafe(fixture, player, out string recapturedError))
        {
            if (!IsReleasedRosterRestorable(fixture, player, out string releasedError))
            {
                error = MatchesNormalRecapture(player) ? recapturedError : releasedError;
                return false;
            }

            try
            {
                RunFixtureCaptivityAction(player, release: false);
            }
            catch (Exception exception)
            {
                error = "The authoritative captivity restore for " + player.ControllerId + " threw " +
                    exception.GetType().Name + ": " + exception.Message;
                return false;
            }

            if (!HasCurrentRosterRegistryIdentities(fixture, out error))
                return false;
            if (!RestoreCapturedPartyVisibility(player, out error) &&
                !IsRecapturedSnapshotReplaySafe(fixture, player, out _))
            {
                return false;
            }

            if (!IsRecapturedSnapshotReplaySafe(fixture, player, out error))
                return false;
        }

        if (!TryRestoreRecapturedPartySnapshot(fixture, player, out error))
            return false;
        return HasCurrentRosterRegistryIdentities(fixture, out error);
    }

    private static void RunFixtureCaptivityAction(DefenderRosterFixturePlayer player, bool release)
    {
        DefenderRosterFixturePlayer previousPlayer = captivityLogPlayer;
        bool previousRelease = captivityLogRelease;
        captivityLogPlayer = player;
        captivityLogRelease = release;
        try
        {
            if (!ContainerProvider.TryResolve<IDefenderFixtureCaptivityActions>(out var actions))
                throw new InvalidOperationException("The fixture captivity actions are unavailable.");
            if (release)
                actions.Release(player.Hero);
            else
                actions.Recapture(player.CaptorParty, player.Hero);
        }
        finally
        {
            captivityLogPlayer = previousPlayer;
            captivityLogRelease = previousRelease;
        }
    }

    private static bool IsFixtureCaptivityLog(Hero hero, PartyBase party, bool release) =>
        ModInformation.IsServer && captivityLogPlayer != null && captivityLogRelease == release &&
        ReferenceEquals(captivityLogPlayer.Hero, hero) && ReferenceEquals(captivityLogPlayer.CaptorParty, party);

    // The fixture keeps authoritative actions live while excluding their temporary history entries.
    [HarmonyPatch(typeof(DefaultLogsCampaignBehavior), nameof(DefaultLogsCampaignBehavior.OnPrisonerTaken))]
    private static class FixturePrisonerTakenLogPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(PartyBase party, Hero hero) =>
            !IsFixtureCaptivityLog(hero, party, release: false);
    }

    [HarmonyPatch(typeof(DefaultLogsCampaignBehavior), nameof(DefaultLogsCampaignBehavior.OnHeroPrisonerReleased))]
    private static class FixturePrisonerReleasedLogPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Hero hero, PartyBase party) =>
            !IsFixtureCaptivityLog(hero, party, release: true);
    }

    private static bool TryRestoreRecapturedPartySnapshot(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer player,
        out string error)
    {
        error = null;
        try
        {
            player.Hero.CaptivityStartTime = player.OriginalCaptivityStartTime;
            player.Party._ignoredUntilTime = player.OriginalIgnoredUntilTime;
            player.Party.IsInspected = player.OriginalIsInspected;
            player.Party.LastVisitedSettlement = player.OriginalLastVisitedSettlement;
            player.Party.Bearing = player.OriginalBearing;
            if (!fixture.BehaviorSnapshot.TryApply(player.Party, player.OriginalBehavior, out _))
            {
                error = "The original movement state for " + player.ControllerId +
                    " could not be reapplied.";
                return false;
            }

            player.Party.Bearing = player.OriginalBearing;
            MessageBroker.Instance.Publish(
                typeof(DefenderSiegeFixtureCommands),
                new PartyBehaviorChangeAttempted(
                    player.Party,
                    forcePosition: true,
                    isCurrentlyAtSea: player.Party.IsCurrentlyAtSea));
            RestoreAttackProtections(player);
            return true;
        }
        catch (Exception exception)
        {
            error = "The captured party state for " + player.ControllerId + " could not be restored: " +
                exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

    private static bool RestoreCapturedPartyVisibility(DefenderRosterFixturePlayer player, out string error)
    {
        error = null;
        try
        {
            player.Party.IsVisible = player.OriginalIsVisible;
        }
        catch (Exception exception)
        {
            error = "The authoritative captivity restore could not restore captured visibility for " +
                player.ControllerId + ": " + exception.GetType().Name + ": " + exception.Message;
            return false;
        }

        if (player.Party.IsVisible == player.OriginalIsVisible)
            return true;

        error = "The authoritative captivity restore did not restore captured visibility for " +
            player.ControllerId + ".";
        return false;
    }

    private static bool MatchesNormalRecapture(DefenderRosterFixturePlayer player) =>
        TryGetCaptorHeroPrisonerElement(player.CaptorParty, player.Hero, out TroopRosterElement captorHeroElement) &&
        DefenderRosterFixtureContract.IsCaptiveBaselineRestorable(
            player.Hero.IsPrisoner,
            ReferenceEquals(player.Hero.PartyBelongedToAsPrisoner, player.CaptorParty),
            player.CaptorParty.IsActive,
            player.Hero.PartyBelongedTo != null,
            player.Hero.HeroState == Hero.CharacterStates.Prisoner,
            captorHeroElement.Number,
            captorHeroElement.WoundedNumber,
            captorHeroElement.Xp,
            player.Party.IsActive,
            player.Party.IsVisible,
            player.Party.Party.GetPartyVisual() != null,
            player.Party.LeaderHero != null,
            player.Party.MemberRoster.GetTroopCount(player.Hero.CharacterObject),
            player.Party.MemberRoster.TotalManCount,
            player.Party.PrisonRoster.TotalManCount,
            player.Party.CurrentSettlement != null,
            player.Party.MapEvent != null,
            player.Party.BesiegerCamp != null,
            player.Party.IsTransitionInProgress,
            player.Party.Army != null,
            player.Party.AttachedTo != null,
            player.Party.AttachedParties?.Count > 0,
            player.Party.IsCurrentlyAtSea,
            player.Hero.StayingInSettlement != null);

    private static bool TryGetCaptorHeroPrisonerElement(
        PartyBase captorParty,
        Hero hero,
        out TroopRosterElement captorHeroElement)
    {
        int index = captorParty.PrisonRoster.FindIndexOfTroop(hero.CharacterObject);
        if (index < 0)
        {
            captorHeroElement = default;
            return false;
        }

        captorHeroElement = captorParty.PrisonRoster.GetElementCopyAtIndex(index);
        return ReferenceEquals(captorHeroElement.Character, hero.CharacterObject);
    }

    private static bool MatchesCapturedCaptivityBaseline(DefenderRosterFixturePlayer player) =>
        MatchesNormalRecapture(player) &&
        ReferenceEquals(player.Hero.PartyBelongedTo, player.OriginalHeroParty) &&
        ReferenceEquals(player.Party.LeaderHero, player.OriginalLeaderHero) &&
        player.Hero.HeroState == player.OriginalHeroState &&
        player.Hero.CaptivityStartTime.NumTicks == player.OriginalCaptivityStartTime.NumTicks &&
        player.Party._ignoredUntilTime.NumTicks == player.OriginalIgnoredUntilTime.NumTicks &&
        player.Party.IsInspected == player.OriginalIsInspected &&
        DefenderRosterFixtureContract.IsCapturedPartyStateCurrent(
            player.OriginalPartyState,
            ReadPartyState(player.PartyId, player.Party),
            player.CaptorParty.Position.X,
            player.CaptorParty.Position.Y,
            player.CaptorParty.Position.IsOnLand) &&
        HasExpectedAttackProtections(player);

    private static bool MatchesUncapturedBaseline(DefenderRosterFixturePlayer player) =>
        MatchesUncapturedBaselineWithPrisonerCount(
            player,
            player.OriginalPrisonerCount,
            expectedPrisoner: null,
            expectedPrisonerPresent: false,
            attackProtectionsCurrent: HasExpectedAttackProtections(player));

    private static bool MatchesUncapturedBaselineWithOwnedCaptiveDelta(
        DefenderRosterFixturePlayer player,
        Hero captive) =>
        player.OriginalPrisonerCount == 0 &&
        MatchesUncapturedBaselineWithPrisonerCount(
            player,
            player.OriginalPrisonerCount + 1,
            captive,
            expectedPrisonerPresent: true,
            attackProtectionsCurrent: HasExpectedAttackProtections(player));

    private static bool MatchesUncapturedBaselineWithOwnedCaptive(
        DefenderRosterFixturePlayer player,
        Hero captive) =>
        MatchesUncapturedBaselineWithPrisonerCount(
            player,
            player.OriginalPrisonerCount,
            captive,
            expectedPrisonerPresent: true,
            attackProtectionsCurrent: HasExpectedAttackProtections(player));

    private static bool MatchesCapturedRosterNonCaptiveBaseline(DefenderRosterFixturePlayer player) =>
        player.OwnedCaptiveDeltaHero == null
            ? MatchesUncapturedBaseline(player)
            : MatchesUncapturedBaselineWithOwnedCaptive(player, player.OwnedCaptiveDeltaHero);

    private static bool MatchesNormalizedNonCaptiveBaseline(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer player)
    {
        if (player.OwnedCaptiveDeltaHero == null)
            return MatchesUncapturedBaseline(player);

        DefenderRosterFixturePlayer ownedCaptive = FindOwnedCaptiveFixturePlayer(fixture, player);
        return player.OriginalPrisonerCount == 1 && ownedCaptive != null &&
            MatchesUncapturedBaselineWithPrisonerCount(
                player,
                player.OriginalPrisonerCount - 1,
                player.OwnedCaptiveDeltaHero,
                expectedPrisonerPresent: false,
                attackProtectionsCurrent: HasExpectedOwnedCaptiveReleaseAttackProtections(player, ownedCaptive));
    }

    private static bool MatchesRestorableNonCaptiveBaseline(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer player)
    {
        if (player.OwnedCaptiveDeltaHero == null)
            return MatchesNormalizedNonCaptiveBaseline(fixture, player);

        DefenderRosterFixturePlayer ownedCaptive = FindOwnedCaptiveFixturePlayer(fixture, player);
        if (ownedCaptive == null)
            return false;
        if (MatchesCapturedCaptivityBaseline(ownedCaptive))
            return MatchesCapturedRosterNonCaptiveBaseline(player);
        if (IsRecapturedSnapshotReplaySafe(fixture, ownedCaptive, out _))
        {
            return MatchesUncapturedBaselineWithPrisonerCount(
                player,
                player.OriginalPrisonerCount,
                player.OwnedCaptiveDeltaHero,
                expectedPrisonerPresent: true,
                attackProtectionsCurrent: HasExpectedOwnedCaptiveReleaseAttackProtections(player, ownedCaptive));
        }

        return IsReleasedRosterRestorable(fixture, ownedCaptive, out _) &&
            MatchesNormalizedNonCaptiveBaseline(fixture, player);
    }

    private static DefenderRosterFixturePlayer FindOwnedCaptiveFixturePlayer(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer player)
    {
        DefenderRosterFixturePlayer ownedCaptive = null;
        foreach (DefenderRosterFixturePlayer candidate in fixture.Players)
        {
            if (!candidate.WasCaptive || !ReferenceEquals(candidate.Hero, player.OwnedCaptiveDeltaHero))
                continue;
            if (ownedCaptive != null)
                return null;

            ownedCaptive = candidate;
        }

        return ownedCaptive;
    }

    private static bool MatchesUncapturedBaselineWithPrisonerCount(
        DefenderRosterFixturePlayer player,
        int expectedPrisonerCount,
        Hero expectedPrisoner,
        bool expectedPrisonerPresent,
        bool attackProtectionsCurrent) =>
        DefenderRosterFixtureContract.IsUncapturedPlayerReady(
            player.Hero.IsPrisoner,
            player.Hero.PartyBelongedToAsPrisoner != null,
            ReferenceEquals(player.Hero.PartyBelongedTo, player.Party),
            player.Hero.HeroState == Hero.CharacterStates.Active,
            player.Party.IsActive,
            player.Party.IsVisible,
            MobilePartyVisualManager.Current != null,
            player.Party.Party.GetPartyVisual() != null,
            ReferenceEquals(player.Party.LeaderHero, player.Hero),
            player.Party.MapEvent != null,
            player.Party.BesiegerCamp != null,
            player.Party.IsTransitionInProgress,
            player.Party.Army != null,
            player.Party.AttachedTo != null,
            player.Party.AttachedParties?.Count > 0,
            player.Party.IsCurrentlyAtSea) &&
        player.Hero.HeroState == player.OriginalHeroState &&
        ReferenceEquals(player.Hero.PartyBelongedTo, player.OriginalHeroParty) &&
        ReferenceEquals(player.Party.LeaderHero, player.OriginalLeaderHero) &&
        player.Hero.CaptivityStartTime.NumTicks == player.OriginalCaptivityStartTime.NumTicks &&
        player.Party._ignoredUntilTime.NumTicks == player.OriginalIgnoredUntilTime.NumTicks &&
        player.Party.IsVisible == player.OriginalIsVisible &&
        player.Party.IsInspected == player.OriginalIsInspected &&
        (player.Party.Party.GetPartyVisual() != null) == player.OriginalVisualPresent &&
        player.Party.MemberRoster.GetTroopCount(player.Hero.CharacterObject) == player.OriginalHeroMemberCount &&
        player.Party.MemberRoster.TotalManCount == player.OriginalMemberCount &&
        player.Party.PrisonRoster.TotalManCount == expectedPrisonerCount &&
        (expectedPrisoner == null || HasExpectedOwnedCaptivePrisoner(
            player.Party.Party,
            expectedPrisoner,
            expectedPrisonerPresent)) &&
        player.OriginalPartyState.Equals(ReadPartyState(player.PartyId, player.Party)) &&
        attackProtectionsCurrent;

    private static bool HasExpectedOwnedCaptivePrisoner(
        PartyBase captorParty,
        Hero captive,
        bool expectedPresent)
    {
        int index = captorParty.PrisonRoster.FindIndexOfTroop(captive.CharacterObject);
        if (!expectedPresent)
            return index < 0;
        if (!TryGetCaptorHeroPrisonerElement(captorParty, captive, out TroopRosterElement element))
            return false;

        return element.Number == 1 && element.WoundedNumber == 0 && element.Xp == 0;
    }

    private static bool IsRosterFixtureRestored(DefenderRosterFixture fixture, out string error)
    {
        if (!HasCurrentRosterRegistryIdentities(fixture, out error))
            return false;
        foreach (DefenderRosterFixturePlayer player in fixture.Players)
        {
            bool restored = player.WasCaptive
                ? MatchesCapturedCaptivityBaseline(player)
                : MatchesCapturedRosterNonCaptiveBaseline(player);
            if (restored) continue;

            error = "The roster fixture did not restore the captured state for " + player.ControllerId + ".";
            return false;
        }

        error = null;
        return true;
    }

    private static AttackProtectionSnapshot[] CaptureAttackProtections(MobileParty party) =>
        DefaultMobilePartyAIModelPatches.GetPersistedAttackProtections()
            .Where(protection => ReferenceEquals(protection.AttackerParty, party) ||
                ReferenceEquals(protection.TargetParty, party))
            .Select(protection => new AttackProtectionSnapshot(
                protection.AttackerParty,
                protection.TargetParty,
                protection.DisabledUntil))
            .ToArray();

    private static FactionAttackProtectionSnapshot[] CaptureFactionAttackProtections(MobileParty party) =>
        DefaultMobilePartyAIModelPatches.GetPersistedFactionAttackProtections()
            .Where(protection => ReferenceEquals(protection.AttackerParty, party))
            .Select(protection => new FactionAttackProtectionSnapshot(
                protection.AttackerParty,
                protection.TargetFaction,
                protection.DisabledUntil))
            .ToArray();

    private static bool HasOnlyCapturedAndReleaseAttackProtections(DefenderRosterFixturePlayer player)
    {
        AttackProtectionSnapshot[] actual = CaptureAttackProtections(player.Party);
        bool originalPartyProtectionsCurrent = player.OriginalAttackProtections.All(expected =>
            actual.Any(candidate => expected.Equals(candidate)));
        bool onlyOriginalAndReleaseProtections = actual.All(protection =>
            player.OriginalAttackProtections.Any(expected => expected.Equals(protection)) ||
            ReferenceEquals(protection.AttackerParty, player.CaptorParty.MobileParty) &&
            ReferenceEquals(protection.TargetParty, player.Party) && protection.DisabledUntil.IsFuture);
        bool hasAtMostOneReleaseProtection = actual.Count(protection =>
            ReferenceEquals(protection.AttackerParty, player.CaptorParty.MobileParty) &&
            ReferenceEquals(protection.TargetParty, player.Party) && protection.DisabledUntil.IsFuture) <= 1;
        return DefenderRosterFixtureContract.IsCapturedAttackProtectionStateCurrent(
            originalPartyProtectionsCurrent,
            onlyOriginalAndReleaseProtections,
            hasAtMostOneReleaseProtection,
            HasExpectedFactionAttackProtections(player.Party, player.OriginalFactionAttackProtections));
    }

    private static bool HasExpectedOwnedCaptiveReleaseAttackProtections(
        DefenderRosterFixturePlayer player,
        DefenderRosterFixturePlayer ownedCaptive)
    {
        if (ownedCaptive == null || !ReferenceEquals(ownedCaptive.Hero, player.OwnedCaptiveDeltaHero))
            return false;

        AttackProtectionSnapshot[] actual = CaptureAttackProtections(player.Party);
        bool originalPartyProtectionsCurrent = player.OriginalAttackProtections.All(expected =>
            actual.Any(candidate => expected.Equals(candidate)));
        bool hasOnlyOriginalAndOwnedReleaseProtection = actual.All(protection =>
            player.OriginalAttackProtections.Any(expected => expected.Equals(protection)) ||
            IsOwnedCaptiveReleaseAttackProtection(player, ownedCaptive, protection));
        bool hasExactlyOneOwnedReleaseProtection = actual.Count(protection =>
            IsOwnedCaptiveReleaseAttackProtection(player, ownedCaptive, protection)) == 1;
        return actual.Length == player.OriginalAttackProtections.Length + 1 &&
            originalPartyProtectionsCurrent && hasOnlyOriginalAndOwnedReleaseProtection &&
            hasExactlyOneOwnedReleaseProtection &&
            HasExpectedFactionAttackProtections(player.Party, player.OriginalFactionAttackProtections);
    }

    private static bool IsOwnedCaptiveReleaseAttackProtection(
        DefenderRosterFixturePlayer player,
        DefenderRosterFixturePlayer ownedCaptive,
        AttackProtectionSnapshot protection) =>
        ReferenceEquals(protection.AttackerParty, player.Party) &&
        ReferenceEquals(protection.TargetParty, ownedCaptive.Party) && protection.DisabledUntil.IsFuture;

    private static bool HasExpectedAttackProtections(DefenderRosterFixturePlayer player) =>
        DefenderRosterFixtureContract.HasExactAttackProtectionRestoration(
            HasExpectedPartyAttackProtections(player.Party, player.OriginalAttackProtections),
            HasExpectedFactionAttackProtections(player.Party, player.OriginalFactionAttackProtections));

    private static bool HasExpectedPartyAttackProtections(
        MobileParty party,
        AttackProtectionSnapshot[] expected)
    {
        AttackProtectionSnapshot[] actual = CaptureAttackProtections(party);
        return actual.Length == expected.Length && actual.All(candidate => expected.Any(candidate.Equals));
    }

    private static bool HasExpectedFactionAttackProtections(
        MobileParty party,
        FactionAttackProtectionSnapshot[] expected)
    {
        FactionAttackProtectionSnapshot[] actual = CaptureFactionAttackProtections(party);
        return actual.Length == expected.Length && actual.All(candidate => expected.Any(candidate.Equals));
    }

    private static void RestoreAttackProtections(DefenderRosterFixturePlayer player)
    {
        DefaultMobilePartyAIModelPatches.RemoveAttackProtectionsForParty(player.Party);
        foreach (AttackProtectionSnapshot protection in player.OriginalAttackProtections)
        {
            DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
                protection.AttackerParty,
                protection.TargetParty,
                protection.DisabledUntil);
        }
        foreach (FactionAttackProtectionSnapshot protection in player.OriginalFactionAttackProtections)
        {
            DefaultMobilePartyAIModelPatches.PreventFactionAttacksUntil(
                protection.AttackerParty,
                protection.TargetFaction,
                protection.DisabledUntil);
        }
    }
#endif

    private static bool TryGetExpectedControllerIds(
        IReadOnlyCollection<string> args,
        out string[] expectedControllerIds,
        out string error)
    {
        expectedControllerIds = null;
        error = null;
        if (args.Count != ExpectedPlayerCount)
        {
            error = "Usage: coop.debug.siege.defender_fixture_capture <firstControllerId> <secondControllerId>";
            return false;
        }

        expectedControllerIds = args
            .Select(controllerId => controllerId?.Trim())
            .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
            .ToArray();
        if (expectedControllerIds.Any(string.IsNullOrEmpty) ||
            !DefenderSiegeFixtureContract.HasExactControllerIds(
                expectedControllerIds, expectedControllerIds))
        {
            error = "The two expected controller ids must be non-empty and distinct.";
            expectedControllerIds = null;
            return false;
        }

        return true;
    }

    private static bool HasCurrentFixtureIdentities(DefenderSiegeFixture fixture)
    {
        if (fixture.CapturedCampaign == null || !ReferenceEquals(Campaign.Current, fixture.CapturedCampaign) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objects) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var players) ||
            !ContainerProvider.TryResolve<IDefenderFixtureBehaviorIdentity>(out var behaviorIdentity) ||
            !objects.TryGetObject<Settlement>(SettlementId, out var settlement) ||
            !ReferenceEquals(settlement, fixture.Settlement)) return false;
        foreach (var party in fixture.Parties)
        {
            if (!players.TryGetPlayer(party.ControllerId, out var player) || !ReferenceEquals(player, party.Player) ||
                player.MobilePartyId != party.PartyId ||
                !objects.TryGetObject<MobileParty>(party.PartyId, out var currentParty) ||
                !ReferenceEquals(currentParty, party.Party) ||
                !IsSettlementIdentityCurrent(objects, party.OriginalSettlementId, party.OriginalSettlement) ||
                !IsSettlementIdentityCurrent(objects, party.OriginalLastVisitedSettlementId, party.OriginalLastVisitedSettlement) ||
                !behaviorIdentity.IsCurrent(party.OriginalBehaviorReferences))
                return false;
        }
        return true;
    }

    private static bool HasRestorableConnectionState(DefenderFixtureParty party) =>
        ContainerProvider.TryResolve<IPlayerManager>(out var players) &&
        (players.IsConnected(party.Player)
            ? party.Party.IsActive == party.OriginalState.IsActive
            : !party.Party.IsActive && !party.Party.IsVisible);

    private static bool IsOfflineParked(DefenderFixtureParty party) =>
        !party.Party.IsActive && !party.Party.IsVisible &&
        ContainerProvider.TryResolve<IPlayerManager>(out var players) && !players.IsConnected(party.Player);

    private static bool IsSettlementIdentityCurrent(IObjectManager objects, string id, Settlement expected) =>
        expected == null ? id == null : objects.TryGetObject<Settlement>(id, out var actual) && ReferenceEquals(actual, expected);

    private static bool IsCaptureCurrent(DefenderSiegeFixture fixture) =>
        HasCurrentFixtureIdentities(fixture) && fixture.Settlement.Party.MapEvent == null && fixture.Settlement.SiegeEvent == null &&
        fixture.Parties.All(party => DefenderSiegeFixtureContract.IsRestored(
            party.OriginalState, ReadPartyState(party.PartyId, party.Party)));

    private static bool IsStaged(DefenderSiegeFixture fixture) =>
        HasCurrentFixtureIdentities(fixture) && fixture.Settlement.Party.MapEvent == null && fixture.Settlement.SiegeEvent == null &&
        fixture.Parties.All(party => DefenderSiegeFixtureContract.IsStaged(
            ReadPartyState(party.PartyId, party.Party), fixture.Settlement.StringId));

    private static bool CanStage(DefenderSiegeFixture fixture, out string error)
    {
        error = null;
        if (!HasCurrentFixtureIdentities(fixture))
        {
            error = "The captured defender campaign or registry identity changed.";
            return false;
        }
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !HasExpectedSynchronizedControllers(playerManager, fixture.ExpectedControllerIds))
        {
            error = "The captured defender controllers must finish campaign synchronization before staging.";
            return false;
        }
        if (fixture.Parties.Any(party => WouldUpdateOwnerVisit(party.Party, fixture.Settlement)))
        {
            error = "A defender party would mutate Odrysa Castle's owner visit timestamp.";
            return false;
        }
        if (fixture.Parties.Any(party => party.OriginalSettlement != null &&
                WouldUpdateOwnerVisit(party.Party, party.OriginalSettlement)))
        {
            error = "A defender party would mutate its original settlement's owner visit timestamp.";
            return false;
        }

        return true;
    }

    private static bool CanRestore(DefenderSiegeFixture fixture, out string error)
    {
        error = null;
        if (!HasCurrentFixtureIdentities(fixture))
        {
            error = "The captured defender campaign or registry identity changed.";
            return false;
        }
        if (fixture.Settlement.Party.MapEvent != null || fixture.Settlement.SiegeEvent != null)
        {
            error = "Odrysa Castle has a map event or besieger camp; fixture restore is unsafe.";
            return false;
        }
        if (fixture.Parties.Any(party => !HasRestorableConnectionState(party) ||
                !DefenderSiegeFixtureContract.IsCleanForRestore(
                ReadPartyState(party.PartyId, party.Party), IsOfflineParked(party))))
        {
            error = "A defender party is no longer clean; fixture restore is unsafe.";
            return false;
        }
        if (fixture.Parties.Any(party => party.OriginalSettlement != null &&
                WouldUpdateOwnerVisit(party.Party, party.OriginalSettlement)))
        {
            error = "A defender party would mutate its original settlement's owner visit timestamp.";
            return false;
        }

        return true;
    }

    private static bool RestoreParty(
        DefenderSiegeFixture fixture,
        DefenderFixtureParty fixtureParty,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        if (!HasCurrentFixtureIdentities(fixture)) return false;
        MobileParty party = fixtureParty.Party;
        if (party.CurrentSettlement != fixtureParty.OriginalSettlement)
        {
            if (party.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(party);
            if (!HasCurrentFixtureIdentities(fixture)) return false;
            if (fixtureParty.OriginalSettlement != null)
                EnterSettlementAction.ApplyForParty(party, fixtureParty.OriginalSettlement);
        }
        if (!HasCurrentFixtureIdentities(fixture)) return false;

        party.Bearing = fixtureParty.OriginalBearing;
        party.LastVisitedSettlement = fixtureParty.OriginalLastVisitedSettlement;
        if (!behaviorSnapshot.TryApply(party, fixtureParty.OriginalBehavior, out _) ||
            !HasCurrentFixtureIdentities(fixture)) return false;

        party.Position = fixtureParty.OriginalPosition;
        MessageBroker.Instance.Publish(
            typeof(DefenderSiegeFixtureCommands),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: party.IsCurrentlyAtSea));
        return true;
    }

    private static bool StagePartyInsideSettlement(DefenderSiegeFixture fixture, MobileParty party)
    {
        if (!HasCurrentFixtureIdentities(fixture)) return false;
        if (party.CurrentSettlement == fixture.Settlement) return true;
        if (party.CurrentSettlement != null)
            LeaveSettlementAction.ApplyForParty(party);
        if (!HasCurrentFixtureIdentities(fixture)) return false;
        EnterSettlementAction.ApplyForParty(party, fixture.Settlement);
        return HasCurrentFixtureIdentities(fixture);
    }

    private static bool WouldUpdateOwnerVisit(MobileParty party, Settlement settlement)
    {
        Hero leader = party?.LeaderHero;
        bool partyLeaderIsSettlementOwner = leader != null &&
            leader.Clan == settlement?.OwnerClan && leader.Clan?.Leader == leader;
        return !DefenderSiegeFixtureContract.CanEnterSettlementWithoutOwnerVisit(
            partyLeaderIsSettlementOwner);
    }

    private static bool IsRestored(DefenderSiegeFixture fixture) =>
        HasCurrentFixtureIdentities(fixture) && fixture.Settlement.Party.MapEvent == null && fixture.Settlement.SiegeEvent == null &&
        fixture.Parties.All(party => HasRestorableConnectionState(party) &&
            DefenderSiegeFixtureContract.IsRestored(
                party.OriginalState, ReadPartyState(party.PartyId, party.Party), IsOfflineParked(party)));

    private static bool IsExpectedLocalParty(
        string localControllerId,
        MobileParty localParty,
        string[] expectedControllerIds,
        Player[] expectedPlayers,
        MobileParty[] expectedParties)
    {
        if (string.IsNullOrEmpty(localControllerId) || localParty == null) return false;
        int index = Array.IndexOf(expectedControllerIds, localControllerId);
        return index >= 0 && expectedPlayers[index] != null &&
            ReferenceEquals(localParty, expectedParties[index]);
    }

    private static DefenderFixturePartyState ReadPartyState(string partyId, MobileParty party) =>
        new DefenderFixturePartyState(
            partyId,
            party.IsActive,
            party.IsCurrentlyAtSea,
            party.PartyMoveMode == MoveModeType.Hold,
            party.CurrentSettlement?.StringId,
            party.LastVisitedSettlement?.StringId,
            party.Position.X,
            party.Position.Y,
            party.Position.IsOnLand,
            party.Bearing.X,
            party.Bearing.Y,
            party.MapEvent != null,
            party.BesiegerCamp != null,
            party.IsTransitionInProgress,
            party.Army != null,
            party.AttachedTo != null,
            party.AttachedParties?.Count > 0);

    private static string PreAssaultResult(
        string[] expectedControllerIds,
        bool success,
        string reason,
        Settlement settlement,
        IPlayerManager playerManager) => JsonResult(new
        {
            success,
            reason,
            role = ModInformation.IsServer ? "server" : "client",
            settlementId = settlement?.StringId ?? SettlementId,
            expectedPlayerCount = ExpectedPlayerCount,
            expectedControllerIds = expectedControllerIds ?? Array.Empty<string>(),
            registeredPlayerCount = playerManager?.Players.Count ?? 0,
            registeredControllerIds = playerManager?.Players
                .Select(player => player.ControllerId)
                .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>(),
            connectedPlayerCount = -1,
            connectedControllerIds = Array.Empty<string>(),
            connectedControllerIdsSource = "unavailable",
            connectedIdentityAuthority = ModInformation.IsServer ? "server-peer-registry" : "server-only",
            localControllerId = (string)null,
            localPartyId = (string)null,
            localPartyStringId = (string)null,
            noMapEvent = false,
            noBesiegerCamp = false,
            insideSettlement = false,
            fixtureStaged = (bool?)null,
            parties = Array.Empty<object>()
        });

    private static string FixtureResult(
        DefenderSiegeFixture fixture,
        string phase,
        bool success,
        string reason) => JsonResult(new
        {
            success,
            phase,
            reason,
            settlementId = fixture.Settlement.StringId,
            expectedPlayerCount = ExpectedPlayerCount,
            expectedControllerIds = fixture.ExpectedControllerIds,
            staged = fixture.IsStaged,
            parties = fixture.Parties.Select(party => new
            {
                controllerId = party.ControllerId,
                partyId = party.PartyId,
                partyStringId = party.Party.StringId,
                originalSettlementId = party.OriginalSettlement?.StringId,
                currentSettlementId = party.Party.CurrentSettlement?.StringId,
                hasMapEvent = party.Party.MapEvent != null,
                hasBesiegerCamp = party.Party.BesiegerCamp != null
            }).ToArray()
        });

    private static string Failure(string phase, string reason) => JsonResult(new
    {
        success = false,
        phase,
        reason,
        settlementId = SettlementId
    });

#if DEBUG
    private static string RosterFixtureFailure(string phase, string reason) => JsonResult(new
    {
        success = false,
        phase,
        reason,
        expectedPlayerCount = ExpectedPlayerCount,
        requiresReadinessRecheck = true
    });

    private static string RosterFixtureResult(
        DefenderRosterFixture fixture,
        string phase,
        bool success,
        string reason) => JsonResult(new
    {
        success,
        phase,
        reason,
        expectedPlayerCount = ExpectedPlayerCount,
        expectedControllerIds = fixture.ExpectedControllerIds,
        normalized = fixture.IsNormalized,
        normalizationUnsafe = fixture.NormalizationUnsafe,
        restoredPendingVerification = fixture.RestoredPendingVerification,
        requiresReadinessRecheck = true,
        players = fixture.Players.Select(player => new
        {
            controllerId = player.ControllerId,
            heroId = player.Hero.StringId,
            partyId = player.PartyId,
            partyStringId = player.Party.StringId,
            wasCaptive = player.WasCaptive,
            restoreCompleted = player.RestoreCompleted,
            heroIsPrisoner = player.Hero.IsPrisoner,
            heroBelongsToPrisonerParty = player.Hero.PartyBelongedToAsPrisoner != null,
            partyActive = player.Party.IsActive,
            partyVisible = player.Party.IsVisible,
            captorPartyId = GetPartyId(player.CaptorParty)
        }).ToArray()
    });

    private static string RosterCaptiveBaselineFailure(string phase, string reason) => JsonResult(new
    {
        success = false,
        phase,
        reason,
        expectedPlayerCount = ExpectedPlayerCount,
        requiresReadinessRecheck = true
    });

    private static string RosterCaptiveBaselineResult(
        DefenderRosterCaptiveBaselineFixture baseline,
        string phase,
        bool success,
        string reason) => JsonResult(new
    {
        success,
        phase,
        reason,
        expectedPlayerCount = ExpectedPlayerCount,
        expectedControllerIds = baseline.Fixture.ExpectedControllerIds,
        captiveControllerId = baseline.Captive.ControllerId,
        captorControllerId = baseline.Captor.ControllerId,
        captureAttempted = baseline.CaptureAttempted,
        restoreRequired = !baseline.RestoredPendingVerification,
        restoredPendingVerification = baseline.RestoredPendingVerification,
        requiresReadinessRecheck = true,
        players = baseline.Fixture.Players.Select(player => new
        {
            controllerId = player.ControllerId,
            heroId = player.Hero.StringId,
            partyId = player.PartyId,
            partyStringId = player.Party.StringId,
            heroIsPrisoner = player.Hero.IsPrisoner,
            heroHasCaptor = player.Hero.PartyBelongedToAsPrisoner != null,
            partyActive = player.Party.IsActive,
            partyVisible = player.Party.IsVisible
        }).ToArray()
    });
#endif

    private static string GetPartyId(PartyBase party) =>
        party?.MobileParty?.StringId ?? party?.Settlement?.StringId ?? "none";

    private static string JsonResult(object value) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);

    private sealed class DefenderSiegeFixture
    {
        public Campaign CapturedCampaign { get; }
        public string[] ExpectedControllerIds { get; }
        public Settlement Settlement { get; }
        public DefenderFixtureParty[] Parties { get; }
        public bool IsStaged { get; set; }

        public DefenderSiegeFixture(
            string[] expectedControllerIds,
            Settlement settlement,
            DefenderFixtureParty[] parties)
        {
            CapturedCampaign = Campaign.Current;
            ExpectedControllerIds = expectedControllerIds;
            Settlement = settlement;
            Parties = parties;
        }

        public bool HasExpectedControllers(IEnumerable<string> controllerIds) =>
            DefenderSiegeFixtureContract.HasExactControllerIds(
                ExpectedControllerIds, controllerIds);
    }

    private sealed class DefenderFixtureParty
    {
        public Player Player { get; }
        public string ControllerId => Player.ControllerId;
        public string PartyId { get; }
        public MobileParty Party { get; }
        public Settlement OriginalSettlement { get; }
        public Settlement OriginalLastVisitedSettlement { get; }
        public string OriginalSettlementId { get; }
        public string OriginalLastVisitedSettlementId { get; }
        public CampaignVec2 OriginalPosition { get; }
        public Vec2 OriginalBearing { get; }
        public PartyBehaviorUpdateData OriginalBehavior { get; }
        public DefenderFixtureBehaviorReferences OriginalBehaviorReferences { get; }
        public DefenderFixturePartyState OriginalState { get; }

        public DefenderFixtureParty(
            Player player,
            string partyId,
            MobileParty party,
            Settlement originalSettlement,
            Settlement originalLastVisitedSettlement,
            string originalSettlementId,
            string originalLastVisitedSettlementId,
            CampaignVec2 originalPosition,
            Vec2 originalBearing,
            PartyBehaviorUpdateData originalBehavior,
            DefenderFixtureBehaviorReferences originalBehaviorReferences,
            DefenderFixturePartyState originalState)
        {
            Player = player;
            PartyId = partyId;
            Party = party;
            OriginalSettlement = originalSettlement;
            OriginalLastVisitedSettlement = originalLastVisitedSettlement;
            OriginalSettlementId = originalSettlementId;
            OriginalLastVisitedSettlementId = originalLastVisitedSettlementId;
            OriginalPosition = originalPosition;
            OriginalBearing = originalBearing;
            OriginalBehavior = originalBehavior;
            OriginalBehaviorReferences = originalBehaviorReferences;
            OriginalState = originalState;
        }
    }

#if DEBUG
    private sealed class DefenderRosterFixture
    {
        public Campaign CapturedCampaign { get; }
        public string[] ExpectedControllerIds { get; }
        public DefenderRosterFixturePlayer[] Players { get; }
        public IMobilePartyBehaviorSnapshot BehaviorSnapshot { get; }
        public bool IsNormalized { get; set; }
        public bool NormalizationUnsafe { get; set; }
        public bool RestoredPendingVerification { get; set; }

        public DefenderRosterFixture(
            string[] expectedControllerIds,
            DefenderRosterFixturePlayer[] players,
            IMobilePartyBehaviorSnapshot behaviorSnapshot)
        {
            CapturedCampaign = Campaign.Current;
            ExpectedControllerIds = expectedControllerIds;
            Players = players;
            BehaviorSnapshot = behaviorSnapshot;
        }
    }

    private sealed class DefenderRosterCaptiveBaselineFixture
    {
        public DefenderRosterFixture Fixture { get; }
        public DefenderRosterFixturePlayer Captive { get; }
        public DefenderRosterFixturePlayer Captor { get; }
        public bool CaptureAttempted { get; set; }
        public bool RestoredPendingVerification { get; set; }

        public DefenderRosterCaptiveBaselineFixture(
            DefenderRosterFixture fixture,
            DefenderRosterFixturePlayer captive,
            DefenderRosterFixturePlayer captor)
        {
            Fixture = fixture;
            Captive = captive;
            Captor = captor;
        }

        public bool HasExpectedControllers(IEnumerable<string> controllerIds) =>
            DefenderSiegeFixtureContract.HasExactControllerIds(
                Fixture.ExpectedControllerIds, controllerIds);
    }

    private sealed class DefenderRosterFixturePlayer
    {
        public string ControllerId { get; }
        public Player Player { get; }
        public Hero Hero { get; }
        public string PartyId { get; }
        public MobileParty Party { get; }
        public PartyBase CaptorParty { get; set; }
        public MobileParty OriginalHeroParty { get; }
        public Hero OriginalLeaderHero { get; }
        public DefenderFixturePartyState OriginalPartyState { get; }
        public Settlement OriginalLastVisitedSettlement { get; }
        public Vec2 OriginalBearing { get; }
        public PartyBehaviorUpdateData OriginalBehavior { get; }
        public Hero.CharacterStates OriginalHeroState { get; }
        public CampaignTime OriginalCaptivityStartTime { get; }
        public CampaignTime OriginalIgnoredUntilTime { get; }
        public bool OriginalIsVisible { get; }
        public bool OriginalIsInspected { get; }
        public bool OriginalVisualPresent { get; }
        public int OriginalHeroMemberCount { get; }
        public int OriginalMemberCount { get; }
        public int OriginalPrisonerCount { get; }
        public AttackProtectionSnapshot[] OriginalAttackProtections { get; }
        public FactionAttackProtectionSnapshot[] OriginalFactionAttackProtections { get; }
        public bool WasCaptive { get; }
        public Hero OwnedCaptiveDeltaHero { get; }
        public bool RestoreCompleted { get; set; }

        public DefenderRosterFixturePlayer(
            string controllerId,
            Player player,
            Hero hero,
            string partyId,
            MobileParty party,
            PartyBase captorParty,
            MobileParty originalHeroParty,
            Hero originalLeaderHero,
            DefenderFixturePartyState originalPartyState,
            Settlement originalLastVisitedSettlement,
            Vec2 originalBearing,
            PartyBehaviorUpdateData originalBehavior,
            Hero.CharacterStates originalHeroState,
            CampaignTime originalCaptivityStartTime,
            CampaignTime originalIgnoredUntilTime,
            bool originalIsVisible,
            bool originalIsInspected,
            bool originalVisualPresent,
            int originalHeroMemberCount,
            int originalMemberCount,
            int originalPrisonerCount,
            AttackProtectionSnapshot[] originalAttackProtections,
            FactionAttackProtectionSnapshot[] originalFactionAttackProtections,
            bool wasCaptive,
            Hero ownedCaptiveDeltaHero)
        {
            ControllerId = controllerId;
            Player = player;
            Hero = hero;
            PartyId = partyId;
            Party = party;
            CaptorParty = captorParty;
            OriginalHeroParty = originalHeroParty;
            OriginalLeaderHero = originalLeaderHero;
            OriginalPartyState = originalPartyState;
            OriginalLastVisitedSettlement = originalLastVisitedSettlement;
            OriginalBearing = originalBearing;
            OriginalBehavior = originalBehavior;
            OriginalHeroState = originalHeroState;
            OriginalCaptivityStartTime = originalCaptivityStartTime;
            OriginalIgnoredUntilTime = originalIgnoredUntilTime;
            OriginalIsVisible = originalIsVisible;
            OriginalIsInspected = originalIsInspected;
            OriginalVisualPresent = originalVisualPresent;
            OriginalHeroMemberCount = originalHeroMemberCount;
            OriginalMemberCount = originalMemberCount;
            OriginalPrisonerCount = originalPrisonerCount;
            OriginalAttackProtections = originalAttackProtections;
            OriginalFactionAttackProtections = originalFactionAttackProtections;
            WasCaptive = wasCaptive;
            OwnedCaptiveDeltaHero = ownedCaptiveDeltaHero;
        }
    }

    private sealed class AttackProtectionSnapshot : IEquatable<AttackProtectionSnapshot>
    {
        public MobileParty AttackerParty { get; }
        public MobileParty TargetParty { get; }
        public CampaignTime DisabledUntil { get; }

        public AttackProtectionSnapshot(
            MobileParty attackerParty,
            MobileParty targetParty,
            CampaignTime disabledUntil)
        {
            AttackerParty = attackerParty;
            TargetParty = targetParty;
            DisabledUntil = disabledUntil;
        }

        public bool Equals(AttackProtectionSnapshot other) =>
            other != null && ReferenceEquals(AttackerParty, other.AttackerParty) &&
            ReferenceEquals(TargetParty, other.TargetParty) &&
            DisabledUntil.NumTicks == other.DisabledUntil.NumTicks;

        public override bool Equals(object obj) => Equals(obj as AttackProtectionSnapshot);

        public override int GetHashCode() =>
            (AttackerParty?.GetHashCode() ?? 0) ^ (TargetParty?.GetHashCode() ?? 0) ^
            DisabledUntil.NumTicks.GetHashCode();
    }

    private sealed class FactionAttackProtectionSnapshot : IEquatable<FactionAttackProtectionSnapshot>
    {
        public MobileParty AttackerParty { get; }
        public IFaction TargetFaction { get; }
        public CampaignTime DisabledUntil { get; }

        public FactionAttackProtectionSnapshot(
            MobileParty attackerParty,
            IFaction targetFaction,
            CampaignTime disabledUntil)
        {
            AttackerParty = attackerParty;
            TargetFaction = targetFaction;
            DisabledUntil = disabledUntil;
        }

        public bool Equals(FactionAttackProtectionSnapshot other) =>
            other != null && ReferenceEquals(AttackerParty, other.AttackerParty) &&
            ReferenceEquals(TargetFaction, other.TargetFaction) &&
            DisabledUntil.NumTicks == other.DisabledUntil.NumTicks;

        public override bool Equals(object obj) => Equals(obj as FactionAttackProtectionSnapshot);

        public override int GetHashCode() =>
            (AttackerParty?.GetHashCode() ?? 0) ^ (TargetFaction?.GetHashCode() ?? 0) ^
            DisabledUntil.NumTicks.GetHashCode();
    }
#endif
}

internal readonly struct DefenderFixturePartyState : IEquatable<DefenderFixturePartyState>
{
    public string PartyId { get; }
    public bool IsActive { get; }
    public bool IsAtSea { get; }
    public bool IsHolding { get; }
    public string CurrentSettlementId { get; }
    public string LastVisitedSettlementId { get; }
    public float PositionX { get; }
    public float PositionY { get; }
    public bool PositionIsOnLand { get; }
    public float BearingX { get; }
    public float BearingY { get; }
    public bool HasMapEvent { get; }
    public bool HasBesiegerCamp { get; }
    public bool IsTransitionInProgress { get; }
    public bool HasArmy { get; }
    public bool HasAttachedTo { get; }
    public bool HasAttachedParties { get; }

    public DefenderFixturePartyState(
        string partyId,
        bool isActive,
        bool isAtSea,
        bool isHolding,
        string currentSettlementId,
        string lastVisitedSettlementId,
        float positionX,
        float positionY,
        bool positionIsOnLand,
        float bearingX,
        float bearingY,
        bool hasMapEvent,
        bool hasBesiegerCamp,
        bool isTransitionInProgress,
        bool hasArmy,
        bool hasAttachedTo,
        bool hasAttachedParties)
    {
        PartyId = partyId;
        IsActive = isActive;
        IsAtSea = isAtSea;
        IsHolding = isHolding;
        CurrentSettlementId = currentSettlementId;
        LastVisitedSettlementId = lastVisitedSettlementId;
        PositionX = positionX;
        PositionY = positionY;
        PositionIsOnLand = positionIsOnLand;
        BearingX = bearingX;
        BearingY = bearingY;
        HasMapEvent = hasMapEvent;
        HasBesiegerCamp = hasBesiegerCamp;
        IsTransitionInProgress = isTransitionInProgress;
        HasArmy = hasArmy;
        HasAttachedTo = hasAttachedTo;
        HasAttachedParties = hasAttachedParties;
    }

    public bool Equals(DefenderFixturePartyState other) =>
        IsActive == other.IsActive && HasSameWorldState(other);

    public bool HasSameWorldState(DefenderFixturePartyState other) =>
        PartyId == other.PartyId &&
        IsAtSea == other.IsAtSea && IsHolding == other.IsHolding &&
        CurrentSettlementId == other.CurrentSettlementId &&
        LastVisitedSettlementId == other.LastVisitedSettlementId &&
        PositionX.Equals(other.PositionX) && PositionY.Equals(other.PositionY) &&
        PositionIsOnLand == other.PositionIsOnLand &&
        BearingX.Equals(other.BearingX) && BearingY.Equals(other.BearingY) &&
        HasMapEvent == other.HasMapEvent && HasBesiegerCamp == other.HasBesiegerCamp &&
        IsTransitionInProgress == other.IsTransitionInProgress && HasArmy == other.HasArmy &&
        HasAttachedTo == other.HasAttachedTo && HasAttachedParties == other.HasAttachedParties;

    public override bool Equals(object obj) =>
        obj is DefenderFixturePartyState state && Equals(state);

    public override int GetHashCode() =>
        (PartyId ?? string.Empty).GetHashCode();
}

internal static class DefenderSiegeFixtureContract
{
    internal static bool HasExactControllerIds(
        IEnumerable<string> actualControllerIds,
        IEnumerable<string> expectedControllerIds)
    {
        if (actualControllerIds == null || expectedControllerIds == null) return false;
        string[] actual = actualControllerIds.ToArray();
        string[] expected = expectedControllerIds.ToArray();
        if (actual.Length != 2 || expected.Length != 2 ||
            actual.Any(string.IsNullOrEmpty) || expected.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);
        var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
        return actualSet.Count == actual.Length && expectedSet.Count == expected.Length &&
            actualSet.SetEquals(expectedSet);
    }

    internal static bool IsCleanForCapture(DefenderFixturePartyState state) =>
        IsCleanForRestore(state, offlineParked: false);

    internal static bool IsCleanForRestore(DefenderFixturePartyState state, bool offlineParked) =>
        (state.IsActive || offlineParked) && !state.IsAtSea &&
        !state.HasMapEvent && !state.HasBesiegerCamp &&
        !state.IsTransitionInProgress && !state.HasArmy &&
        !state.HasAttachedTo && !state.HasAttachedParties;

    internal static bool CanEnterSettlementWithoutOwnerVisit(
        bool partyLeaderIsSettlementOwner) => !partyLeaderIsSettlementOwner;

    internal static bool IsStaged(
        DefenderFixturePartyState state,
        string settlementId) =>
        IsCleanForCapture(state) && state.CurrentSettlementId == settlementId;

    internal static bool IsRestored(
        DefenderFixturePartyState expected,
        DefenderFixturePartyState actual,
        bool offlineParked = false) =>
        expected.HasSameWorldState(actual) &&
        (expected.IsActive == actual.IsActive || (offlineParked && !actual.IsActive)) &&
        IsCleanForRestore(actual, offlineParked);

    internal static bool IsPreAssaultReady(
        bool fixtureStaged,
        bool connectionReady,
        bool expectedPlayersResolved,
        bool noMapEvent,
        bool noBesiegerCamp,
        bool insideSettlement,
        bool localPartyReady) =>
        fixtureStaged && connectionReady && expectedPlayersResolved &&
        noMapEvent && noBesiegerCamp && insideSettlement && localPartyReady;

    internal static bool IsConnectionReadinessSatisfied(
        bool isServer,
        int connectedPlayerCount,
        bool connectedControllersExact) =>
        connectedPlayerCount == 2 && (!isServer || connectedControllersExact);
}

#if DEBUG
internal static class DefenderRosterFixtureContract
{
    internal static bool IsNormalizationRetryable(bool isNormalized, bool normalizationUnsafe) =>
        !isNormalized && !normalizationUnsafe;

    internal static bool CanReportNormalizedSuccess(
        bool isNormalized,
        bool normalizationUnsafe,
        bool normalizedStateCurrent) =>
        isNormalized && !normalizationUnsafe && normalizedStateCurrent;

    internal static bool CanRestoreCapturedPlayer(
        bool capturedBaseline,
        bool recapturedSnapshotReplaySafe,
        bool releasedForRestoration) =>
        capturedBaseline || recapturedSnapshotReplaySafe || releasedForRestoration;

    internal static bool RequiresRestoreFailureEscalation(
        bool capturedBaseline,
        bool recapturedSnapshotReplaySafe,
        bool releasedForRestoration) =>
        !CanRestoreCapturedPlayer(
            capturedBaseline,
            recapturedSnapshotReplaySafe,
            releasedForRestoration);

    internal static bool IsRecapturedSnapshotReplaySafe(
        bool normallyRecaptured,
        bool behaviorRestorable,
        bool attackProtectionsCurrent) =>
        normallyRecaptured && behaviorRestorable && attackProtectionsCurrent;

    internal static bool CanAcceptNormalizedPlayer(
        bool releasedForReadiness,
        bool releasedRestorable) =>
        releasedForReadiness && releasedRestorable;

    internal static bool IsReleasedRosterRestorable(
        bool releasedForRestoration,
        bool behaviorRestorable,
        bool attackProtectionsCurrent) =>
        releasedForRestoration && behaviorRestorable && attackProtectionsCurrent;

    internal static bool HasExactAttackProtectionRestoration(
        bool partyProtectionsRestored,
        bool factionProtectionsRestored) =>
        partyProtectionsRestored && factionProtectionsRestored;

    internal static bool IsCapturedAttackProtectionStateCurrent(
        bool originalPartyProtectionsCurrent,
        bool onlyOriginalAndReleaseProtections,
        bool hasAtMostOneReleaseProtection,
        bool factionProtectionsCurrent) =>
        originalPartyProtectionsCurrent && onlyOriginalAndReleaseProtections &&
        hasAtMostOneReleaseProtection && factionProtectionsCurrent;

    internal static bool IsCapturedPartyStateCurrent(
        DefenderFixturePartyState originalState,
        DefenderFixturePartyState currentState,
        float captorPositionX,
        float captorPositionY,
        bool captorPositionIsOnLand) =>
        originalState.PartyId == currentState.PartyId &&
        originalState.IsActive == currentState.IsActive &&
        originalState.IsAtSea == currentState.IsAtSea &&
        originalState.IsHolding == currentState.IsHolding &&
        originalState.CurrentSettlementId == currentState.CurrentSettlementId &&
        originalState.LastVisitedSettlementId == currentState.LastVisitedSettlementId &&
        currentState.PositionX.Equals(captorPositionX) &&
        currentState.PositionY.Equals(captorPositionY) &&
        currentState.PositionIsOnLand == captorPositionIsOnLand &&
        originalState.BearingX.Equals(currentState.BearingX) &&
        originalState.BearingY.Equals(currentState.BearingY) &&
        originalState.HasMapEvent == currentState.HasMapEvent &&
        originalState.HasBesiegerCamp == currentState.HasBesiegerCamp &&
        originalState.IsTransitionInProgress == currentState.IsTransitionInProgress &&
        originalState.HasArmy == currentState.HasArmy &&
        originalState.HasAttachedTo == currentState.HasAttachedTo &&
        originalState.HasAttachedParties == currentState.HasAttachedParties;

    internal static bool IsCaptiveBaselineRestorable(
        bool heroIsPrisoner,
        bool heroHasCaptor,
        bool captorPartyIsActive,
        bool heroHasParty,
        bool heroStateIsPrisoner,
        int captorHeroCount,
        int captorHeroWoundedNumber,
        int captorHeroXp,
        bool partyActive,
        bool partyVisible,
        bool partyHasVisual,
        bool partyHasLeader,
        int partyHeroMemberCount,
        int partyMemberCount,
        int partyPrisonerCount,
        bool partyHasCurrentSettlement,
        bool partyHasMapEvent,
        bool partyHasBesiegerCamp,
        bool partyIsTransitioning,
        bool partyHasArmy,
        bool partyHasAttachedTo,
        bool partyHasAttachedParties,
        bool partyIsAtSea,
        bool heroStaysInSettlement) =>
        heroIsPrisoner && heroHasCaptor && captorPartyIsActive && !heroHasParty && heroStateIsPrisoner &&
        captorHeroCount == 1 &&
        captorHeroWoundedNumber == 0 && captorHeroXp == 0 &&
        !partyActive && partyVisible && !partyHasVisual && !partyHasLeader &&
        partyHeroMemberCount == 0 && partyMemberCount == 0 && partyPrisonerCount == 0 &&
        !partyHasCurrentSettlement && !partyHasMapEvent && !partyHasBesiegerCamp &&
        !partyIsTransitioning && !partyHasArmy && !partyHasAttachedTo &&
        !partyHasAttachedParties && !partyIsAtSea && !heroStaysInSettlement;

    internal static bool IsUncapturedPlayerReady(
        bool heroIsPrisoner,
        bool heroHasCaptor,
        bool heroBelongsToPlayerParty,
        bool heroStateIsActive,
        bool partyActive,
        bool partyVisible,
        bool visualManagerPresent,
        bool partyHasVisual,
        bool partyLeaderIsHero,
        bool partyHasMapEvent,
        bool partyHasBesiegerCamp,
        bool partyIsTransitioning,
        bool partyHasArmy,
        bool partyHasAttachedTo,
        bool partyHasAttachedParties,
        bool partyIsAtSea) =>
        !heroIsPrisoner && !heroHasCaptor && heroBelongsToPlayerParty && heroStateIsActive && partyActive &&
        partyVisible && (!visualManagerPresent || partyHasVisual) && partyLeaderIsHero &&
        !partyHasMapEvent && !partyHasBesiegerCamp && !partyIsTransitioning &&
        !partyHasArmy && !partyHasAttachedTo && !partyHasAttachedParties && !partyIsAtSea;

    internal static bool IsReleasedForReadiness(
        bool heroIsPrisoner,
        bool heroHasCaptor,
        bool heroBelongsToPlayerParty,
        bool heroStateIsActive,
        bool partyActive,
        bool partyVisible,
        bool visualManagerPresent,
        bool partyHasVisual,
        bool partyLeaderIsHero,
        int partyHeroMemberCount) =>
        !heroIsPrisoner && !heroHasCaptor && heroBelongsToPlayerParty && heroStateIsActive && partyActive &&
        partyVisible && (!visualManagerPresent || partyHasVisual) && partyLeaderIsHero && partyHeroMemberCount == 1;

    internal static bool IsReleasedForRestoration(
        bool heroIsPrisoner,
        bool heroHasCaptor,
        bool heroBelongsToPlayerParty,
        bool heroStateIsActive,
        bool partyActive,
        bool partyVisible,
        bool visualManagerPresent,
        bool partyHasVisual,
        bool partyLeaderIsHero,
        bool partyHasCurrentSettlement,
        bool partyHasMapEvent,
        bool partyHasBesiegerCamp,
        bool partyIsTransitioning,
        bool partyHasArmy,
        bool partyHasAttachedTo,
        bool partyHasAttachedParties,
        bool partyIsAtSea,
        int partyHeroMemberCount,
        int partyMemberCount,
        int partyPrisonerCount,
        bool captorPartyIsActive,
        int captorHeroPrisonerCount,
        bool playerDisconnected = false) =>
        !heroIsPrisoner && !heroHasCaptor && heroBelongsToPlayerParty && heroStateIsActive &&
        ((partyActive && partyVisible && (!visualManagerPresent || partyHasVisual)) ||
            (playerDisconnected && !partyActive && !partyVisible && !partyHasVisual)) && partyLeaderIsHero &&
        !partyHasCurrentSettlement && !partyHasMapEvent && !partyHasBesiegerCamp &&
        !partyIsTransitioning && !partyHasArmy && !partyHasAttachedTo &&
        !partyHasAttachedParties && !partyIsAtSea && partyHeroMemberCount == 1 && partyMemberCount == 1 &&
        partyPrisonerCount == 0 && captorPartyIsActive && captorHeroPrisonerCount == 0;
}
#endif
