#if DEBUG
using Common;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Party.Commands;

internal static class PrisonerDonationFixtureCommands
{
    private const string FixtureTroopId = "battanian_picked_warrior";
    private static PrisonerDonationFixture fixture;

    [CommandLineArgumentFunction("prisoner_donation_identity", "coop.debug.party")]
    public static string Identity(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Run this command on the owning client.";
        if (args.Count != 0)
            return "Usage: coop.debug.party.prisoner_donation_identity";
        if (Hero.MainHero == null ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetIdWithLogging(Hero.MainHero, out var heroRegistryId))
        {
            return "The local player hero is not registered.";
        }

        return "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            heroRegistryId,
            heroStringId = Hero.MainHero.StringId,
        });
    }

    [CommandLineArgumentFunction("prisoner_donation_fixture_capture", "coop.debug.party")]
    public static string Capture(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 2)
            return "Usage: coop.debug.party.prisoner_donation_fixture_capture town_ES1 <heroRegistryId>";
        if (fixture != null)
            return "A prisoner-donation fixture is already pending restoration.";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve IObjectManager.";
        var settlement = Settlement.Find(args[0]);
        if (settlement == null)
            return $"Settlement '{args[0]}' not found.";
        if (!objectManager.TryGetObjectWithLogging<Hero>(args[1], out var hero))
            return $"Hero '{args[1]}' not found.";
        if (!objectManager.TryGetObjectWithLogging<CharacterObject>(FixtureTroopId, out var troop))
            return $"Fixture troop '{FixtureTroopId}' not found.";

        var party = hero.PartyBelongedTo;
        if (party == null)
            return $"Hero '{args[1]}' has no party.";
        if (party.MapEvent != null)
            return "The fixture party must be outside a map event.";
        if (settlement.Town == null)
            return $"Settlement '{args[0]}' is not a town.";
        if (settlement.Town.GarrisonParty == null)
            return $"Settlement '{args[0]}' has no garrison party.";
        if (hero.Clan == null)
            return "The fixture player hero must belong to a clan.";
        if (party.ActualClan != hero.Clan)
            return "The fixture player party and hero must belong to the same clan.";
        if (settlement.OwnerClan == null || settlement.OwnerClan == party.ActualClan)
            return "Danustica must be owned by a different clan for the donation influence test.";
        var fixtureKingdom = hero.Clan.Kingdom ?? settlement.OwnerClan.Kingdom;
        if (fixtureKingdom == null)
            return "The fixture could not resolve a kingdom for the influence award.";

        fixture = new PrisonerDonationFixture(
            party,
            settlement,
            troop,
            CaptureRosterState(party.PrisonRoster, troop),
            CaptureRosterState(settlement.Party.PrisonRoster, troop),
            hero.Clan.Influence,
            hero.Clan.Kingdom,
            fixtureKingdom,
            party.CurrentSettlement,
            party.Position);

        return StructuredState(fixture, "captured");
    }

    [CommandLineArgumentFunction("prisoner_donation_fixture_begin", "coop.debug.party")]
    public static string Begin(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.party.prisoner_donation_fixture_begin";
        if (fixture == null)
            return "Capture the prisoner-donation fixture first.";
        if (fixture.Started)
            return "The prisoner-donation fixture mutation already ran.";

        fixture.Started = true;

        if (fixture.PlayerClan.Kingdom == null)
        {
            if (!ContainerProvider.TryResolve<IKingdomMembershipState>(out var kingdomMembershipState))
                return "Unable to resolve IKingdomMembershipState.";

            kingdomMembershipState.MoveClanToKingdom(
                previousKingdom: null,
                kingdom: fixture.FixtureKingdom,
                clan: fixture.PlayerClan,
                publishCollectionChanges: true);
            if (fixture.PlayerClan.Kingdom != fixture.FixtureKingdom)
                return "The fixture player clan could not be staged in a kingdom.";
        }

        if (fixture.PlayerParty.CurrentSettlement != fixture.Settlement)
        {
            if (fixture.PlayerParty.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(fixture.PlayerParty);
            EnterSettlementAction.ApplyForParty(
                fixture.PlayerParty,
                fixture.Settlement);
        }

        fixture.PlayerParty.PrisonRoster.AddToCounts(fixture.Troop, 1);
        return StructuredState(fixture, "started");
    }

    [CommandLineArgumentFunction("prisoner_donation_fixture_state", "coop.debug.party")]
    public static string State(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.party.prisoner_donation_fixture_state";
        if (fixture == null)
            return "The prisoner-donation fixture is not active on this instance.";

        return "PRISONER_DONATION_FIXTURE_STATE " + FormatState(fixture);
    }

    [CommandLineArgumentFunction("prisoner_donation_fixture_assert", "coop.debug.party")]
    public static string AssertFixture(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 1 ||
            (args[0] != "staged" && args[0] != "donated" && args[0] != "repeat-proof"))
        {
            return "Usage: coop.debug.party.prisoner_donation_fixture_assert " +
                   "<staged|donated|repeat-proof>";
        }
        if (fixture == null || !fixture.Started)
            return "The prisoner-donation fixture has not been staged.";

        var playerState = CaptureRosterState(
            fixture.PlayerParty.PrisonRoster,
            fixture.Troop);
        var settlementState = CaptureRosterState(
            fixture.Settlement.Party.PrisonRoster,
            fixture.Troop);
        var influence = fixture.PlayerParty.ActualClan.Influence;
        var valid = false;
        switch (args[0])
        {
            case "staged":
                valid = playerState.Number == fixture.PlayerPrisonerState.Number + 1 &&
                        settlementState.Number == fixture.SettlementPrisonerState.Number &&
                        influence == fixture.ClanInfluence;
                break;
            case "donated":
                valid = playerState.Number == fixture.PlayerPrisonerState.Number &&
                        settlementState.Number == fixture.SettlementPrisonerState.Number + 1 &&
                        influence > fixture.ClanInfluence;
                if (valid)
                    fixture.DonationInfluence = influence;
                break;
            case "repeat-proof":
                valid = fixture.DonationInfluence.HasValue &&
                        playerState.Number == fixture.PlayerPrisonerState.Number &&
                        settlementState.Number == fixture.SettlementPrisonerState.Number + 1 &&
                        influence == fixture.DonationInfluence.Value;
                break;
        }

        return valid
            ? StructuredState(fixture, args[0])
            : $"Prisoner-donation fixture assertion '{args[0]}' failed: {FormatState(fixture)}";
    }

    [CommandLineArgumentFunction("prisoner_donation_state", "coop.debug.party")]
    public static string StateById(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.party.prisoner_donation_state town_ES1 <heroRegistryId>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve IObjectManager.";
        var settlement = Settlement.Find(args[0]);
        if (settlement == null)
            return $"Settlement '{args[0]}' not found.";
        if (!objectManager.TryGetObjectWithLogging<Hero>(args[1], out var hero))
            return $"Hero '{args[1]}' not found.";
        if (!objectManager.TryGetObjectWithLogging<CharacterObject>(FixtureTroopId, out var troop))
            return $"Fixture troop '{FixtureTroopId}' not found.";
        if (hero.PartyBelongedTo == null)
            return $"Hero '{args[1]}' has no party.";

        return StructuredState(
            hero.PartyBelongedTo,
            settlement,
            troop,
            "observed");
    }

    [CommandLineArgumentFunction("prisoner_donation_open", "coop.debug.party")]
    public static string Open(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Run this command on the owning client.";
        if (args.Count != 0)
            return "Usage: coop.debug.party.prisoner_donation_open";
        if (Hero.MainHero?.PartyBelongedTo?.CurrentSettlement == null)
            return "The client player party is not in a settlement.";
        if (Game.Current.GameStateManager.ActiveState is PartyState)
            return "A Party screen is already active.";

        PartyScreenHelper.OpenScreenAsDonatePrisoners();
        return Game.Current.GameStateManager.ActiveState is PartyState
            ? "LIVE_TEST_JSON=true"
            : "The prisoner-donation Party screen did not open.";
    }

    [CommandLineArgumentFunction("prisoner_donation_stage", "coop.debug.party")]
    public static string Stage(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Run this command on the owning client.";
        if (args.Count != 0)
            return "Usage: coop.debug.party.prisoner_donation_stage";
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState))
            return "No active Party screen.";
        if (partyState.PartyScreenMode != PartyScreenHelper.PartyScreenMode.PrisonerManage)
            return "The active Party screen is not the prisoner-donation screen.";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObjectWithLogging<CharacterObject>(FixtureTroopId, out var troop))
            return $"Fixture troop '{FixtureTroopId}' not found.";

        var logic = partyState.PartyScreenLogic;
        var rightRoster = logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right];
        var sourceIndex = rightRoster.FindIndexOfTroop(troop);
        if (sourceIndex < 0)
            return $"Fixture troop '{FixtureTroopId}' is not in the player prisoner roster.";

        var element = rightRoster.GetElementCopyAtIndex(sourceIndex);
        var targetIndex = logic.GetIndexToInsertTroop(
            PartyScreenLogic.PartyRosterSide.Left,
            PartyScreenLogic.TroopType.Prisoner,
            element);
        var command = new PartyScreenLogic.PartyCommand();
        command.FillForTransferTroop(
            PartyScreenLogic.PartyRosterSide.Right,
            PartyScreenLogic.TroopType.Prisoner,
            troop,
            totalNumber: 1,
            woundedNumber: 0,
            targetIndex: targetIndex);
        if (!logic.ValidateCommand(command))
            return "The prisoner donation transfer was rejected.";

        using (new AllowedThread())
        {
            logic.AddCommand(command);
            logic.RemoveZeroCounts();
        }

        return "LIVE_TEST_JSON=true";
    }

    [CommandLineArgumentFunction("prisoner_donation_commit", "coop.debug.party")]
    public static string Commit(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Run this command on the owning client.";
        if (args.Count != 0)
            return "Usage: coop.debug.party.prisoner_donation_commit";
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState partyState))
            return "No active Party screen.";

        PartyScreenHelper.CloseScreen(isForced: false);
        return Game.Current.GameStateManager.ActiveState == partyState
            ? "The prisoner donation commit was rejected."
            : "LIVE_TEST_JSON=true";
    }

    [CommandLineArgumentFunction("prisoner_donation_close", "coop.debug.party")]
    public static string Close(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Run this command on the owning client.";
        if (args.Count != 0)
            return "Usage: coop.debug.party.prisoner_donation_close";
        if (!(Game.Current?.GameStateManager?.ActiveState is PartyState))
            return "No active Party screen.";

        PartyScreenHelper.CloseScreen(isForced: true, fromCancel: true);
        return "LIVE_TEST_JSON=true";
    }

    [CommandLineArgumentFunction("prisoner_donation_open_influence", "coop.debug.party")]
    public static string OpenInfluence(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Run this command on the owning client.";
        if (args.Count != 0)
            return "Usage: coop.debug.party.prisoner_donation_open_influence";
        if (Game.Current?.GameStateManager == null || Hero.MainHero == null)
            return "The Clan screen is unavailable.";
        if (!(Game.Current.GameStateManager.ActiveState is ClanState))
        {
            Game.Current.GameStateManager.PushState(
                Game.Current.GameStateManager.CreateState<ClanState>(),
                0);
        }

        return Game.Current.GameStateManager.ActiveState is ClanState
            ? "LIVE_TEST_JSON=true"
            : "The Clan screen did not open.";
    }

    [CommandLineArgumentFunction("prisoner_donation_fixture_restore", "coop.debug.party")]
    public static string Restore(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.party.prisoner_donation_fixture_restore";
        if (fixture == null)
            return "The prisoner-donation fixture is not active.";

        var activeFixture = fixture;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<INetwork>(out var network) ||
            !ContainerProvider.TryResolve<ISendCoalescer>(out var sendCoalescer) ||
            !objectManager.TryGetIdWithLogging(activeFixture.PlayerParty.PrisonRoster, out var playerRosterId) ||
            !objectManager.TryGetIdWithLogging(activeFixture.Settlement.Party.PrisonRoster, out var settlementRosterId) ||
            !objectManager.TryGetIdWithLogging(activeFixture.Troop, out var troopId))
        {
            return "Unable to resolve the prisoner-donation fixture restoration services.";
        }

        RestoreRosterState(
            activeFixture.PlayerParty.PrisonRoster,
            activeFixture.Troop,
            activeFixture.PlayerPrisonerState);
        RestoreRosterState(
            activeFixture.Settlement.Party.PrisonRoster,
            activeFixture.Troop,
            activeFixture.SettlementPrisonerState);
        RepublishRosterElement(
            activeFixture.PlayerParty.PrisonRoster,
            activeFixture.Troop,
            playerRosterId,
            troopId,
            network,
            sendCoalescer);
        RepublishRosterElement(
            activeFixture.Settlement.Party.PrisonRoster,
            activeFixture.Troop,
            settlementRosterId,
            troopId,
            network,
            sendCoalescer);

        if (activeFixture.PlayerParty.CurrentSettlement != activeFixture.OriginalSettlement)
        {
            if (activeFixture.PlayerParty.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(activeFixture.PlayerParty);

            if (activeFixture.OriginalSettlement != null)
                EnterSettlementAction.ApplyForParty(
                    activeFixture.PlayerParty,
                    activeFixture.OriginalSettlement);
            else
                activeFixture.PlayerParty.Position = activeFixture.OriginalPosition;
        }

        if (activeFixture.PlayerClan.Kingdom != activeFixture.OriginalKingdom)
        {
            if (!ContainerProvider.TryResolve<IKingdomMembershipState>(out var kingdomMembershipState))
                return "Unable to resolve IKingdomMembershipState during fixture restoration.";

            kingdomMembershipState.MoveClanToKingdom(
                activeFixture.PlayerClan.Kingdom,
                activeFixture.OriginalKingdom,
                activeFixture.PlayerClan,
                publishCollectionChanges: true,
                republishExistingCollections: true);
            if (activeFixture.PlayerClan.Kingdom != activeFixture.OriginalKingdom)
                return "The fixture player clan kingdom could not be restored.";
        }

        activeFixture.PlayerClan.Influence = activeFixture.ClanInfluence;
        fixture = null;
        return "LIVE_TEST_JSON=true";
    }

    [CommandLineArgumentFunction("prisoner_donation_fixture_verify", "coop.debug.party")]
    public static string Verify(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Run this command on the owning client.";
        if (args.Count != 6 ||
            !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expectedPlayerCount) ||
            !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expectedSettlementCount) ||
            !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var expectedInfluence))
        {
            return "Usage: coop.debug.party.prisoner_donation_fixture_verify " +
                   "town_ES1 <heroRegistryId> <playerCount> <settlementCount> <influence> <kingdomId>";
        }
        var settlement = Settlement.Find(args[0]);
        if (settlement == null ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObjectWithLogging<Hero>(args[1], out var hero) ||
            !objectManager.TryGetObjectWithLogging<CharacterObject>(FixtureTroopId, out var troop) ||
            hero.PartyBelongedTo == null)
        {
            return "The restored prisoner-donation fixture could not be resolved.";
        }

        var playerCount = hero.PartyBelongedTo.PrisonRoster.GetTroopCount(troop);
        var settlementCount = settlement.Party.PrisonRoster.GetTroopCount(troop);
        var kingdomId = hero.Clan?.Kingdom?.StringId ?? "none";
        var restored = playerCount == expectedPlayerCount &&
                       settlementCount == expectedSettlementCount &&
                       hero.Clan.Influence == expectedInfluence &&
                       kingdomId == args[5];

        if (Game.Current.GameStateManager.ActiveState is PartyState)
            PartyScreenHelper.CloseScreen(isForced: true, fromCancel: true);
        else if (Game.Current.GameStateManager.ActiveState is ClanState)
            Game.Current.GameStateManager.PopState(0);

        return restored
            ? "LIVE_TEST_JSON=true"
            : $"Fixture restoration failed: playerCount={playerCount}/{expectedPlayerCount} " +
              $"settlementCount={settlementCount}/{expectedSettlementCount} " +
              $"influence={hero.Clan.Influence.ToString("R", CultureInfo.InvariantCulture)}/" +
              expectedInfluence.ToString("R", CultureInfo.InvariantCulture) + " " +
              $"kingdom={kingdomId}/{args[5]}";
    }

    private static string FormatState(PrisonerDonationFixture activeFixture)
    {
        var playerState = CaptureRosterState(
            activeFixture.PlayerParty.PrisonRoster,
            activeFixture.Troop);
        var settlementState = CaptureRosterState(
            activeFixture.Settlement.Party.PrisonRoster,
            activeFixture.Troop);
        return
            $"hero={activeFixture.PlayerParty.LeaderHero.StringId} " +
            $"settlement={activeFixture.Settlement.StringId} " +
            $"troop={activeFixture.Troop.StringId} " +
            $"playerCount={playerState.Number} " +
            $"settlementCount={settlementState.Number} " +
            $"influence={activeFixture.PlayerParty.ActualClan.Influence.ToString("R", CultureInfo.InvariantCulture)}";
    }

    private static string StructuredState(
        PrisonerDonationFixture activeFixture,
        string status)
        => StructuredState(
            activeFixture.PlayerParty,
            activeFixture.Settlement,
            activeFixture.Troop,
            status);

    private static string StructuredState(
        MobileParty playerParty,
        Settlement settlement,
        CharacterObject troop,
        string status)
    {
        var playerState = CaptureRosterState(playerParty.PrisonRoster, troop);
        var settlementState = CaptureRosterState(settlement.Party.PrisonRoster, troop);
        return "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            status,
            heroId = playerParty.LeaderHero.StringId,
            settlementId = settlement.StringId,
            troopId = troop.StringId,
            playerCount = playerState.Number,
            settlementCount = settlementState.Number,
            influence = playerParty.ActualClan.Influence,
            kingdomId = playerParty.ActualClan.Kingdom?.StringId ?? "none",
        });
    }

    private static RosterState CaptureRosterState(TroopRoster roster, CharacterObject troop)
    {
        var index = roster.FindIndexOfTroop(troop);
        return index < 0
            ? default
            : new RosterState(
                roster.GetTroopCount(troop),
                roster.GetElementWoundedNumber(index),
                roster.GetElementXp(index));
    }

    private static void RestoreRosterState(
        TroopRoster roster,
        CharacterObject troop,
        RosterState expected)
    {
        var current = CaptureRosterState(roster, troop);
        roster.AddToCounts(
            troop,
            expected.Number - current.Number,
            false,
            expected.Wounded - current.Wounded,
            expected.Xp - current.Xp,
            true);
    }

    private static void RepublishRosterElement(
        TroopRoster roster,
        CharacterObject troop,
        string rosterId,
        string troopId,
        INetwork network,
        ISendCoalescer sendCoalescer)
    {
        sendCoalescer.FlushInstance(Compact(rosterId, typeof(TroopRoster)), network);

        var index = roster.FindIndexOfTroop(troop);
        var element = index >= 0
            ? roster.GetElementCopyAtIndex(index)
            : default;
        network.SendAll(new NetworkTroopRosterSetWoundedNumber(
            rosterId,
            troopId,
            index >= 0 ? element.WoundedNumber : 0));
        network.SendAll(new NetworkTroopRosterSetNumber(
            rosterId,
            troopId,
            index >= 0 ? element.Number : 0));
        if (index >= 0)
        {
            network.SendAll(new NetworkTroopRosterElementBatch(
                rosterId,
                troopId,
                new[] { TroopRosterElementOperation.SetXp(element.Xp) }));
        }

        network.SendAll(new NetworkTroopRosterRemoveZeroCounts(rosterId));
    }

    private sealed class PrisonerDonationFixture
    {
        public MobileParty PlayerParty { get; }
        public Settlement Settlement { get; }
        public CharacterObject Troop { get; }
        public RosterState PlayerPrisonerState { get; }
        public RosterState SettlementPrisonerState { get; }
        public float ClanInfluence { get; }
        public Clan PlayerClan { get; }
        public Kingdom OriginalKingdom { get; }
        public Kingdom FixtureKingdom { get; }
        public Settlement OriginalSettlement { get; }
        public CampaignVec2 OriginalPosition { get; }
        public bool Started { get; set; }
        public float? DonationInfluence { get; set; }

        public PrisonerDonationFixture(
            MobileParty playerParty,
            Settlement settlement,
            CharacterObject troop,
            RosterState playerPrisonerState,
            RosterState settlementPrisonerState,
            float clanInfluence,
            Kingdom originalKingdom,
            Kingdom fixtureKingdom,
            Settlement originalSettlement,
            CampaignVec2 originalPosition)
        {
            PlayerParty = playerParty;
            Settlement = settlement;
            Troop = troop;
            PlayerPrisonerState = playerPrisonerState;
            SettlementPrisonerState = settlementPrisonerState;
            ClanInfluence = clanInfluence;
            PlayerClan = playerParty.ActualClan;
            OriginalKingdom = originalKingdom;
            FixtureKingdom = fixtureKingdom;
            OriginalSettlement = originalSettlement;
            OriginalPosition = originalPosition;
        }
    }

    private readonly struct RosterState
    {
        public int Number { get; }
        public int Wounded { get; }
        public int Xp { get; }

        public RosterState(int number, int wounded, int xp)
        {
            Number = number;
            Wounded = wounded;
            Xp = xp;
        }
    }
}
#endif
