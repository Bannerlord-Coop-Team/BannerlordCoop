using Autofac;
using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using Newtonsoft.Json;
using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Party.Commands;

/// <summary>
/// Stages and restores an upgrade-ready Danustica garrison troop for live transfer tests.
/// </summary>
internal static class GarrisonTroopXpFixtureCommands
{
    private const string SettlementId = "town_ES1";
    private const string CharacterId = "imperial_infantryman";
    private const int FixtureTroopCount = 1;

    private static GarrisonFixture pendingCapture;
    private static GarrisonFixture fixture;
    private static GarrisonFixture restoredFixture;
    private static string pendingNoopRestorationControllerId;

    [CommandLineArgumentFunction("garrison_xp_fixture_capture", "coop.debug.mobileparty")]
    public static string Capture(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobileparty.garrison_xp_fixture_capture <controllerId>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 1) return usage;
        if (fixture != null || restoredFixture != null || pendingNoopRestorationControllerId != null)
            return "A garrison XP fixture lifecycle is already active.";
        if (pendingCapture != null)
        {
            if (pendingCapture.ControllerId != args[0] || !IsCaptureCurrent(pendingCapture))
                return "A different or stale garrison XP fixture capture is active.";
            return FormatCapture(pendingCapture);
        }
        if (!TryResolve(args[0], out var objectManager, out var playerManager, out var player,
            out var playerHero, out var playerClan, out var playerParty, out var settlement,
            out var garrison, out var character, out var error))
            return "Failed to capture garrison XP fixture: " + error;
        if (!playerManager.TryGetPeer(player.ControllerId, out _))
            return $"Player '{player.ControllerId}' is not connected.";
        if (playerParty.MapEvent != null || playerParty.BesiegerCamp != null ||
            playerParty.IsTransitionInProgress)
            return "Failed to capture garrison XP fixture: the player party is in a map event, siege, or navigation transition.";
        if (playerParty.Army != null || playerParty.AttachedTo != null ||
            playerParty.AttachedParties?.Count > 0)
            return "Failed to capture garrison XP fixture: the player party is attached to an army or has attached parties.";
        if (settlement.Party.MapEvent != null || settlement.SiegeEvent != null)
            return "Failed to capture garrison XP fixture: Danustica is in an active map event or siege.";
        if (settlement.OwnerClan?.Leader == null)
            return "Failed to capture garrison XP fixture: Danustica has no restorable owner.";
        if (character.UpgradeTargets.Length == 0)
            return $"Failed to capture garrison XP fixture: {CharacterId} has no upgrade target.";
        if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId) ||
            !objectManager.TryGetIdWithLogging(garrison, out var garrisonPartyId))
            return "Failed to capture garrison XP fixture: a required party is not registered.";

        var garrisonState = ReadRosterState(garrison.MemberRoster, character);
        var playerState = ReadRosterState(playerParty.MemberRoster, character);
        int upgradeXp = character.GetUpgradeXpCost(garrison.Party, 0);
        if (upgradeXp <= 0)
            return $"Failed to capture garrison XP fixture: {CharacterId} has invalid upgrade XP {upgradeXp}.";

        pendingCapture = new GarrisonFixture(
            player.ControllerId,
            playerPartyId,
            garrisonPartyId,
            playerHero,
            playerClan,
            playerParty,
            settlement,
            garrison,
            character,
            settlement.OwnerClan.Leader,
            settlement.Town.Governor,
            settlement.Town.BuildingsInProgress.ToArray(),
            settlement.Town.BoostBuildingProcess,
            settlement.Town.IsOwnerUnassigned,
            playerParty.CurrentSettlement,
            playerParty.LastVisitedSettlement,
            playerParty.Position,
            playerParty.Bearing,
            garrisonState,
            playerState,
            upgradeXp);
        return FormatCapture(pendingCapture);
    }

    [CommandLineArgumentFunction("garrison_xp_fixture_setup", "coop.debug.mobileparty")]
    public static string Setup(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobileparty.garrison_xp_fixture_setup " +
            "<controllerId> <playerPartyId> <garrisonPartyId> <originalOwnerHeroId> <originalSettlementId|none> " +
            "<originalPositionX> <originalPositionY> <originalPositionIsOnLand> " +
            "<garrisonExists> <garrisonCount> <garrisonWounded> <garrisonXp> " +
            "<playerExists> <playerCount> <playerWounded> <playerXp> <upgradeXp>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 17 ||
            !float.TryParse(args[5], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var originalPositionX) ||
            !float.TryParse(args[6], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var originalPositionY) ||
            !bool.TryParse(args[7], out var originalPositionIsOnLand) ||
            !bool.TryParse(args[8], out var expectedGarrisonExists) ||
            !int.TryParse(args[9], out var expectedGarrisonCount) ||
            !int.TryParse(args[10], out var expectedGarrisonWounded) ||
            !int.TryParse(args[11], out var expectedGarrisonXp) ||
            !bool.TryParse(args[12], out var expectedPlayerExists) ||
            !int.TryParse(args[13], out var expectedPlayerCount) ||
            !int.TryParse(args[14], out var expectedPlayerWounded) ||
            !int.TryParse(args[15], out var expectedPlayerXp) ||
            !int.TryParse(args[16], out var expectedUpgradeXp))
            return usage;
        if (fixture != null || restoredFixture != null)
            return "A garrison XP fixture lifecycle is already active.";

        pendingNoopRestorationControllerId = args[0];
        if (!TryResolve(args[0], out var objectManager, out var playerManager, out var player,
            out var playerHero, out var playerClan, out var playerParty, out var settlement,
            out var garrison, out var character, out var error))
            return "Failed to set up garrison XP fixture: " + error;
        if (!playerManager.TryGetPeer(player.ControllerId, out _))
            return $"Player '{player.ControllerId}' is not connected.";
        if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId) ||
            !objectManager.TryGetIdWithLogging(garrison, out var garrisonPartyId))
            return "Failed to set up garrison XP fixture: a required party is not registered.";

        var capturedFixture = pendingCapture;
        var originalOwner = Hero.FindFirst(hero => hero.StringId == args[3]);
        var expectedGarrisonState = new RosterState(
            expectedGarrisonExists, expectedGarrisonCount, expectedGarrisonWounded, expectedGarrisonXp);
        var expectedPlayerState = new RosterState(
            expectedPlayerExists, expectedPlayerCount, expectedPlayerWounded, expectedPlayerXp);
        int currentUpgradeXp = character.GetUpgradeXpCost(garrison.Party, 0);
        if (capturedFixture == null || capturedFixture.ControllerId != args[0] ||
            playerPartyId != args[1] || garrisonPartyId != args[2] || originalOwner == null ||
            settlement.OwnerClan?.Leader != originalOwner ||
            (playerParty.CurrentSettlement?.StringId ?? "none") != args[4] ||
            playerParty.Position.X != originalPositionX || playerParty.Position.Y != originalPositionY ||
            playerParty.Position.IsOnLand != originalPositionIsOnLand ||
            !ReadRosterState(garrison.MemberRoster, character).Equals(expectedGarrisonState) ||
            !ReadRosterState(playerParty.MemberRoster, character).Equals(expectedPlayerState) ||
            currentUpgradeXp != expectedUpgradeXp || !IsCaptureCurrent(capturedFixture))
            return "Failed to set up garrison XP fixture: the captured fixture state changed.";

        fixture = capturedFixture;
        pendingCapture = null;

        try
        {
            if (settlement.OwnerClan != playerClan)
            {
                // Only the ownership field is needed for the XP relevance precondition. Running
                // the ownership action would fire unrelated, irreversible campaign side effects.
                settlement.Town.OwnerClan = playerClan;
                settlement.Town.IsOwnerUnassigned = false;
            }

            if (playerParty.CurrentSettlement != settlement)
                playerParty.CurrentSettlement = settlement;

            SetRosterState(
                garrison.MemberRoster,
                character,
                new RosterState(true, FixtureTroopCount, 0, expectedUpgradeXp));
            pendingNoopRestorationControllerId = null;

            return JsonResult(new
            {
                controllerId = player.ControllerId,
                playerPartyId,
                garrisonPartyId,
                settlementId = SettlementId,
                settlementName = settlement.Name.ToString(),
                characterId = CharacterId,
                characterName = character.Name.ToString(),
                upgradeTargetId = character.UpgradeTargets[0].StringId,
                upgradeXp = expectedUpgradeXp,
                garrisonCount = FixtureTroopCount,
                garrisonXp = expectedUpgradeXp,
                playerCount = expectedPlayerState.Number,
                playerXp = expectedPlayerState.Xp,
                totalXp = expectedPlayerState.Xp + expectedUpgradeXp,
                playerOwnsSettlement = settlement.OwnerClan == playerClan,
                playerAtSettlement = playerParty.CurrentSettlement == settlement
            });
        }
        catch (Exception exception)
        {
            // Keep the partially staged fixture active so the contract's finally path restores
            // and verifies it instead of treating a failed setup as a no-op.
            pendingNoopRestorationControllerId = null;
            return "Failed to set up garrison XP fixture: " + exception.Message;
        }
    }

    [CommandLineArgumentFunction("garrison_xp_fixture_state", "coop.debug.mobileparty")]
    public static string State(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobileparty.garrison_xp_fixture_state " +
            "<playerPartyId> <garrisonPartyId> <characterId>";
        if (args.Count != 3) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out MobileParty playerParty))
            return $"Player party '{args[0]}' was not found.";
        if (!objectManager.TryGetObject(args[1], out MobileParty garrison))
            return $"Garrison party '{args[1]}' was not found.";
        if (!objectManager.TryGetObject(args[2], out CharacterObject character))
            return $"Character '{args[2]}' was not found.";

        var settlement = Settlement.Find(SettlementId);
        var garrisonState = ReadRosterState(garrison.MemberRoster, character);
        var playerState = ReadRosterState(playerParty.MemberRoster, character);
        int upgradeXp = character.GetUpgradeXpCost(garrison.Party, 0);
        return JsonResult(new
        {
            role = ModInformation.IsServer ? "server" : "client",
            playerPartyId = args[0],
            garrisonPartyId = args[1],
            settlementId = SettlementId,
            settlementName = settlement?.Name.ToString(),
            characterId = args[2],
            characterName = character.Name.ToString(),
            playerOwnsSettlement = settlement?.OwnerClan != null &&
                ReferenceEquals(settlement.OwnerClan, playerParty.ActualClan),
            playerAtSettlement = playerParty.CurrentSettlement == settlement,
            bearingX = playerParty.Bearing.X,
            bearingY = playerParty.Bearing.Y,
            garrisonCount = garrisonState.Number,
            garrisonWounded = garrisonState.Wounded,
            garrisonXp = garrisonState.Xp,
            garrisonUpgradeReady = garrisonState.Number > 0 && garrisonState.Xp >= upgradeXp,
            playerCount = playerState.Number,
            playerWounded = playerState.Wounded,
            playerXp = playerState.Xp,
            playerUpgradeReady = playerState.Number > 0 && playerState.Xp >= upgradeXp,
            upgradeXp,
            totalXp = garrisonState.Xp + playerState.Xp
        });
    }

    [CommandLineArgumentFunction("open_garrison_xp_fixture", "coop.debug.mobileparty")]
    public static string OpenGarrison(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobileparty.open_garrison_xp_fixture <garrisonPartyId>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 1) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out MobileParty garrison))
            return $"Garrison party '{args[0]}' was not found.";
        var settlement = Settlement.Find(SettlementId);
        if (settlement == null || garrison != settlement.Town?.GarrisonParty)
            return "The target is not Danustica's garrison.";
        if (Hero.MainHero?.CurrentSettlement != settlement)
            return "The local player is not in Danustica.";
        if (settlement.OwnerClan != Hero.MainHero.Clan)
            return "Danustica does not belong to the local player's clan.";

        PartyScreenHelper.OpenScreenAsManageTroops(garrison);
        return "GARRISON_XP_FIXTURE_SCREEN_OPENED";
    }

    [CommandLineArgumentFunction("garrison_xp_fixture_screen_state", "coop.debug.mobileparty")]
    public static string ScreenState(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobileparty.garrison_xp_fixture_screen_state " +
            "<garrisonPartyId> <characterId> <baseline|staged|committed>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 3 ||
            (args[2] != "baseline" && args[2] != "staged" && args[2] != "committed"))
            return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out MobileParty garrison))
            return $"Garrison party '{args[0]}' was not found.";
        if (!objectManager.TryGetObject(args[1], out CharacterObject character))
            return $"Character '{args[1]}' was not found.";
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState) ||
            partyState.PartyScreenLogic?.LeftOwnerParty?.MobileParty != garrison)
            return "Danustica's Manage Garrison screen is not active.";

        var logic = partyState.PartyScreenLogic;
        var leftState = ReadRosterState(logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left], character);
        var rightState = ReadRosterState(logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right], character);
        var partyVm = (ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource;
        var leftRow = partyVm?.OtherPartyTroops.FirstOrDefault(vm => vm.Character == character);
        var rightRow = partyVm?.MainPartyTroops.FirstOrDefault(vm => vm.Character == character);
        int upgradeXp = character.GetUpgradeXpCost(garrison.Party, 0);
        bool expectedStateReady;
        if (args[2] == "baseline")
        {
            expectedStateReady = partyVm != null && !logic.IsThereAnyChanges() &&
                leftState.Number == FixtureTroopCount && leftState.Xp == upgradeXp &&
                leftRow?.Troop.Number == FixtureTroopCount && leftRow.Troop.Xp == upgradeXp &&
                leftRow.NumOfReadyToUpgradeTroops == FixtureTroopCount;
        }
        else if (args[2] == "staged")
        {
            expectedStateReady = partyVm != null && logic.IsThereAnyChanges() &&
                leftState.Number == 0 && leftRow == null &&
                rightState.Number > 0 && rightRow?.Troop.Number == rightState.Number &&
                rightRow.Troop.Xp == rightState.Xp && rightRow.NumOfReadyToUpgradeTroops > 0;
        }
        else
        {
            expectedStateReady = partyVm != null && !logic.IsThereAnyChanges() &&
                leftState.Number == 0 && leftRow == null &&
                rightState.Number > 0 && rightRow?.Troop.Number == rightState.Number &&
                rightRow.Troop.Xp == rightState.Xp && rightRow.NumOfReadyToUpgradeTroops > 0;
        }
        return JsonResult(new
        {
            ready = expectedStateReady,
            expectedState = args[2],
            settlementId = SettlementId,
            settlementName = Settlement.Find(SettlementId)?.Name.ToString(),
            pending = logic.IsThereAnyChanges(),
            leftCount = leftState.Number,
            leftXp = leftState.Xp,
            leftReady = leftRow?.NumOfReadyToUpgradeTroops ?? 0,
            rightCount = rightState.Number,
            rightXp = rightState.Xp,
            rightReady = rightRow?.NumOfReadyToUpgradeTroops ?? 0,
            upgradeXp
        });
    }

    [CommandLineArgumentFunction("stage_garrison_xp_withdrawal", "coop.debug.mobileparty")]
    public static string StageWithdrawal(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobileparty.stage_garrison_xp_withdrawal " +
            "<garrisonPartyId> <characterId>";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 2) return usage;
        if (!TryGetObjectManager(out var objectManager)) return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out MobileParty garrison))
            return $"Garrison party '{args[0]}' was not found.";
        if (!objectManager.TryGetObject(args[1], out CharacterObject character))
            return $"Character '{args[1]}' was not found.";
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState) ||
            partyState.PartyScreenLogic?.LeftOwnerParty?.MobileParty != garrison)
            return "Danustica's Manage Garrison screen is not active.";

        var partyVm = (ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource;
        var row = partyVm?.OtherPartyTroops.FirstOrDefault(vm => vm.Character == character);
        if (row == null) return $"Character '{args[1]}' is not rendered in Danustica's garrison.";

        partyVm.OnTransferTroop(row, -1, FixtureTroopCount, row.Side);
        partyVm.ExecuteRemoveZeroCounts();
        return partyState.PartyScreenLogic.IsThereAnyChanges()
            ? "GARRISON_XP_WITHDRAWAL_STAGED"
            : "GARRISON_XP_WITHDRAWAL_REJECTED";
    }

    [CommandLineArgumentFunction("commit_garrison_xp_withdrawal", "coop.debug.mobileparty")]
    public static string CommitWithdrawal(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobileparty.commit_garrison_xp_withdrawal";
        if (!ModInformation.IsClient) return "Command can only be run on a client.";
        if (args.Count != 0) return usage;
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState) ||
            !partyState.PartyScreenLogic.IsThereAnyChanges())
            return "No staged garrison withdrawal is active.";
        if (!((ScreenManager.TopScreen as GauntletPartyScreen)?._dataSource is { } partyVm))
            return "No active Party screen view model.";

        partyVm.CloseScreenInternal();
        return Game.Current.GameStateManager.ActiveState is PartyState
            ? "GARRISON_XP_WITHDRAWAL_NOT_COMMITTED"
            : "GARRISON_XP_WITHDRAWAL_COMMITTED";
    }

    [CommandLineArgumentFunction("garrison_xp_fixture_restore", "coop.debug.mobileparty")]
    public static string Restore(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobileparty.garrison_xp_fixture_restore <controllerId>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 1) return usage;
        if (fixture == null && pendingCapture?.ControllerId == args[0])
        {
            pendingCapture = null;
            pendingNoopRestorationControllerId = null;
            restoredFixture = GarrisonFixture.Noop(args[0]);
            return JsonResult(new { restored = true, noFixtureCreated = true });
        }
        if (fixture == null && pendingNoopRestorationControllerId == args[0])
        {
            pendingNoopRestorationControllerId = null;
            restoredFixture = GarrisonFixture.Noop(args[0]);
            return JsonResult(new { restored = true, noFixtureCreated = true });
        }
        if (fixture == null) return "No garrison XP fixture is active.";
        if (fixture.ControllerId != args[0])
            return $"The active fixture belongs to '{fixture.ControllerId}'.";

        var currentFixture = fixture;
        RestoreFixture(currentFixture);
        bool restored = IsRestored(currentFixture);
        if (restored)
        {
            fixture = null;
            restoredFixture = currentFixture;
        }

        return JsonResult(new
        {
            restored,
            controllerId = currentFixture.ControllerId,
            playerPartyId = currentFixture.PlayerPartyId,
            garrisonPartyId = currentFixture.GarrisonPartyId,
            settlementId = SettlementId,
            characterId = CharacterId,
            playerCount = ReadRosterState(currentFixture.PlayerParty.MemberRoster, currentFixture.Character).Number,
            playerXp = ReadRosterState(currentFixture.PlayerParty.MemberRoster, currentFixture.Character).Xp,
            garrisonCount = ReadRosterState(currentFixture.Garrison.MemberRoster, currentFixture.Character).Number,
            garrisonXp = ReadRosterState(currentFixture.Garrison.MemberRoster, currentFixture.Character).Xp,
            ownerHeroId = currentFixture.Settlement.OwnerClan?.Leader?.StringId,
            playerSettlementId = currentFixture.PlayerParty.CurrentSettlement?.StringId
        });
    }

    [CommandLineArgumentFunction("garrison_xp_fixture_verify_restore", "coop.debug.mobileparty")]
    public static string VerifyRestore(List<string> args)
    {
        const string usage = "Usage: coop.debug.mobileparty.garrison_xp_fixture_verify_restore <controllerId>";
        if (!ModInformation.IsServer) return "Command can only be run on the server.";
        if (args.Count != 1) return usage;
        if (restoredFixture == null || restoredFixture.ControllerId != args[0])
            return $"No restored garrison XP fixture is awaiting verification for '{args[0]}'.";
        if (restoredFixture.NoFixtureCreated)
        {
            restoredFixture = null;
            return JsonResult(new { restored = true, noFixtureCreated = true });
        }

        var completedFixture = restoredFixture;
        bool restored = IsRestored(completedFixture);
        if (restored) restoredFixture = null;
        return JsonResult(new
        {
            restored,
            controllerId = completedFixture.ControllerId,
            settlementId = SettlementId,
            characterId = CharacterId,
            ownerHeroId = completedFixture.Settlement.OwnerClan?.Leader?.StringId,
            expectedOwnerHeroId = completedFixture.OriginalOwner.StringId,
            playerSettlementId = completedFixture.PlayerParty.CurrentSettlement?.StringId,
            expectedPlayerSettlementId = completedFixture.OriginalPlayerSettlement?.StringId,
            playerRosterRestored = ReadRosterState(
                completedFixture.PlayerParty.MemberRoster, completedFixture.Character)
                .Equals(completedFixture.OriginalPlayerState),
            garrisonRosterRestored = ReadRosterState(
                completedFixture.Garrison.MemberRoster, completedFixture.Character)
                .Equals(completedFixture.OriginalGarrisonState)
        });
    }

    private static void RestoreFixture(GarrisonFixture currentFixture)
    {
        SetRosterState(
            currentFixture.PlayerParty.MemberRoster,
            currentFixture.Character,
            currentFixture.OriginalPlayerState);
        SetRosterState(
            currentFixture.Garrison.MemberRoster,
            currentFixture.Character,
            currentFixture.OriginalGarrisonState);

        if (currentFixture.Settlement.OwnerClan?.Leader != currentFixture.OriginalOwner)
            currentFixture.Settlement.Town.OwnerClan = currentFixture.OriginalOwner.Clan;

        if (!currentFixture.Settlement.Town.BuildingsInProgress.SequenceEqual(
            currentFixture.OriginalBuildingsInProgress))
        {
            BuildingHelper.ChangeCurrentBuildingQueue(
                currentFixture.OriginalBuildingsInProgress.ToList(),
                currentFixture.Settlement.Town);
        }
        currentFixture.Settlement.Town.BoostBuildingProcess = currentFixture.OriginalBoostBuildingProcess;
        currentFixture.Settlement.Town.IsOwnerUnassigned = currentFixture.OriginalIsOwnerUnassigned;
        currentFixture.Settlement.Town.Governor = currentFixture.OriginalGovernor;

        if (currentFixture.OriginalPlayerSettlement == null)
        {
            currentFixture.PlayerParty.CurrentSettlement = null;
        }
        else if (currentFixture.PlayerParty.CurrentSettlement != currentFixture.OriginalPlayerSettlement)
        {
            currentFixture.PlayerParty.CurrentSettlement = currentFixture.OriginalPlayerSettlement;
        }
        currentFixture.PlayerParty.Position = currentFixture.OriginalPlayerPosition;
        currentFixture.PlayerParty.Bearing = currentFixture.OriginalPlayerBearing;
        currentFixture.PlayerParty.LastVisitedSettlement = currentFixture.OriginalLastVisitedSettlement;
        MessageBroker.Instance.Publish(
            typeof(GarrisonTroopXpFixtureCommands),
            new PartyBehaviorChangeAttempted(
                currentFixture.PlayerParty,
                forcePosition: true,
                isCurrentlyAtSea: currentFixture.OriginalPlayerPosition.IsOnLand == false));
    }

    private static bool IsRestored(GarrisonFixture currentFixture) =>
        currentFixture.Settlement.OwnerClan?.Leader == currentFixture.OriginalOwner &&
        currentFixture.Settlement.Town.Governor == currentFixture.OriginalGovernor &&
        currentFixture.Settlement.Town.BuildingsInProgress.SequenceEqual(
            currentFixture.OriginalBuildingsInProgress) &&
        currentFixture.Settlement.Town.BoostBuildingProcess == currentFixture.OriginalBoostBuildingProcess &&
        currentFixture.Settlement.Town.IsOwnerUnassigned == currentFixture.OriginalIsOwnerUnassigned &&
        currentFixture.PlayerParty.CurrentSettlement == currentFixture.OriginalPlayerSettlement &&
        currentFixture.PlayerParty.Position.Equals(currentFixture.OriginalPlayerPosition) &&
        currentFixture.PlayerParty.Bearing.Equals(currentFixture.OriginalPlayerBearing) &&
        currentFixture.PlayerParty.LastVisitedSettlement == currentFixture.OriginalLastVisitedSettlement &&
        ReadRosterState(currentFixture.PlayerParty.MemberRoster, currentFixture.Character)
            .Equals(currentFixture.OriginalPlayerState) &&
        ReadRosterState(currentFixture.Garrison.MemberRoster, currentFixture.Character)
            .Equals(currentFixture.OriginalGarrisonState);

    private static bool IsCaptureCurrent(GarrisonFixture currentFixture) =>
        currentFixture.Settlement.OwnerClan?.Leader == currentFixture.OriginalOwner &&
        currentFixture.Settlement.Town.Governor == currentFixture.OriginalGovernor &&
        currentFixture.Settlement.Town.BuildingsInProgress.SequenceEqual(
            currentFixture.OriginalBuildingsInProgress) &&
        currentFixture.Settlement.Town.BoostBuildingProcess == currentFixture.OriginalBoostBuildingProcess &&
        currentFixture.Settlement.Town.IsOwnerUnassigned == currentFixture.OriginalIsOwnerUnassigned &&
        currentFixture.PlayerParty.CurrentSettlement == currentFixture.OriginalPlayerSettlement &&
        currentFixture.PlayerParty.LastVisitedSettlement == currentFixture.OriginalLastVisitedSettlement &&
        currentFixture.PlayerParty.Position.Equals(currentFixture.OriginalPlayerPosition) &&
        currentFixture.PlayerParty.Bearing.Equals(currentFixture.OriginalPlayerBearing) &&
        currentFixture.PlayerParty.MapEvent == null && currentFixture.PlayerParty.BesiegerCamp == null &&
        !currentFixture.PlayerParty.IsTransitionInProgress && currentFixture.PlayerParty.Army == null &&
        currentFixture.PlayerParty.AttachedTo == null && currentFixture.PlayerParty.AttachedParties?.Count == 0 &&
        currentFixture.Settlement.Party.MapEvent == null && currentFixture.Settlement.SiegeEvent == null &&
        ReadRosterState(currentFixture.PlayerParty.MemberRoster, currentFixture.Character)
            .Equals(currentFixture.OriginalPlayerState) &&
        ReadRosterState(currentFixture.Garrison.MemberRoster, currentFixture.Character)
            .Equals(currentFixture.OriginalGarrisonState);

    private static string FormatCapture(GarrisonFixture currentFixture) => JsonResult(new
    {
        controllerId = currentFixture.ControllerId,
        playerPartyId = currentFixture.PlayerPartyId,
        garrisonPartyId = currentFixture.GarrisonPartyId,
        settlementId = SettlementId,
        characterId = CharacterId,
        originalOwnerHeroId = currentFixture.OriginalOwner.StringId,
        originalSettlementId = currentFixture.OriginalPlayerSettlement?.StringId ?? "none",
        originalPositionX = currentFixture.OriginalPlayerPosition.X,
        originalPositionY = currentFixture.OriginalPlayerPosition.Y,
        originalPositionIsOnLand = currentFixture.OriginalPlayerPosition.IsOnLand,
        originalBearingX = currentFixture.OriginalPlayerBearing.X,
        originalBearingY = currentFixture.OriginalPlayerBearing.Y,
        garrisonExists = currentFixture.OriginalGarrisonState.Exists,
        garrisonCount = currentFixture.OriginalGarrisonState.Number,
        garrisonWounded = currentFixture.OriginalGarrisonState.Wounded,
        garrisonXp = currentFixture.OriginalGarrisonState.Xp,
        playerExists = currentFixture.OriginalPlayerState.Exists,
        playerCount = currentFixture.OriginalPlayerState.Number,
        playerWounded = currentFixture.OriginalPlayerState.Wounded,
        playerXp = currentFixture.OriginalPlayerState.Xp,
        upgradeXp = currentFixture.UpgradeXp
    });

    private static RosterState ReadRosterState(TroopRoster roster, CharacterObject character)
    {
        int index = roster.FindIndexOfTroop(character);
        if (index < 0) return default;
        var element = roster.GetElementCopyAtIndex(index);
        return new RosterState(true, element.Number, element.WoundedNumber, element.Xp);
    }

    private static void SetRosterState(TroopRoster roster, CharacterObject character, RosterState state)
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

    private static bool TryResolve(
        string controllerId,
        out IObjectManager objectManager,
        out IPlayerManager playerManager,
        out Players.Data.Player player,
        out Hero playerHero,
        out Clan playerClan,
        out MobileParty playerParty,
        out Settlement settlement,
        out MobileParty garrison,
        out CharacterObject character,
        out string error)
    {
        objectManager = null;
        playerManager = null;
        player = null;
        playerHero = null;
        playerClan = null;
        playerParty = null;
        settlement = null;
        garrison = null;
        character = null;
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
        if (!objectManager.TryGetObject(player.HeroId, out playerHero) ||
            !objectManager.TryGetObject(player.ClanId, out playerClan) ||
            !objectManager.TryGetObject(player.MobilePartyId, out playerParty))
        {
            error = $"player '{controllerId}' has unresolved hero, clan, or party objects.";
            return false;
        }

        settlement = Settlement.Find(SettlementId);
        garrison = settlement?.Town?.GarrisonParty;
        if (garrison == null)
        {
            error = "Danustica has no garrison party.";
            return false;
        }
        if (!objectManager.TryGetObject(CharacterId, out character) || character.IsHero)
        {
            error = $"registered troop '{CharacterId}' was not found.";
            return false;
        }
        return true;
    }

    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        return ContainerProvider.TryResolve(out objectManager);
    }

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

    private sealed class GarrisonFixture
    {
        public string ControllerId { get; }
        public string PlayerPartyId { get; }
        public string GarrisonPartyId { get; }
        public Hero PlayerHero { get; }
        public Clan PlayerClan { get; }
        public MobileParty PlayerParty { get; }
        public Settlement Settlement { get; }
        public MobileParty Garrison { get; }
        public CharacterObject Character { get; }
        public Hero OriginalOwner { get; }
        public Hero OriginalGovernor { get; }
        public Building[] OriginalBuildingsInProgress { get; }
        public int OriginalBoostBuildingProcess { get; }
        public bool OriginalIsOwnerUnassigned { get; }
        public Settlement OriginalPlayerSettlement { get; }
        public Settlement OriginalLastVisitedSettlement { get; }
        public CampaignVec2 OriginalPlayerPosition { get; }
        public Vec2 OriginalPlayerBearing { get; }
        public RosterState OriginalGarrisonState { get; }
        public RosterState OriginalPlayerState { get; }
        public int UpgradeXp { get; }
        public bool NoFixtureCreated { get; }

        private GarrisonFixture(string controllerId)
        {
            ControllerId = controllerId;
            NoFixtureCreated = true;
        }

        public GarrisonFixture(
            string controllerId,
            string playerPartyId,
            string garrisonPartyId,
            Hero playerHero,
            Clan playerClan,
            MobileParty playerParty,
            Settlement settlement,
            MobileParty garrison,
            CharacterObject character,
            Hero originalOwner,
            Hero originalGovernor,
            Building[] originalBuildingsInProgress,
            int originalBoostBuildingProcess,
            bool originalIsOwnerUnassigned,
            Settlement originalPlayerSettlement,
            Settlement originalLastVisitedSettlement,
            CampaignVec2 originalPlayerPosition,
            Vec2 originalPlayerBearing,
            RosterState originalGarrisonState,
            RosterState originalPlayerState,
            int upgradeXp)
        {
            ControllerId = controllerId;
            PlayerPartyId = playerPartyId;
            GarrisonPartyId = garrisonPartyId;
            PlayerHero = playerHero;
            PlayerClan = playerClan;
            PlayerParty = playerParty;
            Settlement = settlement;
            Garrison = garrison;
            Character = character;
            OriginalOwner = originalOwner;
            OriginalGovernor = originalGovernor;
            OriginalBuildingsInProgress = originalBuildingsInProgress;
            OriginalBoostBuildingProcess = originalBoostBuildingProcess;
            OriginalIsOwnerUnassigned = originalIsOwnerUnassigned;
            OriginalPlayerSettlement = originalPlayerSettlement;
            OriginalLastVisitedSettlement = originalLastVisitedSettlement;
            OriginalPlayerPosition = originalPlayerPosition;
            OriginalPlayerBearing = originalPlayerBearing;
            OriginalGarrisonState = originalGarrisonState;
            OriginalPlayerState = originalPlayerState;
            UpgradeXp = upgradeXp;
        }

        public static GarrisonFixture Noop(string controllerId) => new(controllerId);
    }
}
