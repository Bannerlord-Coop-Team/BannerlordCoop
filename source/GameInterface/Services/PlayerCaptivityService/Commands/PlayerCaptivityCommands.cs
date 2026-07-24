using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Utils.Commands;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.PlayerCaptivityService.Commands;

internal class PlayerCaptivityCommands
{
    public static readonly ILogger Logger = LogManager.GetLogger<PlayerCaptivityCommands>();
    private static CaptivityRosterFixture pendingRosterFixture;

    private static LiveTestFixtureSnapshot liveTestFixtureSnapshot;

    private const string RandomCapturePlayerUsage =
@"Usage:
  coop.debug.player_captivity.random_capture_player <heroId>

Example:
  coop.debug.player_captivity.random_capture_player Player

Captures the given hero and assigns a random mobile party as the captor.";

    [CommandLineArgumentFunction("random_capture_player", "coop.debug.player_captivity")]
    public static string RandomCapturePlayer(List<string> args)
    {
        var ctx = new CommandContext(
            "random_capture_player",
            RandomCapturePlayerUsage,
            args);

        if (!ctx.RequireServer(out var error))
            return error;

        if (!ctx.RequireArgCount(1, out error))
            return error;

        if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to capture hero: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
            return "Failed to capture hero: " + error;

        if (!TryGetRandomCaptor(out var newCaptor, out error))
            return "Failed to capture hero: " + error;

        return CaptureHero(hero, newCaptor);
    }

    private const string CapturePlayerUsage =
@"Usage:
  coop.debug.player_captivity.capture_player <heroId> <mobilePartyId>

Example:
  coop.debug.player_captivity.capture_player lord_1_29 MobileParty_looters_248

Captures the given hero and assigns the given mobile party as the captor. The party argument accepts
a co-op registry id or a local StringId.";

