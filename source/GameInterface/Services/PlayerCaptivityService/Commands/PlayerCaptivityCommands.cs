using Common.Commands;
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Messages.LordConversations;
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
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public static readonly ILogger Logger = LogManager.GetLogger<PlayerCaptivityCommands>();
    private static CaptivityRosterFixture pendingRosterFixture;

    private static LiveTestFixtureSnapshot liveTestFixtureSnapshot;

        public sealed class PlayerCaptivityRandomCapturePlayerCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "random_capture_player";

        public string Description => "Runs the random capture player debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var ctx = new CommandContext(
                "random_capture_player",
                "Argument must not be empty.",
                new List<string>(args));

            if (!ctx.RequireServer(out var error))
                return Failed(error);


            if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to capture hero: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
                return Failed("Failed to capture hero: " + error);

            if (!TryGetRandomCaptor(out var newCaptor, out error))
                return Failed("Failed to capture hero: " + error);

            return CaptureHero(hero, newCaptor);
        }
    }

        public sealed class PlayerCaptivityCapturePlayerCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "capture_player";

        public string Description => "Runs the capture player debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
            new ExpectedArgs("captor_party_id", "The registered captor mobile party id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var ctx = new CommandContext(
                "capture_player",
                "Argument must not be empty.",
                new List<string>(args));

            if (!ctx.RequireServer(out var error))
                return Failed(error);


            if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
                return Failed(error);

            if (!ctx.TryGetArg(1, "mobilePartyId", out var captorPartyId, out error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to capture hero: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
                return Failed("Failed to capture hero: " + error);

            if (!objectManager.TryGetObject(captorPartyId, out MobileParty newCaptor)
                && !CommandHelpers.TryGetMobileParty(captorPartyId, out newCaptor, out error))
                return Failed("Failed to capture hero: " + error);

            return CaptureHero(hero, newCaptor);
        }
    }

        public sealed class PlayerCaptivityCapturePlayerFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "capture_player_fixture";

        public string Description => "Runs the capture player fixture debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
            new ExpectedArgs("captor_party_id", "The registered captor mobile party id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var ctx = new CommandContext(
                "capture_player_fixture",
                "Argument must not be empty.",
                new List<string>(args));

            if (!ctx.RequireServer(out var error))
                return Failed(error);


            if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
                return Failed(error);

            if (!ctx.TryGetArg(1, "mobilePartyId", out var captorPartyId, out error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to capture hero fixture: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
                return Failed("Failed to capture hero fixture: " + error);

            if (!objectManager.TryGetObject(captorPartyId, out MobileParty captorParty)
                && !CommandHelpers.TryGetMobileParty(captorPartyId, out captorParty, out error))
                return Failed("Failed to capture hero fixture: " + error);

            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
                return Failed("Failed to capture hero fixture: could not resolve PlayerManager.");

            var player = playerManager.Players.SingleOrDefault(candidate => candidate.HeroId == heroId);
            if (player == null)
                return Failed($"Failed to capture hero fixture: hero '{GetHeroDisplayName(hero)}' is not a registered co-op player.");

            if (!objectManager.TryGetObject(player.MobilePartyId, out MobileParty playerParty))
                return Failed($"Failed to capture hero fixture: player party '{player.MobilePartyId}' is not registered.");

            if (hero.IsPrisoner)
                return CaptureHero(hero, captorParty);

            if (hero.PartyBelongedTo != playerParty)
                return Failed("Failed to capture hero fixture: the hero does not belong to the registered player party.");

            if (captorParty == playerParty)
                return Failed("Failed to capture hero fixture: the player party cannot capture its own hero.");

            if (!playerParty.IsActive)
                return Failed("Failed to capture hero fixture: the player party is not active.");

            if (pendingRosterFixture != null)
                return Failed("Failed to capture hero fixture: another roster fixture is pending cleanup.");

            var regularTroops = SnapshotRegularTroops(playerParty.MemberRoster);
            if (HasOtherHero(playerParty.MemberRoster, hero))
                return Failed("Failed to capture hero fixture: the player party contains another hero.");

            pendingRosterFixture = new CaptivityRosterFixture(hero, playerParty, captorParty, regularTroops);
            CoopCommandResult captureResult = CaptureHero(hero, captorParty);
            if (!captureResult.Succeeded)
            {
                pendingRosterFixture = null;
                return captureResult;
            }

            if (!hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != captorParty.Party)
            {
                pendingRosterFixture = null;
                return Failed(captureResult.Output + "\nFailed to capture the player through the expected captivity path.");
            }

            if (playerParty.IsActive)
            {
                pendingRosterFixture = null;
                return Failed(captureResult.Output + "\nFailed to record fixture regular troops: the captivity handler did not park the player party.");
            }

            return Succeeded(captureResult.Output + "\nFixture regular troops recorded for cleanup: " +
                regularTroops.Sum(troop => troop.Number).ToString(CultureInfo.InvariantCulture));
        }
    }

        public sealed class PlayerCaptivityRestoreRosterFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "restore_roster_fixture";

        public string Description => "Runs the restore roster fixture debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var ctx = new CommandContext(
                "restore_roster_fixture",
                "Argument must not be empty.",
                new List<string>(args));

            if (!ctx.RequireServer(out var error))
                return Failed(error);


            if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to restore roster fixture: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
                return Failed("Failed to restore roster fixture: " + error);

            var fixture = pendingRosterFixture;
            if (fixture == null)
                return Failed("No roster fixture is pending cleanup.");

            if (fixture.PlayerHero != hero)
                return Failed($"Failed to restore roster fixture: the pending fixture belongs to '{GetHeroDisplayName(fixture.PlayerHero)}'.");

            if (hero.IsPrisoner)
                return Failed("Failed to restore roster fixture: the player hero is still a prisoner.");

            var playerHeroIndex = fixture.PlayerParty.MemberRoster.FindIndexOfTroop(fixture.PlayerHero.CharacterObject);
            if (fixture.PlayerParty.MemberRoster.TotalManCount != 1 ||
                playerHeroIndex < 0 ||
                fixture.PlayerParty.MemberRoster.GetTroopCount(fixture.PlayerHero.CharacterObject) != 1)
                return Failed("Failed to restore roster fixture: the player party is not the released hero's party of one.");

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
                    return Failed($"Failed to restore roster fixture: captor no longer holds the recorded '{troop.Character.StringId}' troops.");
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
            return Succeeded("Restored fixture regular troops: " +
                fixture.RegularTroops.Sum(troop => troop.Number).ToString(CultureInfo.InvariantCulture));
        }
    }

        public sealed class PlayerCaptivityReleasePlayerCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "release_player";

        public string Description => "Runs the release player debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var ctx = new CommandContext(
                "release_player",
                "Argument must not be empty.",
                new List<string>(args));

            if (!ctx.RequireServer(out var error))
                return Failed(error);


            if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to release hero: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
                return Failed("Failed to release hero: " + error);

            if (!hero.IsPrisoner)
                return Failed($"Hero '{GetHeroDisplayName(hero)}' is not a prisoner.");

            var captorId = hero.PartyBelongedToAsPrisoner?.MobileParty?.StringId ?? "unknown";

            try
            {
                EndCaptivityAction.ApplyByEscape(hero);

                return Succeeded("Hero released successfully.\n" +
                    $"Hero: {GetHeroDisplayName(hero)}\n" +
                    $"Former captor StringId: {captorId}");
            }
            catch (Exception ex)
            {
                return Failed(CommandHelpers.FormatException(
                    $"Failed to release hero '{GetHeroDisplayName(hero)}'",
                    ex));
            }
        }
    }

        public sealed class PlayerCaptivityPrepareVisualTestFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "prepare_visual_test_fixture";

        public string Description => "Runs the prepare visual test fixture debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
            new ExpectedArgs("captor_party_id", "The registered captor mobile party id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var ctx = new CommandContext(
                "prepare_visual_test_fixture",
                "Argument must not be empty.",
                new List<string>(args));

            if (!ctx.RequireServer(out var error))
                return Failed(error);


            if (liveTestFixtureSnapshot != null)
                return Failed("A visual test fixture is already prepared.");

            if (!ctx.TryGetArg(0, "heroId", out var heroId, out error) ||
                !ctx.TryGetArg(1, "captorPartyId", out var captorPartyId, out error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to prepare visual test fixture: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
                return Failed("Failed to prepare visual test fixture: " + error);

            if (!objectManager.TryGetObject(captorPartyId, out MobileParty captorParty) &&
                !CommandHelpers.TryGetMobileParty(captorPartyId, out captorParty, out error))
                return Failed("Failed to prepare visual test fixture: " + error);

            var playerParty = hero.PartyBelongedTo;
            if (playerParty == null || playerParty == captorParty)
                return Failed("Failed to prepare visual test fixture: player and captor parties must be distinct.");

            if (hero.IsPrisoner || playerParty.PrisonRoster.TotalManCount != 0)
                return Failed("Failed to prepare visual test fixture: player must be free and their prison roster empty.");

            if (!playerParty.IsActive ||
                playerParty.LeaderHero != hero ||
                playerParty.MemberRoster.GetTroopCount(hero.CharacterObject) != 1)
                return Failed("Failed to prepare visual test fixture: player must lead an active party containing them exactly once.");

            if (playerParty.IsCurrentlyAtSea != captorParty.IsCurrentlyAtSea ||
                playerParty.Position.IsOnLand != captorParty.Position.IsOnLand)
                return Failed("Failed to prepare visual test fixture: player and captor parties must use the same navigation layer.");

            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
                !behaviorSnapshot.TryCreate(playerParty, out var behavior))
                return Failed("Failed to prepare visual test fixture: unable to snapshot player party behavior.");

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

            return Succeeded("Visual test fixture prepared.\n" +
                $"Player party: {playerParty.StringId}\n" +
                $"Original member count: {snapshot.MemberRoster.Sum(element => element.Number)}\n" +
                $"Prepared position: {playerParty.Position.X:R},{playerParty.Position.Y:R}");
        }
    }

    public sealed class PlayerCaptivityRestoreVisualTestFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "restore_visual_test_fixture";

        public string Description => "Runs the restore visual test fixture debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var ctx = new CommandContext(
                "restore_visual_test_fixture",
                "Argument must not be empty.",
                new List<string>(args));

            if (!ctx.RequireServer(out var error))
                return Failed(error);


            var snapshot = liveTestFixtureSnapshot;
            if (snapshot == null)
                return Failed("No visual test fixture is prepared.");

            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
                return Failed("Failed to restore visual test fixture: unable to resolve party behavior snapshot service.");

            var playerParty = snapshot.PlayerParty;
            var playerHero = snapshot.PlayerCharacter.HeroObject;
            if (playerHero == null ||
                playerHero.IsPrisoner ||
                !playerParty.IsActive ||
                playerHero.PartyBelongedTo != playerParty ||
                playerParty.LeaderHero != playerHero)
                return Failed("Failed to restore visual test fixture: player must be free in their active party.");

            if (!behaviorSnapshot.CanApply(playerParty, snapshot.Behavior))
                return Failed("Failed to restore visual test fixture: unable to resolve the party behavior snapshot.");

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
                return Failed("Failed to restore visual test fixture: unable to apply the party behavior snapshot.");

            MessageBroker.Instance.Publish(
                typeof(PlayerCaptivityCommands),
                new PartyBehaviorChangeAttempted(
                    playerParty,
                    forcePosition: true,
                    isCurrentlyAtSea: playerParty.IsCurrentlyAtSea,
                    resetMovementToHold: false));

            liveTestFixtureSnapshot = null;
            return Succeeded("Visual test fixture restored.\n" +
                $"Player party: {playerParty.StringId}\n" +
                $"Member count: {playerParty.MemberRoster.TotalManCount}\n" +
                $"Position: {playerParty.Position.X:R},{playerParty.Position.Y:R}\n" +
                $"Move mode: {playerParty.PartyMoveMode}");
        }
    }

        public sealed class PlayerCaptivityLiberatePrisonerCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "liberate_prisoner";

        public string Description => "Runs the liberate prisoner debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string error;
            if (ModInformation.IsServer)
                return Failed("Run coop.debug.player_captivity.liberate_prisoner on a client.");

            var ctx = new CommandContext(
                "liberate_prisoner",
                "Argument must not be empty.",
                new List<string>(args));


            if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to liberate hero: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
                return Failed("Failed to liberate hero: " + error);

            if (!hero.IsPrisoner)
                return Failed($"Hero '{GetHeroDisplayName(hero)}' is not a prisoner.");

            var behavior = Campaign.Current?.GetCampaignBehavior<LordConversationsCampaignBehavior>();
            if (behavior == null)
                return Failed($"Unable to find {nameof(LordConversationsCampaignBehavior)}.");

            try
            {
                MessageBroker.Instance.Publish(
                    behavior,
                    new LiberateLordPrisoner(Hero.MainHero, hero));
                EndCaptivityAction.ApplyByReleasedAfterBattle(hero);
                return Succeeded($"Liberated '{GetHeroDisplayName(hero)}' after battle.");
            }
            catch (Exception ex)
            {
                return Failed(CommandHelpers.FormatException(
                    $"Failed to liberate hero '{GetHeroDisplayName(hero)}'",
                    ex));
            }
        }
    }

        public sealed class PlayerCaptivityStatusCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "status";

        public string Description => "Reports status.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string error;
            var ctx = new CommandContext(
                "status",
                "Argument must not be empty.",
                new List<string>(args));


            if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to inspect hero: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
                return Failed("Failed to inspect hero: " + error);

            var captor = hero.PartyBelongedToAsPrisoner?.MobileParty?.StringId ?? "none";
            return Succeeded($"Hero: {GetHeroDisplayName(hero)} ({hero.StringId})\n" +
                $"IsPrisoner: {hero.IsPrisoner}\n" +
                $"Captor: {captor}");
        }
    }

        public sealed class PlayerCaptivityDiscardPlayerFromPartyScreenCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "discard_player_from_party_screen";

        public string Description => "Runs the discard player from party screen debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
            new ExpectedArgs("captor_party_id", "The registered captor mobile party id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string error;
            var ctx = new CommandContext(
                "discard_player_from_party_screen",
                "Argument must not be empty.",
                new List<string>(args));

            if (!ModInformation.IsClient)
                return Failed("Run this command on the client that controls the captor party.");


            if (!ctx.TryGetArg(0, "heroId", out var heroId, out error) ||
                !ctx.TryGetArg(1, "captorPartyId", out var captorPartyId, out error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to discard player prisoner: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var prisoner, out error))
                return Failed("Failed to discard player prisoner: " + error);

            if (!objectManager.TryGetObject(captorPartyId, out MobileParty captor) &&
                !CommandHelpers.TryGetMobileParty(captorPartyId, out captor, out error))
                return Failed("Failed to discard player prisoner: " + error);

            if (!prisoner.IsPrisoner || prisoner.PartyBelongedToAsPrisoner?.MobileParty != captor)
                return Failed($"Hero '{GetHeroDisplayName(prisoner)}' is not a prisoner of '{captor.StringId}'.");

            if (captor.LeaderHero == null)
                return Failed($"Captor party '{captor.StringId}' has no leader hero.");

            if (MobileParty.MainParty != captor)
                return Failed("Run this command on the client that controls the captor party.");

            if (Game.Current?.GameStateManager?.ActiveState is not PartyState partyState ||
                partyState.PartyScreenLogic == null)
                return Failed("Open the normal Party screen before running this command.");

            if (partyState.PartyScreenMode != PartyScreenHelper.PartyScreenMode.Normal)
                return Failed($"The active Party screen is '{partyState.PartyScreenMode}', not Normal.");

            var partyScreenLogic = partyState.PartyScreenLogic;
            var leftPrisonerRoster =
                partyScreenLogic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left];
            if (partyScreenLogic.LeftOwnerParty != null ||
                leftPrisonerRoster.OwnerParty != null ||
                objectManager.TryGetId(leftPrisonerRoster, out _) ||
                partyScreenLogic.RightOwnerParty?.MobileParty != captor ||
                partyScreenLogic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right] != captor.PrisonRoster)
                return Failed("The active Party screen is not the captor's prisoner-dismissal screen.");

            var rightPrisonerRoster =
                partyScreenLogic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right];
            var rightIndex = rightPrisonerRoster.FindIndexOfTroop(prisoner.CharacterObject);
            if (rightIndex < 0)
                return Failed($"The active Party screen does not contain '{GetHeroDisplayName(prisoner)}' as a prisoner.");

            var prisonerElement = rightPrisonerRoster.GetElementCopyAtIndex(rightIndex);
            if (!partyScreenLogic.IsTroopTransferable(
                    PartyScreenLogic.TroopType.Prisoner,
                    prisoner.CharacterObject,
                    (int)PartyScreenLogic.PartyRosterSide.Right))
                return Failed($"The active Party screen does not allow '{GetHeroDisplayName(prisoner)}' to be transferred.");

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
            using (new AllowedThread())
            {
                partyScreenLogic.AddCommand(command);
                partyScreenLogic.RemoveZeroCounts();
            }

            var movedCount = leftPrisonerRoster.GetTroopCount(prisoner.CharacterObject);
            if (movedCount != prisonerElement.Number ||
                rightPrisonerRoster.GetTroopCount(prisoner.CharacterObject) != 0)
                return Failed("PartyScreenLogic did not move the complete prisoner stack to the dismissal roster.");

            PartyScreenHelper.CloseScreen(isForced: false);
            var screenClosed = Game.Current.GameStateManager.ActiveState != partyState;

            return Succeeded("Player prisoner discarded through the active Party screen.\n" +
                $"Hero: {GetHeroDisplayName(prisoner)}\n" +
                $"Captor StringId: {captor.StringId}\n" +
                $"TransferredCount: {movedCount}\n" +
                $"ActionPath: {nameof(PartyScreenLogic)}.{nameof(PartyScreenLogic.AddCommand)} -> " +
                $"{nameof(PartyScreenHelper)}.{nameof(PartyScreenHelper.CloseScreen)}\n" +
                $"ScreenClosed: {screenClosed}");
        }
    }

        public sealed class PlayerCaptivityObservePlayerCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "observe_player";

        public string Description => "Runs the observe player debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string error;
            var ctx = new CommandContext("observe_player", "Argument must not be empty.", new List<string>(args));


            if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to observe player: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
                return Failed("Failed to observe player: " + error);

            if (!objectManager.TryGetId(hero, out var registeredHeroId))
                return Failed("Failed to observe player: hero is not registered.");

            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
                return Failed("Failed to observe player: unable to resolve player manager.");

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
            return Succeeded(output.ToString());
        }
    }

        public sealed class PlayerCaptivityRansomPlayerAtSettlementCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "ransom_player_at_settlement";

        public string Description => "Runs the ransom player at settlement debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var ctx = new CommandContext(
                "ransom_player_at_settlement",
                "Argument must not be empty.",
                new List<string>(args));

            if (!ctx.RequireServer(out var error))
                return Failed(error);


            if (!ctx.TryGetArg(0, "heroId", out var heroId, out error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to ransom hero: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, heroId, out var hero, out error))
                return Failed("Failed to ransom hero: " + error);

            if (!hero.IsPrisoner || hero.PartyBelongedToAsPrisoner == null)
                return Failed($"Hero '{GetHeroDisplayName(hero)}' is not a prisoner.");

            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
                !playerManager.Contains(hero))
                return Failed($"Hero '{GetHeroDisplayName(hero)}' is not a registered co-op player.");

            var captorParty = hero.PartyBelongedToAsPrisoner;
            var currentSettlement = captorParty.MobileParty?.CurrentSettlement;
            if (currentSettlement == null)
                return Failed($"Captor for '{GetHeroDisplayName(hero)}' is not in a settlement.");

            if (!ContainerProvider.TryResolve<IPrisonerSaleProcessor>(out var prisonerSaleProcessor))
                return Failed("Failed to ransom hero: could not resolve PrisonerSaleProcessor.");

            if (!ContainerProvider.TryResolve<IPlayerRansomReleaseSettlementProvider>(out var releaseSettlementProvider))
                return Failed("Failed to ransom hero: could not resolve PlayerRansomReleaseSettlementProvider.");

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
            return Succeeded("Hero ransomed successfully.\n" +
                $"Hero: {GetHeroDisplayName(hero)}\n" +
                $"Ransom settlement: {currentSettlement.Name} ({currentSettlement.StringId})\n" +
                $"Release settlement: {releaseSettlement.Name} ({releaseSettlement.StringId})\n" +
                $"Player faction: {playerFaction?.StringId ?? "none"}\n" +
                $"Release settlement faction: {releaseFaction?.StringId ?? "none"}\n" +
                $"Release settlement hostile: {releaseSettlementHostile}\n" +
                $"Release gate X: {releaseSettlement.GatePosition.X.ToString(CultureInfo.InvariantCulture)}\n" +
                $"Release gate Y: {releaseSettlement.GatePosition.Y.ToString(CultureInfo.InvariantCulture)}\n" +
                $"Seller gold change: {sellerGoldAfter - sellerGoldBefore}");
        }
    }

    public sealed class PlayerCaptivityCaptivityStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "captivity_state";

        public string Description => "Reports captivity state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero_id", "The registered player hero id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out var error))
                return Failed("Failed to inspect captivity: " + error);

            if (!CommandHelpers.TryGetManagedObject<Hero>(objectManager, args[0], out var hero, out error))
                return Failed("Failed to inspect captivity: " + error);

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
            return Succeeded(result.ToString());
        }
    }

    public sealed class PlayerCaptivityPartyFixtureStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "party_fixture_state";

        public string Description => "Reports party fixture state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("party_id", "The registered mobile party id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out var error))
                return Failed("Failed to inspect party: " + error);

            if (!TryResolveMobileParty(objectManager, args[0], out var party, out error))
                return Failed("Failed to inspect party: " + error);

            return Succeeded(GetPartyFixtureState(party));
        }
    }

    public sealed class PlayerCaptivityRestorePartyFixtureStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.player_captivity";

        public string Name => "restore_party_fixture_state";

        public string Description => "Reports restore party fixture state.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("party_id", "The registered mobile party id.", isRequired: true),
            new ExpectedArgs("settlement_id_or_none", "The settlement id, or none.", isRequired: true),
            new ExpectedArgs("x", "The map x coordinate.", isRequired: true),
            new ExpectedArgs("y", "The map y coordinate.", isRequired: true),
            new ExpectedArgs("is_on_land", "Whether the position is on land.", isRequired: true),
            new ExpectedArgs("is_active", "Whether the party is active.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var ctx = new CommandContext("restore_party_fixture_state", "Argument must not be empty.", new List<string>(args));
            if (!ctx.RequireServer(out var error))
                return Failed(error);

            if (!CommandHelpers.TryGetObjectManager(out var objectManager, out error))
                return Failed("Failed to restore party: " + error);
            if (!TryResolveMobileParty(objectManager, args[0], out var party, out error))
                return Failed("Failed to restore party: " + error);
            if (!ContainerProvider.TryResolve<INetwork>(out var network))
                return Failed("Failed to restore party: could not resolve Network.");
            if (!objectManager.TryGetIdWithLogging(party, out var partyId))
                return Failed("Failed to restore party: the party is not registered.");
            if (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                !bool.TryParse(args[4], out var isOnLand) ||
                !bool.TryParse(args[5], out var isActive))
                return Failed("Coordinates and active-state values must use valid numeric and boolean formats.");

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
                    return Failed($"Failed to restore party: settlement '{args[1]}' not found.");

                if (party.CurrentSettlement != settlement)
                {
                    if (party.CurrentSettlement != null)
                        LeaveSettlementAction.ApplyForParty(party);
                    EnterSettlementAction.ApplyForParty(party, settlement);
                }
            }

            party.IsActive = isActive;
            network.SendAll(new NetworkPlayerCaptivityReleasePositionSet(partyId, party.Position));

            return Succeeded("Restored party fixture state.\n" + GetPartyFixtureState(party));
        }
    }

    private static string GetPartyFixtureState(MobileParty party)
    {
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

    private static CoopCommandResult CaptureHero(Hero hero, MobileParty newCaptor)
    {
        if (hero == null)
            return Failed("Failed to capture hero: hero is null.");

        if (newCaptor == null)
            return Failed("Failed to capture hero: captor party is null.");

        if (newCaptor.Party == null)
            return Failed($"Failed to capture hero: MobileParty '{newCaptor.StringId}' has no Party.");

        if (hero.IsPrisoner)
        {
            var currentCaptor = hero.PartyBelongedToAsPrisoner?.MobileParty?.StringId
                ?? hero.PartyBelongedTo?.StringId
                ?? "unknown";

            return Failed(
                $"Hero '{GetHeroDisplayName(hero)}' is already a prisoner.\n" +
                $"Current captor: {currentCaptor}.");
        }

        try
        {
            TakePrisonerAction.Apply(newCaptor.Party, hero);

            var captorName = newCaptor.Name?.ToString() ?? newCaptor.StringId;

            return Succeeded(
                "Hero captured successfully.\n" +
                $"Hero: {GetHeroDisplayName(hero)}\n" +
                $"Captor: {captorName}\n" +
                $"Captor StringId: {newCaptor.StringId}");
        }
        catch (Exception ex)
        {
            return Failed(CommandHelpers.FormatException(
                $"Failed to capture hero '{GetHeroDisplayName(hero)}' by '{newCaptor.StringId}'",
                ex));
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
