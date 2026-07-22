using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Utils.Commands;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.PartyBases.Extensions;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.PlayerCaptivityService.Commands;

internal class PlayerCaptivityCommands
{
    public static readonly ILogger Logger = LogManager.GetLogger<PlayerCaptivityCommands>();

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

Simulates the normal party screen's dummy-left prisoner discard for a captured player. Server only.";

    [CommandLineArgumentFunction("discard_player_from_party_screen", "coop.debug.player_captivity")]
    public static string DiscardPlayerFromPartyScreen(List<string> args)
    {
        var ctx = new CommandContext(
            "discard_player_from_party_screen",
            DiscardPlayerFromPartyScreenUsage,
            args);

        if (!ctx.RequireServer(out var error))
            return error;

        if (!ctx.RequireArgCount(2, out error))
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

        if (!objectManager.TryGetId(captor.LeaderHero, out var captorHeroId) ||
            !objectManager.TryGetId(prisoner.CharacterObject, out var prisonerCharacterId))
            return "Failed to resolve the registered captor or prisoner id.";

        if (!ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) ||
            !ContainerProvider.TryResolve<ITroopRosterInterface>(out var troopRosterInterface))
            return "Failed to resolve party-screen synchronization services.";

        var emptyRosterDelta = new TroopRosterData(Array.Empty<TroopRosterElementData>());
        var leftDummyDiscardDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData(prisonerCharacterId, 1, 0, 0),
        });
        var rightPrisonerDelta = new TroopRosterData(new[]
        {
            new TroopRosterElementData(prisonerCharacterId, -1, 0, 0),
        });

        var message = new NetworkCompleteDoneLogic(
            captorHeroId,
            Array.Empty<FlattenedTroop>(),
            Array.Empty<FlattenedTroop>(),
            Array.Empty<FlattenedTroop>(),
            emptyRosterDelta,
            leftDummyDiscardDelta,
            emptyRosterDelta,
            rightPrisonerDelta,
            captor.ItemRoster.ToArray(),
            new UpgradedTroopHistoryData(new()),
            null,
            null,
            0,
            0,
            0,
            true,
            captor.Position,
            Helpers.PartyScreenHelper.PartyScreenMode.Normal,
            troopRosterInterface.PackTroopRosterOrderData(captor.MemberRoster));

        messageBroker.Publish(typeof(PlayerCaptivityCommands), message);

        return
            "Player prisoner discard submitted.\n" +
            $"Hero: {GetHeroDisplayName(prisoner)}\n" +
            $"Captor StringId: {captor.StringId}";
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
