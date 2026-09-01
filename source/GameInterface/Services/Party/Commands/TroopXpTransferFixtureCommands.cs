using Autofac;
using Common;
using Common.Commands;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using Newtonsoft.Json;
using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Party.Commands;

/// <summary>
/// Stages and restores a player-clan party troop transfer for live XP synchronization tests.
/// </summary>
internal static class TroopXpTransferFixtureCommands
{
    private const string FixtureCompanionName = "Coop XP Transfer Fixture";
    private const int FixtureTroopCount = 1;
    private const int FixtureTroopXp = 200;

    private static TroopXpTransferCapture pendingSetupCapture;
    private static TroopXpTransferFixture pendingFixture;
    private static TroopXpTransferRestoration pendingRestorationVerification;
    private static string pendingNoopRestorationControllerId;

    public static string Capture(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobile_party.clan_party_xp_fixture_capture <controllerId>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 1) return usage;
        if (pendingSetupCapture != null || pendingFixture != null ||
            pendingRestorationVerification != null || pendingNoopRestorationControllerId != null)
            return "A clan-party XP transfer fixture lifecycle is already active.";
        if (!TryResolvePlayer(args[0], out var objectManager, out var playerManager,
            out var player, out _, out var playerClan, out var playerParty, out var error))
            return "Failed to capture clan-party XP transfer fixture: " + error;
        if (!playerManager.TryGetPeer(player.ControllerId, out _))
            return $"Player '{player.ControllerId}' is not connected.";

