using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Armies.Commands;

public class ArmySiegeWaitFixtureCommands
{
    private sealed class FixtureTimeGuard
    {
        public bool CanUnpause() => fixture == null;
    }

    private sealed class SettlementSnapshot
    {
        public Settlement Settlement;
        public MobileParty LastAttackerParty;
        public CampaignTime LastThreatTime;
        public SiegeEvent.SiegeEnginesContainer SiegeEngines;
        public MBList<SiegeEvent.SiegeEngineMissile> SiegeEngineMissiles;
        public int NumberOfTroopsKilledOnSide;
        public SiegeStrategy SiegeStrategy;
        public Settlement.SiegeState CurrentSiegeState;
    }

    private sealed class ClanMembershipSnapshot
    {
        public Clan Clan;
        public Kingdom Kingdom;
        public IFaction MapFaction;
        public bool IsUnderMercenaryService;
        public int FiefCount;
        public Kingdom[] CollectionKingdoms;
        public MBList<Clan> FixtureKingdomClanList;
        public Clan[] FixtureKingdomClans;
    }

    private sealed class Fixture
    {
        public string ControllerId;
        public string ObserverControllerId;
        public Settlement Settlement;
        public MobileParty PlayerParty;
        public MobileParty LeaderParty;
        public string PlayerPartyId;
        public string LeaderPartyId;
        public ClanMembershipSnapshot PlayerClanMembership;
        public Kingdom FixtureKingdom;
        public int FixtureKingdomClanCount;
        public Army Army;
        public SiegeEvent SiegeEvent;
        public BesiegerCamp BesiegerCamp;
        public Hero SettlementOwnerHero;
        public Hero LeaderHero;
        public int OwnerLeaderRelation;
        public bool PlayerWasActive;
        public bool PlayerDisorganized;
        public bool LeaderDisorganized;
        public CampaignTime PlayerDisorganizedUntilTime;
        public CampaignTime LeaderDisorganizedUntilTime;
        public bool LeaderRethinkAtNextHourlyTick;
        public PartyBehaviorUpdateData PlayerBehavior;
        public PartyBehaviorUpdateData LeaderBehavior;
        public SettlementSnapshot[] SettlementSnapshots;
        public TimeControlEnum OriginalTimeControl;
        public bool TimePolicyAdded;
        public bool TimeRestored;
    }

    private sealed class ClientFixture
    {
        public Hero LeaderHero;
        public bool LeaderHasMet;
        public CampaignTime LeaderLastMeetingTime;
    }

    private sealed class ClientStateFixture
    {
        public Settlement Settlement;
        public MobileParty PlayerParty;
        public MobileParty LeaderParty;
        public bool PlayerWasActive;
        public ClanMembershipSnapshot PlayerClanMembership;
        public Kingdom FixtureKingdom;
        public int FixtureKingdomClanCount;
        public bool LeaderRethinkAtNextHourlyTick;
        public SettlementSnapshot[] SettlementSnapshots;
    }

    private static Fixture fixture;
    private static Fixture lastRestoredFixture;
    private static ClientFixture clientFixture;
    private static ClientFixture lastRestoredClientFixture;
    private static ClientStateFixture clientStateFixture;
    private static ClientStateFixture lastRestoredClientStateFixture;
    private static readonly FixtureTimeGuard TimeGuard = new FixtureTimeGuard();
    private static readonly Func<bool> TimeUnpausePolicy = TimeGuard.CanUnpause;

    [CommandLineArgumentFunction("siege_wait_fixture_preflight", "coop.debug.army")]
    public static string Preflight(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 2)
            return "Usage: coop.debug.army.siege_wait_fixture_preflight <controllerId> <observerControllerId>";
        if (fixture != null)
            return "An army siege-wait fixture is already active.";
        if (!TryGetConnectedPlayerParty(args[0], out var playerParty, out var error) ||
            !TryGetConnectedPlayerParty(args[1], out _, out error))
            return error;
        if (!TryGetFixturePlayerClan(playerParty, out var playerClan, out error))
            return error;

        var settlement = Settlement.All.FirstOrDefault(candidate => candidate.StringId == "castle_EW1");
        if (settlement == null)
            return "Garontor Castle (castle_EW1) was not found.";
        if (settlement.SiegeEvent != null)
            return "Garontor Castle is already under siege.";