    [CommandLineArgumentFunction("capture_player", "coop.debug.player_captivity")]
    public static string CapturePlayer(List<string> args)
    {
        var ctx = new CommandContext(
            "capture_player",
            CapturePlayerUsage,
            args);

        if (!ctx.RequireServer(out var error))
            return error;

        if (!ctx.RequireArgCount(2, out error))
            return error;

        if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
            return error;

        if (!ctx.TryGetArg(1, "mobilePartyId", out var captorPartyId, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to capture hero: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
            return "Failed to capture hero: " + error;

        if (!objectManager.TryGetObject(captorPartyId, out MobileParty newCaptor)
            && !CommandHelpers.TryGetMobileParty(captorPartyId, out newCaptor, out error))
            return "Failed to capture hero: " + error;

        return CaptureHero(hero, newCaptor);
    }

    private const string CapturePlayerFixtureUsage =
@"Usage:
  coop.debug.player_captivity.capture_player_fixture <heroId> <mobilePartyId>

Example:
  coop.debug.player_captivity.capture_player_fixture Hero_Player2863 MobileParty_Player

Captures a registered co-op player through the real captivity path and records the transferred regular
troops for separate fixture cleanup. This is intended for automated tests.";

    [CommandLineArgumentFunction("capture_player_fixture", "coop.debug.player_captivity")]
    public static string CapturePlayerFixture(List<string> args)
    {
        var ctx = new CommandContext(
            "capture_player_fixture",
            CapturePlayerFixtureUsage,
            args);

        if (!ctx.RequireServer(out var error))
            return error;

        if (!ctx.RequireArgCount(2, out error))
            return error;

        if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
            return error;

        if (!ctx.TryGetArg(1, "mobilePartyId", out var captorPartyId, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to capture hero fixture: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
            return "Failed to capture hero fixture: " + error;

        if (!objectManager.TryGetObject(captorPartyId, out MobileParty captorParty)
            && !CommandHelpers.TryGetMobileParty(captorPartyId, out captorParty, out error))
            return "Failed to capture hero fixture: " + error;

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return "Failed to capture hero fixture: could not resolve PlayerManager.";

        var player = playerManager.Players.SingleOrDefault(candidate => candidate.HeroId == heroId);
        if (player == null)
            return $"Failed to capture hero fixture: hero '{GetHeroDisplayName(hero)}' is not a registered co-op player.";

        if (!objectManager.TryGetObject(player.MobilePartyId, out MobileParty playerParty))
            return $"Failed to capture hero fixture: player party '{player.MobilePartyId}' is not registered.";

        if (hero.IsPrisoner)
            return CaptureHero(hero, captorParty);

        if (hero.PartyBelongedTo != playerParty)
            return "Failed to capture hero fixture: the hero does not belong to the registered player party.";

        if (captorParty == playerParty)
            return "Failed to capture hero fixture: the player party cannot capture its own hero.";

        if (!playerParty.IsActive)
            return "Failed to capture hero fixture: the player party is not active.";

        if (pendingRosterFixture != null)
            return "Failed to capture hero fixture: another roster fixture is pending cleanup.";

        var regularTroops = SnapshotRegularTroops(playerParty.MemberRoster);
        if (HasOtherHero(playerParty.MemberRoster, hero))
            return "Failed to capture hero fixture: the player party contains another hero.";

        pendingRosterFixture = new CaptivityRosterFixture(hero, playerParty, captorParty, regularTroops);
        var captureResult = CaptureHero(hero, captorParty);
        if (!hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != captorParty.Party)
        {
            pendingRosterFixture = null;
            return captureResult;
        }

        if (playerParty.IsActive)
        {
            pendingRosterFixture = null;
            return captureResult + "\nFailed to record fixture regular troops: the captivity handler did not park the player party.";
        }

        return captureResult + "\nFixture regular troops recorded for cleanup: " +
            regularTroops.Sum(troop => troop.Number).ToString(CultureInfo.InvariantCulture);
    }

    private const string RestoreRosterFixtureUsage =
@"Usage:
  coop.debug.player_captivity.restore_roster_fixture <heroId>

Example:
  coop.debug.player_captivity.restore_roster_fixture Hero_Player2863

Restores the regular troops recorded by capture_player_fixture after the player has left captivity.";

    [CommandLineArgumentFunction("restore_roster_fixture", "coop.debug.player_captivity")]
    public static string RestoreRosterFixture(List<string> args)
    {
        var ctx = new CommandContext(
            "restore_roster_fixture",
            RestoreRosterFixtureUsage,
            args);

        if (!ctx.RequireServer(out var error))
            return error;

        if (!ctx.RequireArgCount(1, out error))
            return error;

        if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to restore roster fixture: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
            return "Failed to restore roster fixture: " + error;

        var fixture = pendingRosterFixture;
        if (fixture == null)
            return "No roster fixture is pending cleanup.";

        if (fixture.PlayerHero != hero)
            return $"Failed to restore roster fixture: the pending fixture belongs to '{GetHeroDisplayName(fixture.PlayerHero)}'.";

        if (hero.IsPrisoner)
            return "Failed to restore roster fixture: the player hero is still a prisoner.";

        var playerHeroIndex = fixture.PlayerParty.MemberRoster.FindIndexOfTroop(fixture.PlayerHero.CharacterObject);
        if (fixture.PlayerParty.MemberRoster.TotalManCount != 1 ||
            playerHeroIndex < 0 ||
            fixture.PlayerParty.MemberRoster.GetTroopCount(fixture.PlayerHero.CharacterObject) != 1)
            return "Failed to restore roster fixture: the player party is not the released hero's party of one.";

        foreach (var troop in fixture.RegularTroops)
        {
            var captorIndex = fixture.CaptorParty.PrisonRoster.FindIndexOfTroop(troop.Character);
            var availableTotal = captorIndex < 0
                ? 0
                : fixture.CaptorParty.PrisonRoster.GetTroopCount(troop.Character);
            var availableWounded = captorIndex < 0
                ? 0
                : fixture.CaptorParty.PrisonRoster.GetElementWoundedNumber(captorIndex);
            var availableHealthy = availableTotal - availableWounded;
            var recordedHealthy = troop.Number - troop.WoundedNumber;
            if (availableWounded < troop.WoundedNumber || availableHealthy < recordedHealthy)
                return $"Failed to restore roster fixture: captor no longer holds the recorded '{troop.Character.StringId}' troops.";
        }

        foreach (var troop in fixture.RegularTroops)
        {
            fixture.CaptorParty.PrisonRoster.AddToCounts(
                troop.Character,
                -troop.Number,
                false,
                -troop.WoundedNumber,
                0,
                true);
            fixture.PlayerParty.MemberRoster.AddToCounts(
                troop.Character,
                troop.Number,
                false,
                troop.WoundedNumber,
                troop.Xp,
                true);
        }

        pendingRosterFixture = null;
        return "Restored fixture regular troops: " +
            fixture.RegularTroops.Sum(troop => troop.Number).ToString(CultureInfo.InvariantCulture);
    }

    private const string ReleasePlayerUsage =
@"Usage:
  coop.debug.player_captivity.release_player <heroId>

Example:
  coop.debug.player_captivity.release_player Player

Releases the given player hero from captivity.";

    [CommandLineArgumentFunction("release_player", "coop.debug.player_captivity")]
    public static string ReleasePlayer(List<string> args)
    {
        var ctx = new CommandContext(
            "release_player",
            ReleasePlayerUsage,
            args);

        if (!ctx.RequireServer(out var error))
            return error;

        if (!ctx.RequireArgCount(1, out error))
            return error;

        if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to release hero: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
            return "Failed to release hero: " + error;

        if (!hero.IsPrisoner)
            return $"Hero '{GetHeroDisplayName(hero)}' is not a prisoner.";

        var captorId = hero.PartyBelongedToAsPrisoner?.MobileParty?.StringId ?? "unknown";

        try
        {
            EndCaptivityAction.ApplyByEscape(hero);

            return
                "Hero released successfully.\n" +
                $"Hero: {GetHeroDisplayName(hero)}\n" +
                $"Former captor StringId: {captorId}";
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException(
                $"Failed to release hero '{GetHeroDisplayName(hero)}'",
                ex);
        }
    }

    private const string PrepareVisualTestFixtureUsage =
@"Usage:
  coop.debug.player_captivity.prepare_visual_test_fixture <heroId> <captorPartyId>

Snapshots the player's party, removes its non-hero members, and moves it beside the captor. Server only.";

    [CommandLineArgumentFunction("prepare_visual_test_fixture", "coop.debug.player_captivity")]
    public static string PrepareVisualTestFixture(List<string> args)
    {
        var ctx = new CommandContext(
            "prepare_visual_test_fixture",
            PrepareVisualTestFixtureUsage,
            args);

        if (!ctx.RequireServer(out var error))
            return error;

        if (!ctx.RequireArgCount(2, out error))
            return error;

        if (liveTestFixtureSnapshot != null)
            return "A visual test fixture is already prepared.";

        if (!ctx.TryGetArg(0, "heroId", out var heroId, out error) ||
            !ctx.TryGetArg(1, "captorPartyId", out var captorPartyId, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to prepare visual test fixture: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
            return "Failed to prepare visual test fixture: " + error;

        if (!objectManager.TryGetObject(captorPartyId, out MobileParty captorParty) &&
            !CommandHelpers.TryGetMobileParty(captorPartyId, out captorParty, out error))
            return "Failed to prepare visual test fixture: " + error;

        var playerParty = hero.PartyBelongedTo;
        if (playerParty == null || playerParty == captorParty)
            return "Failed to prepare visual test fixture: player and captor parties must be distinct.";

        if (hero.IsPrisoner || playerParty.PrisonRoster.TotalManCount != 0)
            return "Failed to prepare visual test fixture: player must be free and their prison roster empty.";

        if (!playerParty.IsActive ||
            playerParty.LeaderHero != hero ||
            playerParty.MemberRoster.GetTroopCount(hero.CharacterObject) != 1)
            return "Failed to prepare visual test fixture: player must lead an active party containing them exactly once.";

        if (playerParty.IsCurrentlyAtSea != captorParty.IsCurrentlyAtSea ||
            playerParty.Position.IsOnLand != captorParty.Position.IsOnLand)
            return "Failed to prepare visual test fixture: player and captor parties must use the same navigation layer.";

        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !behaviorSnapshot.TryCreate(playerParty, out var behavior))
            return "Failed to prepare visual test fixture: unable to snapshot player party behavior.";

        var snapshot = new LiveTestFixtureSnapshot(
            playerParty,
            hero.CharacterObject,
            playerParty.MemberRoster.GetTroopRoster().ToArray(),
            behavior);
        liveTestFixtureSnapshot = snapshot;

        foreach (var element in snapshot.MemberRoster.Where(element => element.Character != snapshot.PlayerCharacter))
        {
            playerParty.MemberRoster.AddToCounts(
                element.Character,
                -element.Number,
                false,
                -element.WoundedNumber,
                -element.Xp);
        }

        playerParty.Position = new CampaignVec2(
            new TaleWorlds.Library.Vec2(captorParty.Position.X + 1f, captorParty.Position.Y),
            captorParty.Position.IsOnLand);
        playerParty.SetMoveModeHold();
        playerParty.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(PlayerCaptivityCommands),
            new PartyBehaviorChangeAttempted(
                playerParty,
                forcePosition: true,
                isCurrentlyAtSea: playerParty.IsCurrentlyAtSea,
                resetMovementToHold: true));

        return
            "Visual test fixture prepared.\n" +
            $"Player party: {playerParty.StringId}\n" +
            $"Original member count: {snapshot.MemberRoster.Sum(element => element.Number)}\n" +
            $"Prepared position: {playerParty.Position.X:R},{playerParty.Position.Y:R}";
    }

    [CommandLineArgumentFunction("restore_visual_test_fixture", "coop.debug.player_captivity")]
    public static string RestoreVisualTestFixture(List<string> args)
    {
        var ctx = new CommandContext(
            "restore_visual_test_fixture",
            "Usage: coop.debug.player_captivity.restore_visual_test_fixture",
            args);

        if (!ctx.RequireServer(out var error))
            return error;

        if (!ctx.RequireArgCount(0, out error))
            return error;

        var snapshot = liveTestFixtureSnapshot;
        if (snapshot == null)
            return "No visual test fixture is prepared.";

        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "Failed to restore visual test fixture: unable to resolve party behavior snapshot service.";

        var playerParty = snapshot.PlayerParty;
        var playerHero = snapshot.PlayerCharacter.HeroObject;
        if (playerHero == null ||
            playerHero.IsPrisoner ||
            !playerParty.IsActive ||
            playerHero.PartyBelongedTo != playerParty ||
            playerParty.LeaderHero != playerHero)
            return "Failed to restore visual test fixture: player must be free in their active party.";

        if (!behaviorSnapshot.CanApply(playerParty, snapshot.Behavior))
            return "Failed to restore visual test fixture: unable to resolve the party behavior snapshot.";

        foreach (var element in playerParty.MemberRoster.GetTroopRoster()
                     .Where(element => element.Character != snapshot.PlayerCharacter)
                     .ToArray())
        {
            playerParty.MemberRoster.AddToCounts(
                element.Character,
                -element.Number,
                false,
                -element.WoundedNumber,
                -element.Xp);
        }

        foreach (var element in snapshot.MemberRoster.Where(element => element.Character != snapshot.PlayerCharacter))
            playerParty.MemberRoster.Add(element);

        playerParty.Position = snapshot.Behavior.PartyPosition;
        playerParty.IsCurrentlyAtSea = snapshot.Behavior.IsCurrentlyAtSea;
        if (!behaviorSnapshot.TryApply(playerParty, snapshot.Behavior, out _))
            return "Failed to restore visual test fixture: unable to apply the party behavior snapshot.";

        MessageBroker.Instance.Publish(
            typeof(PlayerCaptivityCommands),
            new PartyBehaviorChangeAttempted(
                playerParty,
                forcePosition: true,
                isCurrentlyAtSea: playerParty.IsCurrentlyAtSea,
                resetMovementToHold: false));

        liveTestFixtureSnapshot = null;
        return
            "Visual test fixture restored.\n" +
            $"Player party: {playerParty.StringId}\n" +
            $"Member count: {playerParty.MemberRoster.TotalManCount}\n" +
            $"Position: {playerParty.Position.X:R},{playerParty.Position.Y:R}\n" +
            $"Move mode: {playerParty.PartyMoveMode}";
    }

    private const string LiberatePrisonerUsage =
@"Usage:
  coop.debug.player_captivity.liberate_prisoner <heroId>

Example:
  coop.debug.player_captivity.liberate_prisoner lord_1_29

Runs the client-side rescued-prisoner liberation consequence for the given hero.";

    [CommandLineArgumentFunction("liberate_prisoner", "coop.debug.player_captivity")]
    public static string LiberatePrisoner(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run coop.debug.player_captivity.liberate_prisoner on a client.";

        var ctx = new CommandContext(
            "liberate_prisoner",
            LiberatePrisonerUsage,
            args);

        if (!ctx.RequireArgCount(1, out var error))
            return error;

        if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to liberate hero: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
            return "Failed to liberate hero: " + error;

        if (!hero.IsPrisoner)
            return $"Hero '{GetHeroDisplayName(hero)}' is not a prisoner.";

        var behavior = Campaign.Current?.GetCampaignBehavior<LordConversationsCampaignBehavior>();
        if (behavior == null)
            return $"Unable to find {nameof(LordConversationsCampaignBehavior)}.";

        try
        {
            MessageBroker.Instance.Publish(
                behavior,
                new PrisonerLiberationAttempted(hero));
            EndCaptivityAction.ApplyByReleasedAfterBattle(hero);
            return $"Liberated '{GetHeroDisplayName(hero)}' after battle.";
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException(
                $"Failed to liberate hero '{GetHeroDisplayName(hero)}'",
                ex);
        }
    }

    private const string PrisonerStatusUsage =
@"Usage:
  coop.debug.player_captivity.status <heroId>

Example:
  coop.debug.player_captivity.status lord_1_29

Reports the hero's current captivity state on this process.";

    [CommandLineArgumentFunction("status", "coop.debug.player_captivity")]
    public static string PrisonerStatus(List<string> args)
    {
        var ctx = new CommandContext(
            "status",
            PrisonerStatusUsage,
            args);

        if (!ctx.RequireArgCount(1, out var error))
            return error;

        if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to inspect hero: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
            return "Failed to inspect hero: " + error;

        var captor = hero.PartyBelongedToAsPrisoner?.MobileParty?.StringId ?? "none";
        return
            $"Hero: {GetHeroDisplayName(hero)} ({hero.StringId})\n" +
            $"IsPrisoner: {hero.IsPrisoner}\n" +
            $"Captor: {captor}";
    }

    private const string DiscardPlayerFromPartyScreenUsage =
@"Usage:
  coop.debug.player_captivity.discard_player_from_party_screen <heroId> <captorPartyId>

Moves a captured player into the active normal Party screen's left dismissal roster and presses Done.
Run this on the client that controls the captor after opening the Party screen.";

    [CommandLineArgumentFunction("discard_player_from_party_screen", "coop.debug.player_captivity")]
    public static string DiscardPlayerFromPartyScreen(List<string> args)
    {
        var ctx = new CommandContext(
            "discard_player_from_party_screen",
            DiscardPlayerFromPartyScreenUsage,
            args);

        if (!ModInformation.IsClient)
            return "Run this command on the client that controls the captor party.";

        if (!ctx.RequireArgCount(2, out var error))
            return error;

        if (!ctx.TryGetArg(0, "heroId", out var heroId, out error) ||
            !ctx.TryGetArg(1, "captorPartyId", out var captorPartyId, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to discard player prisoner: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var prisoner, out error))
            return "Failed to discard player prisoner: " + error;

        if (!objectManager.TryGetObject(captorPartyId, out MobileParty captor) &&
            !CommandHelpers.TryGetMobileParty(captorPartyId, out captor, out error))
            return "Failed to discard player prisoner: " + error;

        if (!prisoner.IsPrisoner || prisoner.PartyBelongedToAsPrisoner?.MobileParty != captor)
            return $"Hero '{GetHeroDisplayName(prisoner)}' is not a prisoner of '{captor.StringId}'.";

        if (captor.LeaderHero == null)
            return $"Captor party '{captor.StringId}' has no leader hero.";

        if (MobileParty.MainParty != captor)
            return "Run this command on the client that controls the captor party.";

        if (Game.Current?.GameStateManager?.ActiveState is not PartyState partyState ||
            partyState.PartyScreenLogic == null)
            return "Open the normal Party screen before running this command.";

        if (partyState.PartyScreenMode != PartyScreenHelper.PartyScreenMode.Normal)
            return $"The active Party screen is '{partyState.PartyScreenMode}', not Normal.";

        var partyScreenLogic = partyState.PartyScreenLogic;
        var leftPrisonerRoster =
            partyScreenLogic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left];
        if (partyScreenLogic.LeftOwnerParty != null ||
            leftPrisonerRoster.OwnerParty != null ||
            objectManager.TryGetId(leftPrisonerRoster, out _) ||
            partyScreenLogic.RightOwnerParty?.MobileParty != captor ||
            partyScreenLogic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right] != captor.PrisonRoster)
            return "The active Party screen is not the captor's prisoner-dismissal screen.";

        var rightPrisonerRoster =
            partyScreenLogic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right];
        var rightIndex = rightPrisonerRoster.FindIndexOfTroop(prisoner.CharacterObject);
        if (rightIndex < 0)
            return $"The active Party screen does not contain '{GetHeroDisplayName(prisoner)}' as a prisoner.";

        var prisonerElement = rightPrisonerRoster.GetElementCopyAtIndex(rightIndex);
        if (!partyScreenLogic.IsTroopTransferable(
                PartyScreenLogic.TroopType.Prisoner,
                prisoner.CharacterObject,
                (int)PartyScreenLogic.PartyRosterSide.Right))
            return $"The active Party screen does not allow '{GetHeroDisplayName(prisoner)}' to be transferred.";

        var targetIndex = partyScreenLogic.GetIndexToInsertTroop(
            PartyScreenLogic.PartyRosterSide.Left,
            PartyScreenLogic.TroopType.Prisoner,
            prisonerElement);
        var command = new PartyScreenLogic.PartyCommand();
        command.FillForTransferTroop(
            PartyScreenLogic.PartyRosterSide.Right,
            PartyScreenLogic.TroopType.Prisoner,
            prisoner.CharacterObject,
            prisonerElement.Number,
            prisonerElement.WoundedNumber,
            targetIndex);

        // PartyCharacterVM.ApplyTransfer is patched with this same scope. Drive PartyScreenLogic directly
        // so the automation exercises the real transfer history, both Done handlers, network messages,
        // rollback, and state close without synthesizing either wire payload.
        using (new Common.Util.AllowedThread())
        {
            partyScreenLogic.AddCommand(command);
            partyScreenLogic.RemoveZeroCounts();
        }

        var movedCount = leftPrisonerRoster.GetTroopCount(prisoner.CharacterObject);
        if (movedCount != prisonerElement.Number ||
            rightPrisonerRoster.GetTroopCount(prisoner.CharacterObject) != 0)
            return "PartyScreenLogic did not move the complete prisoner stack to the dismissal roster.";

        PartyScreenHelper.CloseScreen(isForced: false);
        var screenClosed = Game.Current.GameStateManager.ActiveState != partyState;

        return
            "Player prisoner discarded through the active Party screen.\n" +
            $"Hero: {GetHeroDisplayName(prisoner)}\n" +
            $"Captor StringId: {captor.StringId}\n" +
            $"TransferredCount: {movedCount}\n" +
            $"ActionPath: {nameof(PartyScreenLogic)}.{nameof(PartyScreenLogic.AddCommand)} -> " +
            $"{nameof(PartyScreenHelper)}.{nameof(PartyScreenHelper.CloseScreen)}\n" +
            $"ScreenClosed: {screenClosed}";
    }

    private const string ObservePlayerUsage =
@"Usage:
  coop.debug.player_captivity.observe_player <heroId>

Reports captivity and registered party state without mutating it.";

    [CommandLineArgumentFunction("observe_player", "coop.debug.player_captivity")]
    public static string ObservePlayer(List<string> args)
    {
        var ctx = new CommandContext("observe_player", ObservePlayerUsage, args);

        if (!ctx.RequireArgCount(1, out var error))
            return error;

        if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to observe player: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
            return "Failed to observe player: " + error;

        if (!objectManager.TryGetId(hero, out var registeredHeroId))
            return "Failed to observe player: hero is not registered.";

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return "Failed to observe player: unable to resolve player manager.";

        var player = playerManager.Players.FirstOrDefault(candidate => candidate.HeroId == registeredHeroId);
        var partyId = player?.MobilePartyId;
        MobileParty registeredParty = null;
        if (!string.IsNullOrEmpty(partyId))
            objectManager.TryGetObject(partyId, out registeredParty);
        var captorParty = hero.PartyBelongedToAsPrisoner?.MobileParty;

        var output = new StringBuilder();
        output.AppendLine($"HeroId: {registeredHeroId}");
        output.AppendLine($"IsPrisoner: {hero.IsPrisoner}");
        output.AppendLine($"HeroPartyId: {hero.PartyBelongedTo?.StringId ?? "<none>"}");
        output.AppendLine($"CaptorPartyId: {captorParty?.StringId ?? "<none>"}");
        output.AppendLine($"RegisteredPartyId: {partyId ?? "<none>"}");
        output.AppendLine($"PartyResolved: {registeredParty != null}");

        if (registeredParty != null)
        {
            output.AppendLine($"PartyActive: {registeredParty.IsActive}");
            output.AppendLine($"PartyVisible: {registeredParty.IsVisible}");
            output.AppendLine($"PartyVisualPresent: {registeredParty.Party.GetPartyVisual() != null}");
            output.AppendLine($"PartyLeaderId: {registeredParty.LeaderHero?.StringId ?? "<none>"}");
            output.AppendLine($"HeroMemberCount: {registeredParty.MemberRoster.GetTroopCount(hero.CharacterObject)}");
            output.AppendLine($"PartyMemberCount: {registeredParty.MemberRoster.TotalManCount}");
            output.AppendLine($"PartyPrisonerCount: {registeredParty.PrisonRoster.TotalManCount}");
            output.AppendLine($"PartyPosition: {registeredParty.Position.X:R},{registeredParty.Position.Y:R}");
            output.AppendLine($"PartyIsOnLand: {registeredParty.Position.IsOnLand}");
            output.AppendLine($"PartyMoveMode: {registeredParty.PartyMoveMode}");
            output.AppendLine($"MoveTargetPoint: {registeredParty.MoveTargetPoint.X:R},{registeredParty.MoveTargetPoint.Y:R}");
        }

        output.AppendLine($"CaptorPrisonerCount: {captorParty?.PrisonRoster.GetTroopCount(hero.CharacterObject) ?? 0}");
        return output.ToString();
    }

    private const string RansomPlayerAtSettlementUsage =
@"Usage:
  coop.debug.player_captivity.ransom_player_at_settlement <heroId>

Example:
  coop.debug.player_captivity.ransom_player_at_settlement Player

Ransoms the captive player hero for zero gold and releases them at a nearby neutral or allied settlement.";

    [CommandLineArgumentFunction("ransom_player_at_settlement", "coop.debug.player_captivity")]
    public static string RansomPlayerAtSettlement(List<string> args)
    {
        var ctx = new CommandContext(
            "ransom_player_at_settlement",
            RansomPlayerAtSettlementUsage,
            args);

        if (!ctx.RequireServer(out var error))
            return error;

        if (!ctx.RequireArgCount(1, out error))
            return error;

        if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to ransom hero: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
            return "Failed to ransom hero: " + error;

        if (!hero.IsPrisoner || hero.PartyBelongedToAsPrisoner == null)
            return $"Hero '{GetHeroDisplayName(hero)}' is not a prisoner.";

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.Contains(hero))
            return $"Hero '{GetHeroDisplayName(hero)}' is not a registered co-op player.";

        var captorParty = hero.PartyBelongedToAsPrisoner;
        var currentSettlement = captorParty.MobileParty?.CurrentSettlement;
        if (currentSettlement == null)
            return $"Captor for '{GetHeroDisplayName(hero)}' is not in a settlement.";

        if (!ContainerProvider.TryResolve<IPrisonerSaleProcessor>(out var prisonerSaleProcessor))
            return "Failed to ransom hero: could not resolve PrisonerSaleProcessor.";

        if (!ContainerProvider.TryResolve<IPlayerRansomReleaseSettlementProvider>(out var releaseSettlementProvider))
            return "Failed to ransom hero: could not resolve PlayerRansomReleaseSettlementProvider.";

        var releaseSettlement = releaseSettlementProvider.GetReleaseSettlement(captorParty, hero);
        var playerFaction = hero.MapFaction;
        var releaseFaction = releaseSettlement.MapFaction;
        var releaseSettlementHostile = playerFaction != null && releaseFaction != null &&
            FactionManager.IsAtWarAgainstFaction(playerFaction, releaseFaction);

        var requestedPrisoners = new TroopRoster();
        requestedPrisoners.AddToCounts(hero.CharacterObject, 1);
        var seller = captorParty.LeaderHero;
        var sellerGoldBefore = seller?.Gold ?? 0;

        prisonerSaleProcessor.Sell(captorParty, requestedPrisoners);

        var sellerGoldAfter = seller?.Gold ?? 0;
        return
            "Hero ransomed successfully.\n" +
            $"Hero: {GetHeroDisplayName(hero)}\n" +
            $"Ransom settlement: {currentSettlement.Name} ({currentSettlement.StringId})\n" +
            $"Release settlement: {releaseSettlement.Name} ({releaseSettlement.StringId})\n" +
            $"Player faction: {playerFaction?.StringId ?? "none"}\n" +
            $"Release settlement faction: {releaseFaction?.StringId ?? "none"}\n" +
            $"Release settlement hostile: {releaseSettlementHostile}\n" +
            $"Release gate X: {releaseSettlement.GatePosition.X.ToString(CultureInfo.InvariantCulture)}\n" +
            $"Release gate Y: {releaseSettlement.GatePosition.Y.ToString(CultureInfo.InvariantCulture)}\n" +
            $"Seller gold change: {sellerGoldAfter - sellerGoldBefore}";
    }