        var character = playerClan.Culture?.BasicTroop;
        if (character == null || character.IsHero ||
            !objectManager.TryGetIdWithLogging(character, out var characterId) ||
            !objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId))
            return "Failed to capture clan-party XP transfer fixture: the player's culture has no registered basic troop.";

        var originalPlayerState = ReadRosterState(playerParty.MemberRoster, character);
        int originalCompanionCount = playerClan.Companions.Count();
        return JsonResult(new
        {
            controllerId = player.ControllerId,
            playerPartyId,
            characterId,
            playerCount = originalPlayerState.Number,
            playerWounded = originalPlayerState.Wounded,
            playerXp = originalPlayerState.Xp,
            companionCount = originalCompanionCount
        });
    }

    public static string Setup(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobile_party.clan_party_xp_fixture_setup " +
            "<controllerId> <playerPartyId> <characterId> <playerCount> <playerWounded> <playerXp> <companionCount>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 7 ||
            !int.TryParse(args[3], out int expectedPlayerCount) ||
            !int.TryParse(args[4], out int expectedPlayerWounded) ||
            !int.TryParse(args[5], out int expectedPlayerXp) ||
            !int.TryParse(args[6], out int expectedCompanionCount))
            return usage;
        if (pendingFixture != null) return "A clan-party XP transfer fixture is already active.";
        if (pendingSetupCapture != null || pendingRestorationVerification != null ||
            pendingNoopRestorationControllerId != null)
            return "A clan-party XP transfer fixture lifecycle is already active.";
        pendingNoopRestorationControllerId = args[0];
        if (!TryResolvePlayer(args[0], out var objectManager, out var playerManager,
            out var player, out var playerHero, out var playerClan, out var playerParty, out var error))
            return "Failed to set up clan-party XP transfer fixture: " + error;
        if (!playerManager.TryGetPeer(player.ControllerId, out _))
            return $"Player '{player.ControllerId}' is not connected.";
        var template = Hero.AllAliveHeroes.FirstOrDefault(hero =>
            hero != playerHero && hero.IsWanderer && hero.CompanionOf == null);
        if (template == null)
            return "Failed to set up clan-party XP transfer fixture: no free living wanderer template is available.";

        var character = playerClan.Culture?.BasicTroop;
        if (character == null || character.IsHero ||
            !objectManager.TryGetIdWithLogging(character, out var characterId) ||
            !objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId))
            return "Failed to set up clan-party XP transfer fixture: the player's culture has no registered basic troop.";
        var originalPlayerState = ReadRosterState(playerParty.MemberRoster, character);
        int originalCompanionCount = playerClan.Companions.Count();
        if (playerPartyId != args[1] || characterId != args[2] ||
            originalPlayerState.Number != expectedPlayerCount ||
            originalPlayerState.Wounded != expectedPlayerWounded ||
            originalPlayerState.Xp != expectedPlayerXp ||
            originalCompanionCount != expectedCompanionCount)
            return "Failed to set up clan-party XP transfer fixture: the captured fixture state changed.";

        var capture = new TroopXpTransferCapture(
            player.ControllerId,
            playerPartyId,
            characterId,
            playerClan,
            playerParty,
            originalPlayerState,
            originalCompanionCount);
        pendingSetupCapture = capture;
        Hero companion = null;
        MobileParty clanParty = null;

        try
        {
            companion = HeroCreator.CreateSpecialHero(template.CharacterObject, playerHero.HomeSettlement, age: 30);
            var fixtureName = new TextObject(FixtureCompanionName);
            companion.SetName(fixtureName, fixtureName);
            companion.SetNewOccupation(Occupation.Wanderer);
            AddCompanionAction.Apply(playerClan, companion);
            AddHeroToPartyAction.Apply(companion, playerParty, true);

            clanParty = MobilePartyHelper.CreateNewClanMobileParty(companion, playerClan);
            clanParty.SetMoveModeHold();
            RemoveRegularTroops(clanParty.MemberRoster);
            clanParty.MemberRoster.AddToCounts(
                character,
                FixtureTroopCount,
                false,
                0,
                FixtureTroopXp,
                true,
                -1);

            if (clanParty.ActualClan != playerClan || clanParty.IsPlayerParty())
                throw new InvalidOperationException("The fixture party was not created as a non-player-controlled party in the player's clan.");
            if (!objectManager.TryGetIdWithLogging(companion, out var companionId) ||
                !objectManager.TryGetIdWithLogging(clanParty, out var clanPartyId))
                throw new InvalidOperationException("The fixture hero or party was not registered.");

            pendingFixture = new TroopXpTransferFixture(
                player.ControllerId,
                playerPartyId,
                clanPartyId,
                characterId,
                playerClan,
                playerParty,
                clanParty,
                companion,
                capture.OriginalPlayerState,
                capture.OriginalCompanionCount);
            pendingSetupCapture = null;
            pendingNoopRestorationControllerId = null;

            return JsonResult(new
            {
                controllerId = player.ControllerId,
                playerPartyId,
                clanPartyId,
                companionId,
                characterId,
                sourceCount = FixtureTroopCount,
                sourceXp = FixtureTroopXp,
                playerCount = capture.OriginalPlayerState.Number,
                playerXp = capture.OriginalPlayerState.Xp,
                totalXp = capture.OriginalPlayerState.Xp + FixtureTroopXp
            });
        }
        catch (Exception exception)
        {
            CleanupCreatedFixture(playerClan, clanParty, companion);
            RestoreRosterState(playerParty.MemberRoster, character, capture.OriginalPlayerState);
            return "Failed to set up clan-party XP transfer fixture: " + exception.Message;
        }
    }

    public static string State(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobile_party.clan_party_xp_fixture_state <playerPartyId> <clanPartyId> <characterId>";
        if (args.Count != 3) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out MobileParty playerParty))
            return $"Player party '{args[0]}' was not found.";
        if (!objectManager.TryGetObject(args[2], out CharacterObject character))
            return $"Character '{args[2]}' was not found.";

        var playerState = ReadRosterState(playerParty.MemberRoster, character);
        bool clanPartyResolved = objectManager.TryGetObject(args[1], out MobileParty clanParty);
        var clanPartyState = clanPartyResolved
            ? ReadRosterState(clanParty.MemberRoster, character)
            : default;

        return JsonResult(new
        {
            playerPartyId = args[0],
            clanPartyId = args[1],
            characterId = args[2],
            clanPartyResolved,
            clanPartyActive = clanPartyResolved && clanParty.IsActive,
            sameClan = clanPartyResolved && clanParty.ActualClan != null &&
                ReferenceEquals(clanParty.ActualClan, playerParty.ActualClan),
            playerCount = playerState.Number,
            playerWounded = playerState.Wounded,
            playerXp = playerState.Xp,
            clanPartyCount = clanPartyState.Number,
            clanPartyWounded = clanPartyState.Wounded,
            clanPartyXp = clanPartyState.Xp,
            totalXp = playerState.Xp + clanPartyState.Xp
        });
    }

    public static string OpenPartyScreen(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobile_party.open_clan_party_transfer <clanPartyId>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out MobileParty clanParty))
            return $"Clan party '{args[0]}' was not found.";
        if (Hero.MainHero?.PartyBelongedTo == null ||
            clanParty.ActualClan == null ||
            !ReferenceEquals(clanParty.ActualClan, Hero.MainHero.Clan))
            return "The target party does not belong to the local player's clan.";
        if (clanParty.IsPlayerParty()) return "The target party is directly controlled by a player.";

        PartyScreenHelper.OpenScreenAsManageTroopsAndPrisoners(clanParty);
        return "CLAN_PARTY_TRANSFER_SCREEN_OPENED";
    }

    public static CoopCommandResult StageTransfer(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobile_party.stage_clan_party_transfer <clanPartyId> <characterId>";
        if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");
        if (args.Count != 2) return Failed(usage);
        if (!TryGetObjectManager(out var objectManager)) return Failed("Unable to resolve ObjectManager.");
        if (!objectManager.TryGetObject(args[0], out MobileParty clanParty))
            return Failed($"Clan party '{args[0]}' was not found.");
        if (!objectManager.TryGetObject(args[1], out CharacterObject character))
            return Failed($"Character '{args[1]}' was not found.");
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState) ||
            partyState.PartyScreenLogic?.LeftOwnerParty?.MobileParty != clanParty)
            return Failed("The target clan-party transfer screen is not active.");

        var logic = partyState.PartyScreenLogic;
        var partyVm = (ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource;
        var row = partyVm?.OtherPartyTroops.FirstOrDefault(vm => vm.Character == character);
        if (row == null)
            return Failed($"Character '{args[1]}' is not rendered in the clan party's member roster.");

        partyVm.OnTransferTroop(row, -1, FixtureTroopCount, row.Side);
        partyVm.ExecuteRemoveZeroCounts();
        if (!logic.IsThereAnyChanges()) return Failed("CLAN_PARTY_TRANSFER_REJECTED");
        return Succeeded("CLAN_PARTY_TRANSFER_STAGED");
    }

    public static string TransferScreenState(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobile_party.clan_party_transfer_screen_state " +
            "<clanPartyId> <characterId> <baseline|staged>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 3 || (args[2] != "baseline" && args[2] != "staged")) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out MobileParty clanParty))
            return $"Clan party '{args[0]}' was not found.";
        if (!objectManager.TryGetObject(args[1], out CharacterObject character))
            return $"Character '{args[1]}' was not found.";
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState) ||
            partyState.PartyScreenLogic?.LeftOwnerParty?.MobileParty != clanParty)
            return "The target clan-party transfer screen is not active.";

        var logic = partyState.PartyScreenLogic;
        var leftState = ReadRosterState(
            logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left], character);
        var rightState = ReadRosterState(
            logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right], character);
        var partyVm = (ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource;
        var leftRow = partyVm?.OtherPartyTroops.FirstOrDefault(vm => vm.Character == character);
        var rightRow = partyVm?.MainPartyTroops.FirstOrDefault(vm => vm.Character == character);
        bool viewReady = partyVm != null;
        bool expectedStateReady = args[2] == "baseline"
            ? viewReady && !logic.IsThereAnyChanges() && leftState.Number == FixtureTroopCount &&
                leftState.Xp == FixtureTroopXp && leftRow?.Troop.Number == FixtureTroopCount &&
                leftRow.Troop.Xp == FixtureTroopXp
            : viewReady && logic.IsThereAnyChanges() && leftState.Number == 0 && leftRow == null &&
                rightState.Number > 0 && rightRow?.Troop.Number == rightState.Number &&
                rightRow.Troop.Xp == rightState.Xp;
        if (!expectedStateReady)
            return $"Clan-party transfer screen has not reached the expected '{args[2]}' state.";

        return JsonResult(new
        {
            viewReady,
            expectedState = args[2],
            pending = logic.IsThereAnyChanges(),
            leftCount = leftState.Number,
            leftWounded = leftState.Wounded,
            leftXp = leftState.Xp,
            rightCount = rightState.Number,
            rightWounded = rightState.Wounded,
            rightXp = rightState.Xp,
            leftVmCount = leftRow?.Troop.Number ?? 0,
            leftVmXp = leftRow?.Troop.Xp ?? 0,
            rightVmCount = rightRow?.Troop.Number ?? 0,
            rightVmXp = rightRow?.Troop.Xp ?? 0
        });
    }

    public static CoopCommandResult CommitTransfer(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobile_party.commit_clan_party_transfer";
        if (!ModInformation.IsClient) return Failed("Command can only be run on a client.");
        if (args.Count != 0) return Failed(usage);
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState) ||
            !partyState.PartyScreenLogic.IsThereAnyChanges())
            return Failed("No staged clan-party transfer is active.");
        if (!((ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource is { } partyVm))
            return Failed("No active Party screen view model.");

        // ExecuteDone opens a confirmation inquiry when the save's player party is over its limit.
        // CloseScreenInternal is the inquiry's affirmative action and always exercises DoneLogic.
        partyVm.CloseScreenInternal();
        return Game.Current.GameStateManager.ActiveState is PartyState
            ? Failed("CLAN_PARTY_TRANSFER_NOT_COMMITTED")
            : Succeeded("CLAN_PARTY_TRANSFER_COMMITTED");
    }

    public static string Restore(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobile_party.clan_party_xp_fixture_restore <controllerId>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 1) return usage;
        if (pendingFixture == null && pendingSetupCapture != null && pendingSetupCapture.ControllerId == args[0])
        {
            pendingRestorationVerification = new TroopXpTransferRestoration(pendingSetupCapture);
            pendingSetupCapture = null;
            pendingNoopRestorationControllerId = null;
            return JsonResult(new
            {
                restored = true,
                playerPartyId = pendingRestorationVerification.PlayerPartyId,
                clanPartyId = (string)null,
                characterId = pendingRestorationVerification.CharacterId,
                playerCount = pendingRestorationVerification.OriginalPlayerState.Number,
                playerWounded = pendingRestorationVerification.OriginalPlayerState.Wounded,
                playerXp = pendingRestorationVerification.OriginalPlayerState.Xp,
                companionCount = pendingRestorationVerification.OriginalCompanionCount,
                clanPartyActive = false
            });
        }
        if (pendingFixture == null && pendingNoopRestorationControllerId == args[0])
        {
            pendingRestorationVerification = new TroopXpTransferRestoration(args[0]);
            pendingNoopRestorationControllerId = null;
            return JsonResult(new { restored = true, noFixtureCreated = true });
        }
        if (pendingFixture == null) return "No clan-party XP transfer fixture is active.";
        if (pendingFixture.ControllerId != args[0])
            return $"The active fixture belongs to '{pendingFixture.ControllerId}'.";

        var fixture = pendingFixture;
        if (!TryGetObjectManager(out var objectManager) ||
            !objectManager.TryGetObject(fixture.CharacterId, out CharacterObject character))
            return "Failed to restore the fixture character.";

        RestoreRosterState(fixture.PlayerParty.MemberRoster, character, fixture.OriginalPlayerState);
        CleanupCreatedFixture(fixture.PlayerClan, fixture.ClanParty, fixture.Companion);

        var restoredPlayerState = ReadRosterState(fixture.PlayerParty.MemberRoster, character);
        int companionCount = fixture.PlayerClan.Companions.Count();
        if (!restoredPlayerState.Equals(fixture.OriginalPlayerState) ||
            companionCount != fixture.OriginalCompanionCount ||
            fixture.ClanParty.IsActive)
        {
            return JsonResult(new
            {
                restored = false,
                playerCount = restoredPlayerState.Number,
                playerWounded = restoredPlayerState.Wounded,
                playerXp = restoredPlayerState.Xp,
                expectedPlayerCount = fixture.OriginalPlayerState.Number,
                expectedPlayerWounded = fixture.OriginalPlayerState.Wounded,
                expectedPlayerXp = fixture.OriginalPlayerState.Xp,
                companionCount,
                expectedCompanionCount = fixture.OriginalCompanionCount,
                clanPartyActive = fixture.ClanParty.IsActive
            });
        }

        pendingFixture = null;
        pendingRestorationVerification = new TroopXpTransferRestoration(fixture);
        return JsonResult(new
        {
            restored = true,
            playerPartyId = fixture.PlayerPartyId,
            clanPartyId = fixture.ClanPartyId,
            characterId = fixture.CharacterId,
            playerCount = restoredPlayerState.Number,
            playerWounded = restoredPlayerState.Wounded,
            playerXp = restoredPlayerState.Xp,
            companionCount,
            clanPartyActive = false
        });
    }

    public static string VerifyRestore(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobile_party.clan_party_xp_fixture_verify_restore <controllerId>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 1) return usage;
        if (pendingRestorationVerification == null ||
            pendingRestorationVerification.ControllerId != args[0])
            return $"No restored clan-party XP transfer fixture is awaiting verification for '{args[0]}'.";
        if (pendingRestorationVerification.NoFixtureCreated)
        {
            pendingRestorationVerification = null;
            return JsonResult(new { restored = true, noFixtureCreated = true });
        }
        if (!TryGetObjectManager(out var objectManager) ||
            !objectManager.TryGetObject(pendingRestorationVerification.CharacterId, out CharacterObject character))
            return "Failed to verify the restored fixture character.";

        var fixture = pendingRestorationVerification;
        var restoredPlayerState = ReadRosterState(fixture.PlayerParty.MemberRoster, character);
        int companionCount = fixture.PlayerClan.Companions.Count();
        bool restored = restoredPlayerState.Equals(fixture.OriginalPlayerState) &&
            companionCount == fixture.OriginalCompanionCount &&
            fixture.ClanParty?.IsActive != true;
        if (restored) pendingRestorationVerification = null;

        return JsonResult(new
        {
            restored,
            playerPartyId = fixture.PlayerPartyId,
            clanPartyId = fixture.ClanPartyId,
            characterId = fixture.CharacterId,
            playerCount = restoredPlayerState.Number,
            playerWounded = restoredPlayerState.Wounded,
            playerXp = restoredPlayerState.Xp,
            expectedPlayerCount = fixture.OriginalPlayerState.Number,
            expectedPlayerWounded = fixture.OriginalPlayerState.Wounded,
            expectedPlayerXp = fixture.OriginalPlayerState.Xp,
            companionCount,
            expectedCompanionCount = fixture.OriginalCompanionCount,
            clanPartyActive = fixture.ClanParty?.IsActive == true
        });
    }

    private static void RemoveRegularTroops(TroopRoster roster)
    {
        foreach (var element in roster.data
            .Where(element => element.Character != null && !element.Character.IsHero)
            .ToArray())
        {
            roster.AddToCounts(
                element.Character,
                -element.Number,
                false,
                -element.WoundedNumber,
                -element.Xp,
                true,
                -1);
        }
    }

    private static void CleanupCreatedFixture(Clan clan, MobileParty party, Hero companion)
    {
        if (party?.IsActive == true) DestroyPartyAction.Apply(null, party);
        if (companion?.CompanionOf != null) RemoveCompanionAction.ApplyByFire(clan, companion);
        if (companion != null && companion.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
            KillCharacterAction.ApplyByRemove(companion, false, true);
    }

    private static RosterState ReadRosterState(TroopRoster roster, CharacterObject character)
    {
        int index = roster.FindIndexOfTroop(character);
        if (index < 0) return default;

        var element = roster.GetElementCopyAtIndex(index);
        return new RosterState(true, element.Number, element.WoundedNumber, element.Xp);
    }

    private static void RestoreRosterState(TroopRoster roster, CharacterObject character, RosterState state)
    {
        int index = roster.FindIndexOfTroop(character);
        if (index < 0 && state.Exists)
        {
            roster.AddToCounts(character, Math.Max(state.Number, 1), removeDepleted: false);
            index = roster.FindIndexOfTroop(character);
        }

        if (index < 0) return;

        int currentWounded = roster.GetElementWoundedNumber(index);
        if (currentWounded > state.Number) roster.SetElementWoundedNumber(index, state.Number);
        roster.SetElementNumber(index, state.Number);
        roster.SetElementWoundedNumber(index, state.Wounded);
        roster.SetElementXp(index, state.Xp);
        if (!state.Exists) roster.RemoveZeroCounts();
        roster.InitializeCachedData();
    }

    private static bool TryResolvePlayer(
        string controllerId,
        out IObjectManager objectManager,
        out IPlayerManager playerManager,
        out Players.Data.Player player,
        out Hero hero,
        out Clan clan,
        out MobileParty party,
        out string error)
    {
        objectManager = null;
        playerManager = null;
        player = null;
        hero = null;
        clan = null;
        party = null;
        error = null;

        if (!ContainerProvider.TryResolve(out objectManager) ||
            !ContainerProvider.TryResolve(out playerManager))
        {
            error = "could not resolve player services.";
            return false;
        }
        if (!playerManager.TryGetPlayer(controllerId, out player))
        {
            error = $"no registered player has controller id '{controllerId}'.";
            return false;
        }
        if (!objectManager.TryGetObject(player.HeroId, out hero) ||
            !objectManager.TryGetObject(player.ClanId, out clan) ||
            !objectManager.TryGetObject(player.MobilePartyId, out party))
        {
            error = $"player '{controllerId}' has unresolved hero, clan, or party objects.";
            return false;
        }

        return true;
    }

    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        return ContainerProvider.TryResolve(out objectManager);
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static string JsonResult(object value) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);

    private readonly struct RosterState : IEquatable<RosterState>
    {
        public readonly bool Exists;
        public readonly int Number;
        public readonly int Wounded;
        public readonly int Xp;

        public RosterState(bool exists, int number, int wounded, int xp)
        {
            Exists = exists;
            Number = number;
            Wounded = wounded;
            Xp = xp;
        }

        public bool Equals(RosterState other) =>
            Exists == other.Exists && Number == other.Number &&
            Wounded == other.Wounded && Xp == other.Xp;
    }

    private sealed class TroopXpTransferFixture
    {
        public string ControllerId { get; }
        public string PlayerPartyId { get; }
        public string ClanPartyId { get; }
        public string CharacterId { get; }
        public Clan PlayerClan { get; }
        public MobileParty PlayerParty { get; }
        public MobileParty ClanParty { get; }
        public Hero Companion { get; }
        public RosterState OriginalPlayerState { get; }
        public int OriginalCompanionCount { get; }

        public TroopXpTransferFixture(
            string controllerId,
            string playerPartyId,
            string clanPartyId,
            string characterId,
            Clan playerClan,
            MobileParty playerParty,
            MobileParty clanParty,
            Hero companion,
            RosterState originalPlayerState,
            int originalCompanionCount)
        {
            ControllerId = controllerId;
            PlayerPartyId = playerPartyId;
            ClanPartyId = clanPartyId;
            CharacterId = characterId;
            PlayerClan = playerClan;
            PlayerParty = playerParty;
            ClanParty = clanParty;
            Companion = companion;
            OriginalPlayerState = originalPlayerState;
            OriginalCompanionCount = originalCompanionCount;
        }
    }

    private sealed class TroopXpTransferCapture
    {
        public string ControllerId { get; }
        public string PlayerPartyId { get; }
        public string CharacterId { get; }
        public Clan PlayerClan { get; }
        public MobileParty PlayerParty { get; }
        public RosterState OriginalPlayerState { get; }
        public int OriginalCompanionCount { get; }

        public TroopXpTransferCapture(
            string controllerId,
            string playerPartyId,
            string characterId,
            Clan playerClan,
            MobileParty playerParty,
            RosterState originalPlayerState,
            int originalCompanionCount)
        {
            ControllerId = controllerId;
            PlayerPartyId = playerPartyId;
            CharacterId = characterId;
            PlayerClan = playerClan;
            PlayerParty = playerParty;
            OriginalPlayerState = originalPlayerState;
            OriginalCompanionCount = originalCompanionCount;
        }
    }

    private sealed class TroopXpTransferRestoration
    {
        public string ControllerId { get; }
        public string PlayerPartyId { get; }
        public string ClanPartyId { get; }
        public string CharacterId { get; }
        public Clan PlayerClan { get; }
        public MobileParty PlayerParty { get; }
        public MobileParty ClanParty { get; }
        public RosterState OriginalPlayerState { get; }
        public int OriginalCompanionCount { get; }
        public bool NoFixtureCreated { get; }

        public TroopXpTransferRestoration(string controllerId)
        {
            ControllerId = controllerId;
            NoFixtureCreated = true;
        }

        public TroopXpTransferRestoration(TroopXpTransferCapture capture)
        {
            ControllerId = capture.ControllerId;
            PlayerPartyId = capture.PlayerPartyId;
            CharacterId = capture.CharacterId;
            PlayerClan = capture.PlayerClan;
            PlayerParty = capture.PlayerParty;
            OriginalPlayerState = capture.OriginalPlayerState;
            OriginalCompanionCount = capture.OriginalCompanionCount;
        }

        public TroopXpTransferRestoration(TroopXpTransferFixture fixture)
        {
            ControllerId = fixture.ControllerId;
            PlayerPartyId = fixture.PlayerPartyId;
            ClanPartyId = fixture.ClanPartyId;
            CharacterId = fixture.CharacterId;
            PlayerClan = fixture.PlayerClan;
            PlayerParty = fixture.PlayerParty;
            ClanParty = fixture.ClanParty;
            OriginalPlayerState = fixture.OriginalPlayerState;
            OriginalCompanionCount = fixture.OriginalCompanionCount;
        }
    }
}