        var leader = FindEligibleLeader(playerClan, settlement);
        if (leader == null)
        {
            return "No hostile AI kingdom lord outside an army is available to lead the Garontor siege. " +
                DescribeLeaderCandidates(playerClan, settlement);
        }
        var fixtureKingdom = (Kingdom)leader.MapFaction;
        if (!TryValidateMembershipFixture(playerClan, fixtureKingdom, out error))
            return error;
        if (settlement.OwnerClan?.Leader == null)
            return "Garontor Castle does not have an owner hero whose relation can be restored.";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId) ||
            !objectManager.TryGetIdWithLogging(leader, out var leaderPartyId))
        {
            return "Unable to capture the registered player or AI leader party ids.";
        }

        return $"Army siege-wait fixture preflight: ready={IsFixturePartyReady(playerParty)}, " +
            $"recoverable={IsFixturePartyRecoverable(playerParty)}, settlement={settlement.StringId}, " +
            $"joiningPartyId={playerPartyId}, leaderPartyId={leaderPartyId}, " +
            $"playerClanId={playerClan.StringId}, originalKingdomId={playerClan.Kingdom?.StringId ?? "none"}, " +
            $"leaderKingdomId={fixtureKingdom.StringId}, leaderKingdomClanCount={fixtureKingdom.Clans.Count}, " +
            $"player={DescribeParty(playerParty)}, leader={DescribeParty(leader)}.";
    }

    [CommandLineArgumentFunction("siege_wait_fixture_snapshot_client", "coop.debug.army")]
    public static string SnapshotClient(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 2)
        {
            return "Usage: coop.debug.army.siege_wait_fixture_snapshot_client " +
                "<joiningPartyId> <leaderPartyId>";
        }
        if (clientStateFixture != null)
            return "A client army siege-wait fixture snapshot is already active.";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(args[0], out var playerParty))
            return $"Unable to resolve joining party {args[0]}.";
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(args[1], out var leader))
            return $"Unable to resolve AI leader party {args[1]}.";
        if (!TryGetFixturePlayerClan(playerParty, out var playerClan, out var error))
            return error;
        if (!(leader.MapFaction is Kingdom fixtureKingdom))
            return "The AI leader does not belong to a kingdom.";
        if (!TryValidateMembershipFixture(playerClan, fixtureKingdom, out error))
            return error;

        var settlement = Settlement.All.FirstOrDefault(candidate => candidate.StringId == "castle_EW1");
        if (settlement == null)
            return "Garontor Castle (castle_EW1) was not found.";
        if (settlement.SiegeEvent != null)
            return "Garontor Castle is already under siege.";

        var settlementSnapshots = CaptureSettlementStates(leader, settlement);
        clientStateFixture = new ClientStateFixture
        {
            Settlement = settlement,
            PlayerParty = playerParty,
            LeaderParty = leader,
            PlayerWasActive = playerParty.IsActive,
            PlayerClanMembership = CaptureClanMembership(playerClan, fixtureKingdom),
            FixtureKingdom = fixtureKingdom,
            FixtureKingdomClanCount = fixtureKingdom.Clans.Count,
            LeaderRethinkAtNextHourlyTick = leader.Ai.RethinkAtNextHourlyTick,
            SettlementSnapshots = settlementSnapshots,
        };
        lastRestoredClientStateFixture = null;

        return $"Client army siege-wait fixture snapshot captured: leader={leader.StringId}, " +
               $"playerClan={playerClan.StringId}, leaderKingdom={fixtureKingdom.StringId}, " +
               $"leaderKingdomClanCount={fixtureKingdom.Clans.Count}, " +
               $"playerActive={playerParty.IsActive}, " +
               $"leaderRethinkAtNextHourlyTick={leader.Ai.RethinkAtNextHourlyTick}, " +
               $"settlementSnapshots={settlementSnapshots.Length}.";
    }

    [CommandLineArgumentFunction("siege_wait_fixture_stage", "coop.debug.army")]
    public static string Stage(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 3)
        {
            return "Usage: coop.debug.army.siege_wait_fixture_stage " +
                "<controllerId> <observerControllerId> <leaderPartyId>";
        }

        if (fixture != null)
            return "An army siege-wait fixture is already active.";

        if (!TryGetConnectedPlayerParty(args[0], out var playerParty, out var error) ||
            !TryGetConnectedPlayerParty(args[1], out _, out error))
            return error;
        if (!TryGetFixturePlayerClan(playerParty, out var playerClan, out error))
            return error;

        if (playerParty.CurrentSettlement != null || playerParty.Army != null ||
            playerParty.AttachedTo != null || playerParty.MapEvent != null ||
            playerParty.BesiegerCamp != null)
            return "The joining player party must be outside settlements, armies, map events, and siege camps.";

        var settlement = Settlement.All.FirstOrDefault(candidate => candidate.StringId == "castle_EW1");
        if (settlement == null)
            return "Garontor Castle (castle_EW1) was not found.";
        if (settlement.SiegeEvent != null)
            return "Garontor Castle is already under siege.";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(args[2], out var leader))
            return $"Unable to resolve AI leader party {args[2]}.";
        if (!IsEligibleLeader(leader, playerClan, settlement, out var fixtureKingdom))
            return $"AI leader party {args[2]} no longer qualifies for the Garontor fixture.";
        if (!TryValidateMembershipFixture(playerClan, fixtureKingdom, out error))
            return error;

        var settlementOwnerHero = settlement.OwnerClan?.Leader;
        if (settlementOwnerHero == null)
            return "Garontor Castle does not have an owner hero whose relation can be restored.";

        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !behaviorSnapshot.TryCreate(playerParty, out var playerBehavior) ||
            !behaviorSnapshot.TryCreate(leader, out var leaderBehavior))
            return "Unable to capture the player or AI leader movement state.";
        if (!objectManager.TryGetIdWithLogging(playerParty, out var playerPartyId) ||
            !objectManager.TryGetIdWithLogging(leader, out var leaderPartyId))
            return "Unable to capture the registered player or AI leader party ids.";
        if (!ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControl))
            return "Unable to resolve authoritative time control.";
        if (!ContainerProvider.TryResolve<IKingdomMembershipState>(out var kingdomMembershipState))
            return "Unable to resolve kingdom membership state.";

        var settlementSnapshots = CaptureSettlementStates(leader, settlement);
        var playerClanMembership = CaptureClanMembership(playerClan, fixtureKingdom);

        var activeFixture = new Fixture
        {
            ControllerId = args[0],
            ObserverControllerId = args[1],
            Settlement = settlement,
            PlayerParty = playerParty,
            LeaderParty = leader,
            PlayerPartyId = playerPartyId,
            LeaderPartyId = leaderPartyId,
            PlayerClanMembership = playerClanMembership,
            FixtureKingdom = fixtureKingdom,
            FixtureKingdomClanCount = fixtureKingdom.Clans.Count,
            SettlementOwnerHero = settlementOwnerHero,
            LeaderHero = leader.LeaderHero,
            OwnerLeaderRelation = CharacterRelationManager.GetHeroRelation(settlementOwnerHero, leader.LeaderHero),
            PlayerWasActive = playerParty.IsActive,
            PlayerDisorganized = playerParty.IsDisorganized,
            LeaderDisorganized = leader.IsDisorganized,
            PlayerDisorganizedUntilTime = playerParty.DisorganizedUntilTime,
            LeaderDisorganizedUntilTime = leader.DisorganizedUntilTime,
            LeaderRethinkAtNextHourlyTick = leader.Ai.RethinkAtNextHourlyTick,
            PlayerBehavior = playerBehavior,
            LeaderBehavior = leaderBehavior,
            SettlementSnapshots = settlementSnapshots,
            OriginalTimeControl = timeControl.GetTimeControl(),
        };
        fixture = activeFixture;
        lastRestoredFixture = null;

        try
        {
            timeControl.AddUnpausePolicy(TimeUnpausePolicy);
            activeFixture.TimePolicyAdded = true;
            timeControl.ServerSetTimeControl(TimeControlEnum.Pause);
            if (timeControl.GetTimeControl() != TimeControlEnum.Pause)
                throw new InvalidOperationException("Unable to pause authoritative campaign time.");

            if (!activeFixture.PlayerWasActive)
                playerParty.IsActive = true;
            if (!playerParty.IsActive)
                throw new InvalidOperationException("Unable to activate the joining player party.");
            if (activeFixture.PlayerDisorganized)
                playerParty.SetDisorganized(false);
            if (playerParty.IsDisorganized)
                throw new InvalidOperationException("Unable to clear the joining player's transient disorganized state.");
            if (activeFixture.LeaderDisorganized)
                leader.SetDisorganized(false);
            if (leader.IsDisorganized)
                throw new InvalidOperationException("Unable to clear the AI leader's transient disorganized state.");

            kingdomMembershipState.MoveClanToKingdom(
                playerClanMembership.Kingdom,
                fixtureKingdom,
                playerClan,
                publishCollectionChanges: true);
            if (!IsClanMembershipStaged(
                    playerClanMembership,
                    fixtureKingdom,
                    activeFixture.FixtureKingdomClanCount))
            {
                throw new InvalidOperationException(
                    "Unable to join the player clan to the AI leader's kingdom.");
            }

            try
            {
                fixtureKingdom.CreateArmy(leader.LeaderHero, settlement, Army.ArmyTypes.Besieger);
            }
            finally
            {
                activeFixture.Army = leader.Army;
            }
            if (activeFixture.Army == null || activeFixture.Army.LeaderParty != leader)
                throw new InvalidOperationException("The AI leader did not create an army.");

            leader.Position = settlement.GatePosition;
            leader.SetMoveBesiegeSettlement(settlement, MobileParty.NavigationType.Default);
            try
            {
                activeFixture.SiegeEvent = Campaign.Current.SiegeEventManager.StartSiegeEvent(settlement, leader);
            }
            finally
            {
                CaptureSiegeIdentity(activeFixture);
            }
            if (activeFixture.SiegeEvent?.BesiegerCamp?.LeaderParty != leader)
                throw new InvalidOperationException("Garontor did not create the expected AI-led besieger camp.");
            if (!activeFixture.SiegeEvent.CanPartyJoinSide(playerParty.Party, BattleSideEnum.Attacker))
                throw new InvalidOperationException("The joining player cannot join the AI army's attacking siege side.");

            MoveAndHold(playerParty, new CampaignVec2(
                new TaleWorlds.Library.Vec2(settlement.GatePosition.X + 3f, settlement.GatePosition.Y + 1f),
                isOnLand: true));
            return $"Army siege-wait fixture staged: settlement={settlement.Name} ({settlement.StringId}), " +
                   $"leader={leader.StringId}, leaderPartyId={activeFixture.LeaderPartyId}, " +
                   $"armyLeader={activeFixture.Army.LeaderParty.StringId}, joiningPlayer={playerParty.StringId}, " +
                   $"joiningPartyId={activeFixture.PlayerPartyId}, playerAtSea={playerBehavior.IsCurrentlyAtSea}, " +
                   $"leaderAtSea={leaderBehavior.IsCurrentlyAtSea}, " +
                   $"playerClan={playerClan.StringId}, originalKingdom={playerClanMembership.Kingdom?.StringId ?? "none"}, " +
                   $"leaderKingdom={fixtureKingdom.StringId}, " +
                   $"leaderKingdomOriginalClanCount={activeFixture.FixtureKingdomClanCount}, " +
                   $"playerDisorganized={activeFixture.PlayerDisorganized}, " +
                   $"playerDisorganizedUntilTicks={activeFixture.PlayerDisorganizedUntilTime.NumTicks}, " +
                   $"playerActive={activeFixture.PlayerWasActive}, " +
                   $"leaderDisorganized={activeFixture.LeaderDisorganized}, " +
                   $"leaderDisorganizedUntilTicks={activeFixture.LeaderDisorganizedUntilTime.NumTicks}, " +
                   $"leaderRethinkAtNextHourlyTick={activeFixture.LeaderRethinkAtNextHourlyTick}, " +
                   $"observer={args[1]}, " +
                   $"settlementSnapshots={settlementSnapshots.Length}, originalTime={activeFixture.OriginalTimeControl}.";
        }
        catch (Exception exception)
        {
            return $"Army siege-wait fixture setup failed: {exception.Message}. {RestoreFixture()}";
        }
    }

    [CommandLineArgumentFunction("siege_wait_fixture_open_join", "coop.debug.army")]
    public static string OpenJoin(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on the joining client.";
        if (args.Count != 0)
            return "Usage: coop.debug.army.siege_wait_fixture_open_join";

        var settlement = Settlement.All.FirstOrDefault(candidate => candidate.StringId == "castle_EW1");
        var leader = settlement?.SiegeEvent?.BesiegerCamp?.LeaderParty;
        if (settlement == null || leader == null)
            return "Garontor Castle does not have an AI-led siege fixture.";
        if (MobileParty.MainParty.Army != null)
            return "The joining player is already in an army.";

        EncounterManager.StartPartyEncounter(MobileParty.MainParty.Party, leader.Party);
        return $"Opened the production party encounter with army leader {leader.Name}.";
    }

    [CommandLineArgumentFunction("siege_wait_fixture_join_rendered", "coop.debug.army")]
    public static string JoinRendered(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on the joining client.";
        if (args.Count != 0)
            return "Usage: coop.debug.army.siege_wait_fixture_join_rendered";

        var settlement = Settlement.All.FirstOrDefault(candidate => candidate.StringId == "castle_EW1");
        var leader = settlement?.SiegeEvent?.BesiegerCamp?.LeaderParty;
        if (leader == null || PlayerEncounter.EncounteredMobileParty != leader)
            return "The rendered fixture army encounter is not active.";
        if (!HasSameMapFaction(MobileParty.MainParty, leader))
            return "The joining player and AI army leader no longer share a map faction.";
        if (Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId != "army_encounter")
            return "The rendered army encounter menu is not active.";

        var behavior = Campaign.Current?.GetCampaignBehavior<EncounterGameMenuBehavior>();
        if (behavior == null)
            return "Encounter menu behavior is unavailable.";

        var conditionArgs = new MenuCallbackArgs((MenuContext)null, null);
        if (!behavior.game_menu_army_join_on_condition(conditionArgs) || !conditionArgs.IsEnabled)
            return "The production Join Army option is not available and enabled.";
        if (clientFixture != null)
            return "A joining-client army siege-wait fixture snapshot is already active.";
        if (leader.LeaderHero == null)
            return "The fixture army leader does not have a hero whose meeting state can be restored.";

        clientFixture = new ClientFixture
        {
            LeaderHero = leader.LeaderHero,
            LeaderHasMet = leader.LeaderHero.HasMet,
            LeaderLastMeetingTime = leader.LeaderHero.LastMeetingTimeWithPlayer,
        };
        lastRestoredClientFixture = null;
        behavior.game_menu_army_join_on_consequence(null);
        return "Invoked the rendered army-join consequence.";
    }

    [CommandLineArgumentFunction("siege_wait_fixture_restore_client", "coop.debug.army")]
    public static string RestoreClient(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.army.siege_wait_fixture_restore_client";

        var activeClientFixture = clientFixture;
        var activeClientStateFixture = clientStateFixture;
        if (activeClientFixture == null && activeClientStateFixture == null)
            return "No client army siege-wait fixture snapshot is active.";
        if (activeClientStateFixture != null &&
            (activeClientStateFixture.Settlement.SiegeEvent != null ||
             activeClientStateFixture.PlayerParty.IsActive != activeClientStateFixture.PlayerWasActive ||
             activeClientStateFixture.LeaderParty.Army != null ||
             activeClientStateFixture.LeaderParty.BesiegerCamp != null ||
             !IsClanMembershipRestored(
                 activeClientStateFixture.PlayerClanMembership,
                 activeClientStateFixture.FixtureKingdom,
                 activeClientStateFixture.FixtureKingdomClanCount)))
        {
            return "Client army siege-wait fixture restore is waiting for replicated teardown.";
        }

        try
        {
            using (new AllowedThread())
            {
                if (activeClientFixture != null)
                {
                    activeClientFixture.LeaderHero._hasMet = activeClientFixture.LeaderHasMet;
                    activeClientFixture.LeaderHero.LastMeetingTimeWithPlayer =
                        activeClientFixture.LeaderLastMeetingTime;
                }

                if (activeClientStateFixture != null)
                {
                    RestoreSettlementStates(activeClientStateFixture.SettlementSnapshots);
                    activeClientStateFixture.LeaderParty.Ai.RethinkAtNextHourlyTick =
                        activeClientStateFixture.LeaderRethinkAtNextHourlyTick;
                }
            }

            if (activeClientFixture != null && !IsClientHeroStateRestored(activeClientFixture))
                throw new InvalidOperationException("Unable to restore the AI leader's client-local meeting state.");
            if (activeClientStateFixture != null &&
                (!AreSettlementStatesRestored(activeClientStateFixture.SettlementSnapshots) ||
                 activeClientStateFixture.PlayerParty.IsActive != activeClientStateFixture.PlayerWasActive ||
                 activeClientStateFixture.LeaderParty.Ai.RethinkAtNextHourlyTick !=
                    activeClientStateFixture.LeaderRethinkAtNextHourlyTick ||
                 !IsClanMembershipRestored(
                     activeClientStateFixture.PlayerClanMembership,
                     activeClientStateFixture.FixtureKingdom,
                     activeClientStateFixture.FixtureKingdomClanCount)))
            {
                throw new InvalidOperationException(
                    "Unable to verify the replicated membership, siege-side, or party AI state.");
            }

            lastRestoredClientFixture = activeClientFixture;
            clientFixture = null;
            lastRestoredClientStateFixture = activeClientStateFixture;
            clientStateFixture = null;
            return $"Client army siege-wait fixture restored: " +
                   $"leader={activeClientStateFixture?.LeaderParty.StringId ?? activeClientFixture.LeaderHero.StringId}, " +
                   $"meetingState={activeClientFixture != null}, " +
                   $"settlementState={activeClientStateFixture != null}, " +
                   $"leaderRethinkState={activeClientStateFixture != null}, " +
                   $"clientPlayerActiveRestored={activeClientStateFixture != null}, " +
                   $"clientMembershipRestored={activeClientStateFixture != null}.";
        }
        catch (Exception exception)
        {
            return $"Client army siege-wait fixture restore failed: {exception.Message}";
        }
    }

    [CommandLineArgumentFunction("siege_wait_fixture_state", "coop.debug.army")]
    public static string State(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.army.siege_wait_fixture_state <joiningPartyId> <leaderPartyId>";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(args[0], out var player))
            return $"Unable to resolve joining party {args[0]}.";
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(args[1], out var leader))
            return $"Unable to resolve AI leader party {args[1]}.";

        var activeFixture = fixture ?? lastRestoredFixture;
        var settlement = activeFixture?.Settlement ?? Settlement.All.FirstOrDefault(candidate => candidate.StringId == "castle_EW1");
        if (settlement == null)
            return "Garontor Castle (castle_EW1) was not found.";

        var matchesFixture = activeFixture != null &&
            activeFixture.PlayerParty == player &&
            activeFixture.LeaderParty == leader;
        var playerClan = player.LeaderHero?.Clan ?? player.ActualClan;
        var leaderKingdom = leader.MapFaction as Kingdom;
        var relationRestored = matchesFixture
            ? (CharacterRelationManager.GetHeroRelation(activeFixture.SettlementOwnerHero, activeFixture.LeaderHero) ==
                activeFixture.OwnerLeaderRelation).ToString()
            : "unknown";
        var playerPositionRestored = matchesFixture
            ? IsPartyPositionRestored(activeFixture.PlayerParty, activeFixture.PlayerBehavior).ToString()
            : "unknown";
        var leaderPositionRestored = matchesFixture
            ? IsPartyPositionRestored(activeFixture.LeaderParty, activeFixture.LeaderBehavior).ToString()
            : "unknown";
        var playerSeaRestored = matchesFixture
            ? (activeFixture.PlayerParty.IsCurrentlyAtSea == activeFixture.PlayerBehavior.IsCurrentlyAtSea).ToString()
            : "unknown";
        var leaderSeaRestored = matchesFixture
            ? (activeFixture.LeaderParty.IsCurrentlyAtSea == activeFixture.LeaderBehavior.IsCurrentlyAtSea).ToString()
            : "unknown";
        var playerDisorganizedRestored = matchesFixture
            ? IsDisorganizedStateRestored(
                activeFixture.PlayerParty,
                activeFixture.PlayerDisorganized,
                activeFixture.PlayerDisorganizedUntilTime).ToString()
            : "unknown";
        var playerActiveRestored = matchesFixture
            ? (activeFixture.PlayerParty.IsActive == activeFixture.PlayerWasActive).ToString()
            : "unknown";
        var leaderDisorganizedRestored = matchesFixture
            ? IsDisorganizedStateRestored(
                activeFixture.LeaderParty,
                activeFixture.LeaderDisorganized,
                activeFixture.LeaderDisorganizedUntilTime).ToString()
            : "unknown";
        var leaderRethinkRestored = matchesFixture
            ? (activeFixture.LeaderParty.Ai.RethinkAtNextHourlyTick ==
                activeFixture.LeaderRethinkAtNextHourlyTick).ToString()
            : "unknown";
        var settlementStateRestored = matchesFixture
            ? (fixture == null && AreSettlementStatesRestored(activeFixture)).ToString()
            : "unknown";
        var membershipRestored = matchesFixture
            ? (fixture == null &&
               IsClanMembershipRestored(
                   activeFixture.PlayerClanMembership,
                   activeFixture.FixtureKingdom,
                   activeFixture.FixtureKingdomClanCount)).ToString()
            : "unknown";
        var timePaused = matchesFixture &&
            fixture != null &&
            ContainerProvider.TryResolve<ITimeControlInterface>(out var activeTimeControl)
                ? (activeFixture.TimePolicyAdded &&
                   activeTimeControl.GetTimeControl() == TimeControlEnum.Pause).ToString()
                : "unknown";
        var timeRestored = matchesFixture &&
            fixture == null &&
            ContainerProvider.TryResolve<ITimeControlInterface>(out var restoredTimeControl)
                ? (activeFixture.TimeRestored &&
                   restoredTimeControl.GetTimeControl() == activeFixture.OriginalTimeControl).ToString()
                : "unknown";
        var activeClientFixture = clientFixture ?? lastRestoredClientFixture;
        var matchesClientFixture = activeClientFixture != null &&
            activeClientFixture.LeaderHero == leader.LeaderHero;
        var leaderHeroRestored = matchesClientFixture
            ? (clientFixture == null && IsClientHeroStateRestored(activeClientFixture)).ToString()
            : "unknown";
        var activeClientStateFixture = clientStateFixture ?? lastRestoredClientStateFixture;
        var matchesClientStateFixture = activeClientStateFixture != null &&
            activeClientStateFixture.LeaderParty == leader &&
            activeClientStateFixture.Settlement == settlement &&
            activeClientStateFixture.PlayerClanMembership.Clan == playerClan;
        var clientSettlementStateRestored = matchesClientStateFixture
            ? (clientStateFixture == null &&
               AreSettlementStatesRestored(activeClientStateFixture.SettlementSnapshots)).ToString()
            : "unknown";
        var clientPlayerActiveRestored = matchesClientStateFixture
            ? (clientStateFixture == null &&
               activeClientStateFixture.PlayerParty.IsActive ==
                    activeClientStateFixture.PlayerWasActive).ToString()
            : "unknown";
        var clientLeaderRethinkRestored = matchesClientStateFixture
            ? (clientStateFixture == null &&
               activeClientStateFixture.LeaderParty.Ai.RethinkAtNextHourlyTick ==
                    activeClientStateFixture.LeaderRethinkAtNextHourlyTick).ToString()
            : "unknown";
        var clientMembershipRestored = matchesClientStateFixture
            ? (clientStateFixture == null &&
               IsClanMembershipRestored(
                   activeClientStateFixture.PlayerClanMembership,
                   activeClientStateFixture.FixtureKingdom,
                   activeClientStateFixture.FixtureKingdomClanCount)).ToString()
            : "unknown";
        var membershipStaged = matchesFixture && fixture != null
            ? IsClanMembershipStaged(
                activeFixture.PlayerClanMembership,
                activeFixture.FixtureKingdom,
                activeFixture.FixtureKingdomClanCount)
            : matchesClientStateFixture && clientStateFixture != null
                ? IsClanMembershipStaged(
                    activeClientStateFixture.PlayerClanMembership,
                    activeClientStateFixture.FixtureKingdom,
                    activeClientStateFixture.FixtureKingdomClanCount)
                : IsClanCurrentlyInKingdom(playerClan, leaderKingdom);
        var playerBehaviorState = "unavailable";
        var leaderBehaviorState = "unavailable";
        if (ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var currentBehaviorSnapshot))
        {
            if (currentBehaviorSnapshot.TryCreate(player, out var currentPlayerBehavior))
                playerBehaviorState = DescribeBehavior(currentPlayerBehavior);
            if (currentBehaviorSnapshot.TryCreate(leader, out var currentLeaderBehavior))
                leaderBehaviorState = DescribeBehavior(currentLeaderBehavior);
        }
        var ownerLeaderRelation = settlement.OwnerClan?.Leader != null && leader.LeaderHero != null
            ? CharacterRelationManager.GetHeroRelation(settlement.OwnerClan.Leader, leader.LeaderHero)
                .ToString(CultureInfo.InvariantCulture)
            : "unknown";
        var timeControl = ContainerProvider.TryResolve<ITimeControlInterface>(out var currentTimeControl)
            ? currentTimeControl.GetTimeControl().ToString()
            : "unknown";
        return $"Fixture: settlement={settlement.StringId}, siege={settlement.SiegeEvent != null}, " +
               $"leader={DescribeParty(leader)}, player={DescribeParty(player)}, " +
               $"playerClan={playerClan?.StringId ?? "none"}, " +
               $"playerClanKingdom={playerClan?.Kingdom?.StringId ?? "none"}, " +
               $"playerMapFaction={player?.MapFaction?.StringId ?? "none"}, " +
               $"playerClanMercenary={playerClan?.IsUnderMercenaryService.ToString() ?? "unknown"}, " +
               $"playerClanFiefCount={(playerClan == null ? -1 : GetClanFiefCount(playerClan))}, " +
               $"playerClanCollections={DescribeClanCollections(playerClan)}, " +
               $"leaderKingdom={leaderKingdom?.StringId ?? "none"}, " +
               $"leaderMapFaction={leader.MapFaction?.StringId ?? "none"}, " +
               $"playerClanInLeaderKingdom={KingdomContainsClan(leaderKingdom, playerClan)}, " +
               $"leaderKingdomClanCount={leaderKingdom?.Clans.Count ?? -1}, " +
               $"membershipStaged={membershipStaged}, " +
               $"playerPosition={DescribePosition(player.Position)}, " +
               $"leaderPosition={DescribePosition(leader.Position)}, " +
               $"playerBehavior={playerBehaviorState}, leaderBehavior={leaderBehaviorState}, " +
               $"playerDisorganizedUntilTicks={player.DisorganizedUntilTime.NumTicks}, " +
               $"leaderDisorganizedUntilTicks={leader.DisorganizedUntilTime.NumTicks}, " +
               $"lastAttacker={settlement.LastAttackerParty?.StringId ?? "none"}, " +
               $"lastThreatTicks={settlement.LastThreatTime.NumTicks}, " +
               $"ownerLeaderRelation={ownerLeaderRelation}, timeControl={timeControl}, " +
               $"restored={fixture == null && lastRestoredFixture != null && matchesFixture}, " +
               $"relationRestored={relationRestored}, playerPositionRestored={playerPositionRestored}, " +
               $"leaderPositionRestored={leaderPositionRestored}, playerSeaRestored={playerSeaRestored}, " +
               $"leaderSeaRestored={leaderSeaRestored}, " +
               $"playerActiveRestored={playerActiveRestored}, " +
               $"playerDisorganizedRestored={playerDisorganizedRestored}, " +
               $"leaderDisorganizedRestored={leaderDisorganizedRestored}, " +
               $"leaderRethinkRestored={leaderRethinkRestored}, " +
               $"settlementStateRestored={settlementStateRestored}, " +
               $"membershipRestored={membershipRestored}, " +
               $"timePaused={timePaused}, timeRestored={timeRestored}, leaderHeroRestored={leaderHeroRestored}, " +
               $"clientSettlementStateRestored={clientSettlementStateRestored}, " +
               $"clientPlayerActiveRestored={clientPlayerActiveRestored}, " +
               $"clientLeaderRethinkRestored={clientLeaderRethinkRestored}, " +
               $"clientMembershipRestored={clientMembershipRestored}, " +
               $"encounter={PlayerEncounter.Current != null}, " +
               $"menu={Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "none"}.";
    }

    [CommandLineArgumentFunction("siege_wait_fixture_restore", "coop.debug.army")]
    public static string Restore(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.army.siege_wait_fixture_restore";
        return RestoreFixture();
    }

    private static string RestoreFixture()
    {
        var activeFixture = fixture;
        if (activeFixture == null)
            return "No army siege-wait fixture is active.";

        string restoreFailure = null;
        try
        {
            if (MatchesCapturedSiege(
                    activeFixture.PlayerParty.BesiegerCamp,
                    activeFixture.SiegeEvent,
                    activeFixture.BesiegerCamp))
            {
                activeFixture.PlayerParty.BesiegerCamp = null;
            }

            if (activeFixture.Army != null && activeFixture.PlayerParty.Army == activeFixture.Army)
            {
                MessageBroker.Instance.Publish(
                    typeof(ArmySiegeWaitFixtureCommands),
                    new MobilePartyInArmyRemoved(activeFixture.Army, activeFixture.PlayerParty, activeFixture.PlayerParty));
                ArmyPatches.RemoveMobilePartyInArmy(activeFixture.PlayerParty, activeFixture.Army, activeFixture.PlayerParty);
            }

            if (MatchesCapturedSiege(
                    activeFixture.LeaderParty.BesiegerCamp,
                    activeFixture.SiegeEvent,
                    activeFixture.BesiegerCamp))
            {
                if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
                    throw new InvalidOperationException("Unable to resolve siege restoration service.");
                siegeEventInterface.BreakSiege(activeFixture.LeaderParty);
            }

            if (activeFixture.SiegeEvent != null &&
                activeFixture.Settlement.SiegeEvent == activeFixture.SiegeEvent)
            {
                var remainingLeader = activeFixture.SiegeEvent?.BesiegerCamp?.LeaderParty;
                if (remainingLeader != null &&
                    MatchesCapturedSiege(
                        remainingLeader.BesiegerCamp,
                        activeFixture.SiegeEvent,
                        activeFixture.BesiegerCamp))
                {
                    if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
                        throw new InvalidOperationException("Unable to resolve siege restoration service.");
                    siegeEventInterface.BreakSiege(remainingLeader);
                }
            }
            if (activeFixture.SiegeEvent != null &&
                activeFixture.Settlement.SiegeEvent == activeFixture.SiegeEvent)
                throw new InvalidOperationException("Unable to remove the fixture siege.");

            if (activeFixture.Army != null && activeFixture.LeaderParty.Army == activeFixture.Army)
                DisbandArmyAction.ApplyInternal(activeFixture.Army, Army.ArmyDispersionReason.NotEnoughParty);

            RestoreSettlementStates(activeFixture);

            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
                throw new InvalidOperationException("Unable to restore captured movement state.");

            RestoreParty(activeFixture.PlayerParty, activeFixture.PlayerBehavior, behaviorSnapshot);
            RestoreParty(activeFixture.LeaderParty, activeFixture.LeaderBehavior, behaviorSnapshot);
            activeFixture.LeaderParty.Ai.RethinkAtNextHourlyTick =
                activeFixture.LeaderRethinkAtNextHourlyTick;

            RestoreDisorganizedState(
                activeFixture.PlayerParty,
                activeFixture.PlayerDisorganized,
                activeFixture.PlayerDisorganizedUntilTime);
            RestoreDisorganizedState(
                activeFixture.LeaderParty,
                activeFixture.LeaderDisorganized,
                activeFixture.LeaderDisorganizedUntilTime);
            if (!IsDisorganizedStateRestored(
                    activeFixture.PlayerParty,
                    activeFixture.PlayerDisorganized,
                    activeFixture.PlayerDisorganizedUntilTime) ||
                !IsDisorganizedStateRestored(
                    activeFixture.LeaderParty,
                    activeFixture.LeaderDisorganized,
                    activeFixture.LeaderDisorganizedUntilTime) ||
                activeFixture.LeaderParty.Ai.RethinkAtNextHourlyTick !=
                    activeFixture.LeaderRethinkAtNextHourlyTick)
            {
                throw new InvalidOperationException("Unable to restore captured party AI state.");
            }

            if (CharacterRelationManager.GetHeroRelation(activeFixture.SettlementOwnerHero, activeFixture.LeaderHero) !=
                activeFixture.OwnerLeaderRelation)
            {
                CharacterRelationManager.SetHeroRelation(
                    activeFixture.SettlementOwnerHero,
                    activeFixture.LeaderHero,
                    activeFixture.OwnerLeaderRelation);
            }

            PublishBehavior(activeFixture.PlayerParty, activeFixture.PlayerBehavior.IsCurrentlyAtSea);
            PublishBehavior(activeFixture.LeaderParty, activeFixture.LeaderBehavior.IsCurrentlyAtSea);
        }
        catch (Exception exception)
        {
            restoreFailure = exception.Message;
        }
        try
        {
            RestoreClanMembership(activeFixture);
        }
        catch (Exception exception)
        {
            restoreFailure = restoreFailure == null
                ? exception.Message
                : $"{restoreFailure}; membership restoration failed: {exception.Message}";
        }
        try
        {
            activeFixture.PlayerParty.IsActive = activeFixture.PlayerWasActive;
            if (activeFixture.PlayerParty.IsActive != activeFixture.PlayerWasActive)
                throw new InvalidOperationException("Unable to restore the joining player party active state.");
        }
        catch (Exception exception)
        {
            restoreFailure = restoreFailure == null
                ? exception.Message
                : $"{restoreFailure}; active-state restoration failed: {exception.Message}";
        }
        if (restoreFailure == null)
        {
            try
            {
                RestoreTimeControl(activeFixture);
            }
            catch (Exception exception)
            {
                restoreFailure = restoreFailure == null
                    ? exception.Message
                    : $"{restoreFailure}; time restoration failed: {exception.Message}";
            }
        }

        if (restoreFailure != null)
            return $"Army siege-wait fixture restore failed: {restoreFailure}";

        lastRestoredFixture = activeFixture;
        fixture = null;
        return "Army siege-wait fixture restored.";
    }

    private static void RestoreSettlementStates(Fixture activeFixture)
    {
        RestoreSettlementStates(activeFixture.SettlementSnapshots);

        if (!AreSettlementStatesRestored(activeFixture))
            throw new InvalidOperationException("Unable to restore captured settlement siege-side state.");
    }

    private static void RestoreSettlementStates(SettlementSnapshot[] settlementSnapshots)
    {
        foreach (var snapshot in settlementSnapshots)
        {
            snapshot.Settlement.LastAttackerParty = snapshot.LastAttackerParty;
            snapshot.Settlement.LastThreatTime = snapshot.LastThreatTime;
            snapshot.Settlement.SiegeEngines = snapshot.SiegeEngines;
            snapshot.Settlement._siegeEngineMissiles = snapshot.SiegeEngineMissiles;
            snapshot.Settlement.NumberOfTroopsKilledOnSide = snapshot.NumberOfTroopsKilledOnSide;
            snapshot.Settlement.SiegeStrategy = snapshot.SiegeStrategy;
            snapshot.Settlement.CurrentSiegeState = snapshot.CurrentSiegeState;
        }
    }

    private static bool AreSettlementStatesRestored(Fixture activeFixture) =>
        activeFixture?.SettlementSnapshots != null &&
        AreSettlementStatesRestored(activeFixture.SettlementSnapshots);

    private static bool AreSettlementStatesRestored(SettlementSnapshot[] settlementSnapshots) =>
        settlementSnapshots != null &&
        settlementSnapshots.All(snapshot =>
            snapshot.Settlement.LastAttackerParty == snapshot.LastAttackerParty &&
            snapshot.Settlement.LastThreatTime.NumTicks == snapshot.LastThreatTime.NumTicks &&
            snapshot.Settlement.SiegeEngines == snapshot.SiegeEngines &&
            snapshot.Settlement._siegeEngineMissiles == snapshot.SiegeEngineMissiles &&
            snapshot.Settlement.NumberOfTroopsKilledOnSide == snapshot.NumberOfTroopsKilledOnSide &&
            snapshot.Settlement.SiegeStrategy == snapshot.SiegeStrategy &&
            snapshot.Settlement.CurrentSiegeState == snapshot.CurrentSiegeState);

    private static SettlementSnapshot[] CaptureSettlementStates(
        MobileParty leader,
        Settlement settlement) =>
        Settlement.All
            .Where(candidate => candidate == settlement ||
                ((candidate.IsFortification || candidate.IsVillage) &&
                 candidate.LastAttackerParty == leader))
            .Select(candidate => new SettlementSnapshot
            {
                Settlement = candidate,
                LastAttackerParty = candidate.LastAttackerParty,
                LastThreatTime = candidate.LastThreatTime,
                SiegeEngines = candidate.SiegeEngines,
                SiegeEngineMissiles = candidate._siegeEngineMissiles,
                NumberOfTroopsKilledOnSide = candidate.NumberOfTroopsKilledOnSide,
                SiegeStrategy = candidate.SiegeStrategy,
                CurrentSiegeState = candidate.CurrentSiegeState,
            })
            .ToArray();

    internal static void RestoreDisorganizedState(
        MobileParty party,
        bool isDisorganized,
        CampaignTime disorganizedUntilTime)
    {
        party._disorganizedUntilTime = disorganizedUntilTime;
        party._isDisorganized = isDisorganized;
        party.UpdateVersionNo();
    }

    private static bool IsDisorganizedStateRestored(
        MobileParty party,
        bool isDisorganized,
        CampaignTime disorganizedUntilTime) =>
        party.IsDisorganized == isDisorganized &&
        party.DisorganizedUntilTime.NumTicks == disorganizedUntilTime.NumTicks;

    private static void RestoreTimeControl(Fixture activeFixture)
    {
        if (!ContainerProvider.TryResolve<ITimeControlInterface>(out var timeControl))
            throw new InvalidOperationException("Unable to resolve authoritative time control for restoration.");

        if (activeFixture.TimePolicyAdded)
        {
            timeControl.RemoveUnpausePolicy(TimeUnpausePolicy);
            activeFixture.TimePolicyAdded = false;
        }

        try
        {
            timeControl.ServerSetTimeControl(activeFixture.OriginalTimeControl);
            if (timeControl.GetTimeControl() != activeFixture.OriginalTimeControl)
                throw new InvalidOperationException("Unable to restore authoritative campaign time.");

            activeFixture.TimeRestored = true;
        }
        catch
        {
            if (!activeFixture.TimePolicyAdded)
            {
                timeControl.AddUnpausePolicy(TimeUnpausePolicy);
                activeFixture.TimePolicyAdded = true;
            }
            timeControl.ServerSetTimeControl(TimeControlEnum.Pause);
            throw;
        }
    }

    private static bool IsClientHeroStateRestored(ClientFixture activeClientFixture) =>
        activeClientFixture != null &&
        activeClientFixture.LeaderHero.HasMet == activeClientFixture.LeaderHasMet &&
        activeClientFixture.LeaderHero.LastMeetingTimeWithPlayer.NumTicks ==
            activeClientFixture.LeaderLastMeetingTime.NumTicks;

    private static void CaptureSiegeIdentity(Fixture activeFixture)
    {
        if (activeFixture.SiegeEvent == null)
            activeFixture.SiegeEvent = activeFixture.Settlement?.SiegeEvent;
        if (activeFixture.BesiegerCamp == null)
            activeFixture.BesiegerCamp = activeFixture.SiegeEvent?.BesiegerCamp;
        if (activeFixture.SiegeEvent != null &&
            activeFixture.BesiegerCamp == null &&
            activeFixture.LeaderParty?.BesiegerCamp?.SiegeEvent == activeFixture.SiegeEvent)
        {
            activeFixture.BesiegerCamp = activeFixture.LeaderParty.BesiegerCamp;
        }
    }

    internal static bool MatchesCapturedSiege(
        BesiegerCamp partyCamp,
        SiegeEvent siegeEvent,
        BesiegerCamp besiegerCamp) =>
        partyCamp != null &&
        (partyCamp == besiegerCamp || (siegeEvent != null && partyCamp.SiegeEvent == siegeEvent));

    private static void RestoreParty(
        MobileParty party,
        PartyBehaviorUpdateData behavior,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        party.Position = behavior.PartyPosition;
        party.IsCurrentlyAtSea = behavior.IsCurrentlyAtSea;
        if (!behaviorSnapshot.TryApply(party, behavior, out _) ||
            !IsPartyStateRestored(party, behavior))
        {
            throw new InvalidOperationException($"Unable to restore captured movement state for {party.StringId}.");
        }
    }

    internal static bool IsPartyStateRestored(MobileParty party, PartyBehaviorUpdateData behavior) =>
        IsPartyPositionRestored(party, behavior) &&
        party.IsCurrentlyAtSea == behavior.IsCurrentlyAtSea;

    private static bool IsPartyPositionRestored(MobileParty party, PartyBehaviorUpdateData behavior) =>
        party != null &&
        party.Position.IsOnLand == behavior.PartyPosition.IsOnLand &&
        (party.Position - behavior.PartyPosition).LengthSquared < 0.0001f;

    private static ClanMembershipSnapshot CaptureClanMembership(Clan clan, Kingdom fixtureKingdom) =>
        new ClanMembershipSnapshot
        {
            Clan = clan,
            Kingdom = clan.Kingdom,
            MapFaction = clan.MapFaction,
            IsUnderMercenaryService = clan.IsUnderMercenaryService,
            FiefCount = GetClanFiefCount(clan),
            CollectionKingdoms = GetClanCollectionKingdoms(clan),
            FixtureKingdomClanList = fixtureKingdom._clans,
            FixtureKingdomClans = fixtureKingdom._clans.ToArray(),
        };

    private static bool IsClanMembershipStaged(
        ClanMembershipSnapshot snapshot,
        Kingdom fixtureKingdom,
        int fixtureKingdomClanCount)
    {
        if (snapshot?.Clan == null || fixtureKingdom == null ||
            snapshot.FixtureKingdomClanList != fixtureKingdom._clans ||
            fixtureKingdomClanCount != snapshot.FixtureKingdomClans.Length ||
            fixtureKingdom._clans.Count != fixtureKingdomClanCount + 1 ||
            !fixtureKingdom._clans.Take(fixtureKingdomClanCount)
                .SequenceEqual(snapshot.FixtureKingdomClans) ||
            fixtureKingdom._clans[fixtureKingdomClanCount] != snapshot.Clan)
        {
            return false;
        }

        var expectedCollectionKingdoms = snapshot.CollectionKingdoms
            .Concat(new[] { fixtureKingdom })
            .Distinct()
            .ToArray();
        return snapshot.Clan.Kingdom == fixtureKingdom &&
            snapshot.Clan.MapFaction == fixtureKingdom &&
            snapshot.Clan.IsUnderMercenaryService == snapshot.IsUnderMercenaryService &&
            GetClanFiefCount(snapshot.Clan) == snapshot.FiefCount &&
            HaveSameKingdoms(GetClanCollectionKingdoms(snapshot.Clan), expectedCollectionKingdoms);
    }

    private static bool IsClanMembershipRestored(
        ClanMembershipSnapshot snapshot,
        Kingdom fixtureKingdom,
        int fixtureKingdomClanCount) =>
        snapshot?.Clan != null &&
        fixtureKingdom != null &&
        snapshot.FixtureKingdomClanList == fixtureKingdom._clans &&
        fixtureKingdomClanCount == snapshot.FixtureKingdomClans.Length &&
        fixtureKingdom._clans.SequenceEqual(snapshot.FixtureKingdomClans) &&
        snapshot.Clan.Kingdom == snapshot.Kingdom &&
        snapshot.Clan.MapFaction == snapshot.MapFaction &&
        snapshot.Clan.IsUnderMercenaryService == snapshot.IsUnderMercenaryService &&
        GetClanFiefCount(snapshot.Clan) == snapshot.FiefCount &&
        HaveSameKingdoms(GetClanCollectionKingdoms(snapshot.Clan), snapshot.CollectionKingdoms);

    private static void RestoreClanMembership(Fixture activeFixture)
    {
        if (!ContainerProvider.TryResolve<IKingdomMembershipState>(out var kingdomMembershipState))
            throw new InvalidOperationException("Unable to resolve kingdom membership state for restoration.");

        kingdomMembershipState.MoveClanToKingdom(
            activeFixture.FixtureKingdom,
            activeFixture.PlayerClanMembership.Kingdom,
            activeFixture.PlayerClanMembership.Clan,
            publishCollectionChanges: true);
        if (!IsClanMembershipRestored(
                activeFixture.PlayerClanMembership,
                activeFixture.FixtureKingdom,
                activeFixture.FixtureKingdomClanCount))
        {
            throw new InvalidOperationException("Unable to restore the joining player's clan membership.");
        }
    }

    private static bool TryGetFixturePlayerClan(
        MobileParty playerParty,
        out Clan playerClan,
        out string error)
    {
        playerClan = playerParty?.LeaderHero?.Clan ?? playerParty?.ActualClan;
        error = null;
        if (playerClan != null)
            return true;

        error = "Unable to resolve the joining player's clan.";
        return false;
    }

    private static bool TryValidateMembershipFixture(
        Clan playerClan,
        Kingdom fixtureKingdom,
        out string error)
    {
        error = null;
        if (playerClan.Kingdom != null)
        {
            error = $"The joining player clan already belongs to kingdom {playerClan.Kingdom.StringId}.";
            return false;
        }
        if (playerClan.IsUnderMercenaryService)
        {
            error = "The joining player clan must not be under mercenary service.";
            return false;
        }
        if (GetClanFiefCount(playerClan) != 0)
        {
            error = "The joining player clan must not own fiefs.";
            return false;
        }
        if (fixtureKingdom == null)
        {
            error = "The AI leader does not belong to a kingdom.";
            return false;
        }
        if (KingdomContainsClan(fixtureKingdom, playerClan))
        {
            error = $"Kingdom {fixtureKingdom.StringId} already contains the joining player clan.";
            return false;
        }
        if (!HasInitializedRuntimeCollections(fixtureKingdom))
        {
            error = $"Kingdom {fixtureKingdom.StringId} does not have fully initialized runtime state.";
            return false;
        }

        return true;
    }

    private static bool HasInitializedRuntimeCollections(Kingdom kingdom) =>
        kingdom._activePolicies != null &&
        kingdom._armies != null &&
        kingdom._clans != null &&
        kingdom._unresolvedDecisions != null &&
        kingdom._factionsAtWarWith != null &&
        kingdom._alliedKingdoms != null &&
        kingdom._fiefsCache != null &&
        kingdom._townsCache != null &&
        kingdom._settlementsCache != null &&
        kingdom._villagesCache != null &&
        kingdom._heroesCache != null &&
        kingdom._aliveLordsCache != null &&
        kingdom._deadLordsCache != null &&
        kingdom._warPartyComponentsCache != null &&
        kingdom.EncyclopediaText != null &&
        kingdom.EncyclopediaTitle != null &&
        kingdom.EncyclopediaRulerTitle != null;

    private static int GetClanFiefCount(Clan clan)
    {
        if (clan == null)
            return -1;

        var fiefs = new List<Town>();
        if (clan.Fiefs != null)
            fiefs.AddRange(clan.Fiefs.Where(fief => fief != null));
        if (clan._fiefsCache != null)
            fiefs.AddRange(clan._fiefsCache.Where(fief => fief != null));
        return fiefs.Distinct().Count();
    }

    private static Kingdom[] GetClanCollectionKingdoms(Clan clan) =>
        clan == null
            ? Array.Empty<Kingdom>()
            : Kingdom.All
                .Where(kingdom => KingdomContainsClan(kingdom, clan))
                .ToArray();

    private static bool HaveSameKingdoms(Kingdom[] left, Kingdom[] right) =>
        left.Length == right.Length && left.All(right.Contains);

    private static bool KingdomContainsClan(Kingdom kingdom, Clan clan) =>
        kingdom?._clans?.Contains(clan) == true;

    private static bool IsClanCurrentlyInKingdom(Clan clan, Kingdom kingdom) =>
        clan != null &&
        kingdom != null &&
        clan.Kingdom == kingdom &&
        clan.MapFaction == kingdom &&
        KingdomContainsClan(kingdom, clan);

    private static string DescribeClanCollections(Clan clan)
    {
        var kingdomIds = GetClanCollectionKingdoms(clan)
            .Select(kingdom => kingdom.StringId)
            .OrderBy(kingdomId => kingdomId)
            .ToArray();
        return kingdomIds.Length == 0 ? "none" : string.Join(";", kingdomIds);
    }

    private static bool TryGetConnectedPlayerParty(string controllerId, out MobileParty party, out string error)
    {
        party = null;
        error = null;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(controllerId, out var player) || !playerManager.IsConnected(player) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out party))
        {
            error = $"Unable to resolve connected player {controllerId}.";
            return false;
        }

        return true;
    }

    internal static bool HasSameMapFaction(MobileParty playerParty, MobileParty leaderParty)
    {
        var playerFaction = playerParty?.MapFaction;
        return playerFaction != null && playerFaction == leaderParty?.MapFaction;
    }

    private static MobileParty FindEligibleLeader(Clan playerClan, Settlement settlement) =>
        MobileParty.AllLordParties
            .Where(candidate => IsEligibleLeader(candidate, playerClan, settlement, out _))
            .OrderByDescending(candidate => candidate.Party.CalculateCurrentStrength())
            .FirstOrDefault();

    private static bool IsEligibleLeader(
        MobileParty candidate,
        Clan playerClan,
        Settlement settlement,
        out Kingdom fixtureKingdom)
    {
        fixtureKingdom = candidate?.MapFaction as Kingdom;
        return candidate?.IsActive == true &&
            !candidate.IsPlayerParty() &&
            candidate.Army == null &&
            candidate.AttachedTo == null &&
            candidate.MapEvent == null &&
            candidate.BesiegerCamp == null &&
            candidate.CurrentSettlement == null &&
            candidate.LeaderHero != null &&
            fixtureKingdom != null &&
            fixtureKingdom.IsAtWarWith(settlement.MapFaction) &&
            TryValidateMembershipFixture(playerClan, fixtureKingdom, out _);
    }

    private static string DescribeLeaderCandidates(Clan playerClan, Settlement settlement)
    {
        var lords = MobileParty.AllLordParties
            .Where(candidate => candidate.IsActive && !candidate.IsPlayerParty() && candidate.LeaderHero != null)
            .ToArray();
        var hostileKingdomLords = lords
            .Where(candidate =>
                candidate.MapFaction is Kingdom &&
                candidate.MapFaction.IsAtWarWith(settlement.MapFaction))
            .OrderByDescending(candidate => candidate.Party.CalculateCurrentStrength())
            .ToArray();
        var states = hostileKingdomLords
            .Take(12)
            .Select(candidate =>
                $"{candidate.StringId}[kingdom={candidate.MapFaction.StringId}," +
                $"army={candidate.Army != null},attached={candidate.AttachedTo != null}," +
                $"mapEvent={candidate.MapEvent != null},camp={candidate.BesiegerCamp != null}," +
                $"settlement={candidate.CurrentSettlement?.StringId ?? "none"}," +
                $"disorganized={candidate.IsDisorganized}]");

        return $"playerClan={playerClan?.StringId ?? "none"}, " +
            $"playerKingdom={playerClan?.Kingdom?.StringId ?? "none"}, " +
            $"hostileKingdomLords={hostileKingdomLords.Length}, " +
            $"hostileStates={string.Join(";", states)}.";
    }

    private static bool IsFixturePartyReady(MobileParty party) =>
        party?.IsActive == true &&
        party.CurrentSettlement == null &&
        party.Army == null &&
        party.AttachedTo == null &&
        party.MapEvent == null &&
        party.BesiegerCamp == null &&
        !party.IsDisorganized;

    private static bool IsFixturePartyRecoverable(MobileParty party) =>
        party != null &&
        party.CurrentSettlement == null &&
        party.Army == null &&
        party.AttachedTo == null &&
        party.MapEvent == null &&
        party.BesiegerCamp == null;

    private static void MoveAndHold(MobileParty party, CampaignVec2 position)
    {
        party.Position = position;
        party.SetMoveModeHold();
        party.ResetNavigationToHold();
        PublishBehavior(party, party.IsCurrentlyAtSea);
    }

    private static void PublishBehavior(MobileParty party, bool isCurrentlyAtSea)
    {
        MessageBroker.Instance.Publish(
            typeof(ArmySiegeWaitFixtureCommands),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: isCurrentlyAtSea,
                resetMovementToHold: false));
    }

    private static string DescribeParty(MobileParty party) =>
        party == null ? "null" :
        $"{party.StringId}|army={party.Army != null}|attached={party.AttachedTo?.StringId ?? "none"}|" +
        $"camp={party.BesiegerCamp != null}|disorganized={party.IsDisorganized}|" +
        $"atSea={party.IsCurrentlyAtSea}|active={party.IsActive}";

    private static string DescribeBehavior(PartyBehaviorUpdateData behavior) =>
        $"{behavior.NewAiBehavior}|{behavior.InteractablePointId ?? "none"}|" +
        $"{DescribePosition(behavior.BestTargetPoint)}|{behavior.DefaultBehavior}|" +
        $"{DescribePosition(behavior.TargetPosition)}|{behavior.DesiredAiNavigationType}|" +
        $"{behavior.TargetPartyId ?? "none"}|{behavior.TargetSettlementId ?? "none"}|" +
        $"{DescribePosition(behavior.MoveTargetPoint)}|{behavior.IsTargetingPort}|" +
        $"{behavior.PartyMoveMode}|{behavior.MoveTargetPartyId ?? "none"}|" +
        $"{behavior.IsInteractableAnchor}|{behavior.IsCurrentlyAtSea}";

    private static string DescribePosition(CampaignVec2 position) =>
        $"{position.X.ToString("R", CultureInfo.InvariantCulture)}:" +
        $"{position.Y.ToString("R", CultureInfo.InvariantCulture)}:{position.IsOnLand}";
}