    [CommandLineArgumentFunction("captivity_state", "coop.debug.player_captivity")]
    public static string CaptivityState(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.player_captivity.captivity_state <heroId>";

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out var error))
            return "Failed to inspect captivity: " + error;

        if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error))
            return "Failed to inspect captivity: " + error;

        MobileParty playerParty = null;
        if (ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            var player = playerManager.Players.SingleOrDefault(candidate => candidate.HeroId == args[0]);
            if (player != null)
                objectManager.TryGetObject(player.MobilePartyId, out playerParty);
        }

        var result = new StringBuilder();
        result.AppendLine("HeroId=" + hero.StringId);
        result.AppendLine("IsPrisoner=" + hero.IsPrisoner);
        result.AppendLine("CaptorPartyId=" + (hero.PartyBelongedToAsPrisoner?.MobileParty?.StringId ?? "none"));
        result.AppendLine("PlayerPartyId=" + (playerParty?.StringId ?? "none"));
        result.AppendLine("PlayerPartyActive=" + (playerParty?.IsActive.ToString() ?? "none"));
        result.AppendLine("PlayerPartyLeaderHeroId=" + (playerParty?.LeaderHero?.StringId ?? "none"));
        result.AppendLine("PlayerPartyMemberCount=" + (playerParty?.MemberRoster.TotalManCount.ToString(CultureInfo.InvariantCulture) ?? "none"));
        result.AppendLine("PlayerPartyX=" + FormatCoordinate(playerParty?.Position.X));
        result.AppendLine("PlayerPartyY=" + FormatCoordinate(playerParty?.Position.Y));
        result.AppendLine("PlayerPartyIsOnLand=" + (playerParty?.Position.IsOnLand.ToString() ?? "none"));
        result.AppendLine("PlayerPartySettlementId=" + (playerParty?.CurrentSettlement?.StringId ?? "none"));
        return result.ToString();
    }

    [CommandLineArgumentFunction("party_fixture_state", "coop.debug.player_captivity")]
    public static string PartyFixtureState(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.player_captivity.party_fixture_state <partyId>";

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out var error))
            return "Failed to inspect party: " + error;

        if (!TryResolveMobileParty(objectManager, args[0], out var party, out error))
            return "Failed to inspect party: " + error;

        var result = new StringBuilder();
        result.AppendLine("PartyId=" + party.StringId);
        result.AppendLine("IsActive=" + party.IsActive);
        result.AppendLine("PositionX=" + FormatCoordinate(party.Position.X));
        result.AppendLine("PositionY=" + FormatCoordinate(party.Position.Y));
        result.AppendLine("IsOnLand=" + party.Position.IsOnLand);
        result.AppendLine("SettlementId=" + (party.CurrentSettlement?.StringId ?? "none"));
        result.AppendLine("LeaderHeroId=" + (party.LeaderHero?.StringId ?? "none"));
        result.AppendLine("LeaderGold=" + (party.LeaderHero?.Gold.ToString(CultureInfo.InvariantCulture) ?? "none"));
        result.AppendLine("MemberCount=" + party.MemberRoster.TotalManCount.ToString(CultureInfo.InvariantCulture));
        result.AppendLine("PrisonerCount=" + party.PrisonRoster.TotalManCount.ToString(CultureInfo.InvariantCulture));
        return result.ToString();
    }

    [CommandLineArgumentFunction("restore_party_fixture_state", "coop.debug.player_captivity")]
    public static string RestorePartyFixtureState(List<string> args)
    {
        const string usage = "Usage: coop.debug.player_captivity.restore_party_fixture_state <partyId> <settlementId|none> <x> <y> <isOnLand> <isActive>";
        var ctx = new CommandContext("restore_party_fixture_state", usage, args);
        if (!ctx.RequireServer(out var error))
            return error;
        if (!ctx.RequireArgCount(6, out error))
            return error;

        if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
            return "Failed to restore party: " + error;
        if (!TryResolveMobileParty(objectManager, args[0], out var party, out error))
            return "Failed to restore party: " + error;
        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return "Failed to restore party: could not resolve Network.";
        if (!objectManager.TryGetIdWithLogging(party, out var partyId))
            return "Failed to restore party: the party is not registered.";
        if (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !bool.TryParse(args[4], out var isOnLand) ||
            !bool.TryParse(args[5], out var isActive))
            return usage;

        if (args[1] == "none")
        {
            if (party.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(party);

            party.Position = new CampaignVec2(new Vec2(x, y), isOnLand);
        }
        else
        {
            var settlement = Settlement.Find(args[1]);
            if (settlement == null)
                return $"Failed to restore party: settlement '{args[1]}' not found.";

            if (party.CurrentSettlement != settlement)
            {
                if (party.CurrentSettlement != null)
                    LeaveSettlementAction.ApplyForParty(party);
                EnterSettlementAction.ApplyForParty(party, settlement);
            }
        }

        party.IsActive = isActive;
        network.SendAll(new NetworkPlayerCaptivityReleasePositionSet(partyId, party.Position));

        return "Restored party fixture state.\n" + PartyFixtureState(new List<string> { args[0] });
    }

    private static bool TryGetRandomCaptor(out MobileParty newCaptor, out string error)
    {
        newCaptor = null;
        error = null;

        if (Campaign.Current == null)
        {
            error = "Campaign.Current is null.";
            return false;
        }

        var parties = Campaign.Current.MobileParties;

        if (parties == null || parties.Count == 0)
        {
            error = "Campaign.Current.MobileParties is empty.";
            return false;
        }

        var random = new Random();

        for (var attempt = 0; attempt < parties.Count; attempt++)
        {
            var candidate = parties[random.Next(parties.Count)];

            if (candidate?.Party == null)
                continue;

            newCaptor = candidate;
            return true;
        }

        error = "Could not find a valid captor party.";
        return false;
    }

    private static bool TryResolveMobileParty(
        IObjectManager objectManager,
        string partyId,
        out MobileParty party,
        out string error)
    {
        if (objectManager.TryGetObject(partyId, out party))
        {
            error = null;
            return true;
        }

        return CommandHelpers.TryGetMobileParty(partyId, out party, out error);
    }

    private static string FormatCoordinate(float? coordinate) =>
        coordinate?.ToString("R", CultureInfo.InvariantCulture) ?? "none";

    private static List<TroopRosterElement> SnapshotRegularTroops(TroopRoster roster)
    {
        var troops = new List<TroopRosterElement>();
        for (var i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            if (element.Character?.IsHero == false && element.Number > 0)
                troops.Add(element);
        }

        return troops;
    }

    private static bool HasOtherHero(TroopRoster roster, Hero playerHero)
    {
        for (var i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            if (element.Number > 0 && element.Character?.IsHero == true && element.Character.HeroObject != playerHero)
                return true;
        }

        return false;
    }

    private sealed class CaptivityRosterFixture
    {
        public Hero PlayerHero { get; }
        public MobileParty PlayerParty { get; }
        public MobileParty CaptorParty { get; }
        public List<TroopRosterElement> RegularTroops { get; }

        public CaptivityRosterFixture(
            Hero playerHero,
            MobileParty playerParty,
            MobileParty captorParty,
            List<TroopRosterElement> regularTroops)
        {
            PlayerHero = playerHero;
            PlayerParty = playerParty;
            CaptorParty = captorParty;
            RegularTroops = regularTroops;
        }
    }

    private static string CaptureHero(Hero hero, MobileParty newCaptor)
    {
        if (hero == null)
        {
            return "Failed to capture hero: hero is null.";
        }

        if (newCaptor == null)
        {
            return "Failed to capture hero: captor party is null.";
        }

        if (newCaptor.Party == null)
        {
            return $"Failed to capture hero: MobileParty '{newCaptor.StringId}' has no Party.";
        }

        if (hero.IsPrisoner)
        {
            var currentCaptor = hero.PartyBelongedToAsPrisoner?.MobileParty?.StringId
                ?? hero.PartyBelongedTo?.StringId
                ?? "unknown";

            return
                $"Hero '{GetHeroDisplayName(hero)}' is already a prisoner.\n" +
                $"Current captor: {currentCaptor}.";
        }

        try
        {
            TakePrisonerAction.Apply(newCaptor.Party, hero);

            var captorName = newCaptor.Name?.ToString() ?? newCaptor.StringId;

            return
                "Hero captured successfully.\n" +
                $"Hero: {GetHeroDisplayName(hero)}\n" +
                $"Captor: {captorName}\n" +
                $"Captor StringId: {newCaptor.StringId}";
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException(
                $"Failed to capture hero '{GetHeroDisplayName(hero)}' by '{newCaptor.StringId}'",
                ex);
        }
    }

    private static string GetHeroDisplayName(Hero hero)
    {
        if (hero == null)
            return "null";

        var name = hero.Name?.ToString();

        if (!string.IsNullOrWhiteSpace(name))
            return name;

        return hero.StringId ?? "unknown";
    }

    private sealed class LiveTestFixtureSnapshot
    {
        public readonly MobileParty PlayerParty;
        public readonly CharacterObject PlayerCharacter;
        public readonly TroopRosterElement[] MemberRoster;
        public readonly PartyBehaviorUpdateData Behavior;

        public LiveTestFixtureSnapshot(
            MobileParty playerParty,
            CharacterObject playerCharacter,
            TroopRosterElement[] memberRoster,
            PartyBehaviorUpdateData behavior)
        {
            PlayerParty = playerParty;
            PlayerCharacter = playerCharacter;
            MemberRoster = memberRoster;
            Behavior = behavior;
        }
    }
}
