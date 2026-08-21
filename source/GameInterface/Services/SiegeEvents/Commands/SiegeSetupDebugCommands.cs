#if DEBUG
using Autofac;
using Common;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.SiegeEvents.Commands;

internal static class SiegeSetupDebugCommands
{
    [CommandLineArgumentFunction("setup_start", "coop.debug.siege")]
    public static string Start(List<string> args)
    {
        if (!TryGetServerSettlement(args, 1, out _, out Settlement settlement, out string error))
            return Failure("start", error);

        bool wasClean = settlement.SiegeEvent == null;
        if (wasClean)
            SiegeDebugCommand.StartSiege(args);

        var siegeEvent = settlement.SiegeEvent;
        var leader = siegeEvent?.BesiegerCamp?.LeaderParty;
        return Result(new
        {
            success = wasClean && siegeEvent != null && leader != null,
            action = "start",
            settlement = settlement.StringId,
            wasClean,
            siegeActive = siegeEvent != null,
            leaderPartyId = leader?.StringId,
        });
    }

    [CommandLineArgumentFunction("setup_stage_machines", "coop.debug.siege")]
    public static string StageMachines(List<string> args)
    {
        if (!TryGetServerSettlement(args, 1, out _, out Settlement settlement, out string error))
            return Failure("stage-machines", error);

        bool siegeReady = settlement.SiegeEvent?.BesiegerCamp != null;
        if (siegeReady)
            SiegeDebugCommand.StageMachines(args);

        var siegeEvent = settlement.SiegeEvent;
        var attacker = siegeEvent?.GetSiegeEventSide(BattleSideEnum.Attacker);
        var defender = siegeEvent?.GetSiegeEventSide(BattleSideEnum.Defender);
        int attackerEngines = attacker?.SiegeEngines?.DeployedSiegeEngines?.Count ?? 0;
        int defenderEngines = defender?.SiegeEngines?.DeployedSiegeEngines?.Count ?? 0;
        bool preparationConstructed = attacker?.SiegeEngines?.SiegePreparations?.IsConstructed == true;
        return Result(new
        {
            success = siegeReady && preparationConstructed && attackerEngines >= 2 && defenderEngines >= 1,
            action = "stage-machines",
            settlement = settlement.StringId,
            siegeReady,
            preparationConstructed,
            attackerEngines,
            defenderEngines,
        });
    }

    [CommandLineArgumentFunction("setup_stage_defenders", "coop.debug.siege")]
    public static string StageDefenders(List<string> args)
    {
        if (args.Count != 2 || !int.TryParse(args[1], out int expectedPlayerCount) || expectedPlayerCount < 1)
            return Failure("stage-defenders", "expected settlement id and a positive player count");
        if (!TryGetServerSettlement(args, 2, out IObjectManager objectManager, out Settlement settlement, out string error))
            return Failure("stage-defenders", error);
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return Failure("stage-defenders", "player manager is unavailable");

        var connectedPlayers = playerManager.Players.Where(playerManager.IsConnected).ToArray();
        var playerParties = new List<MobileParty>();
        foreach (var player in connectedPlayers)
        {
            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
                return Failure("stage-defenders", "a connected player party was not found");
            playerParties.Add(party);
        }

        bool cleanBeforeStage = settlement.IsFortification && settlement.SiegeEvent == null &&
            settlement.Party?.MapEvent == null && connectedPlayers.Length == expectedPlayerCount &&
            playerParties.All(party => party.IsActive && party.Party != null && party.MapEvent == null &&
                party.BesiegerCamp == null && party.CurrentSettlement == null);
        if (cleanBeforeStage)
        {
            foreach (var party in playerParties)
                EnterSettlementAction.ApplyForParty(party, settlement);
        }

        int stagedPlayerCount = playerParties.Count(party => party.CurrentSettlement == settlement &&
            party.MapEvent == null && party.BesiegerCamp == null);
        return Result(new
        {
            success = cleanBeforeStage && stagedPlayerCount == expectedPlayerCount,
            action = "stage-defenders",
            settlement = settlement.StringId,
            expectedPlayerCount,
            connectedPlayerCount = connectedPlayers.Length,
            cleanBeforeStage,
            stagedPlayerCount,
            siegeActive = settlement.SiegeEvent != null,
            settlementMapEventActive = settlement.Party?.MapEvent != null,
        });
    }

    [CommandLineArgumentFunction("setup_join_players", "coop.debug.siege")]
    public static string JoinPlayers(List<string> args)
    {
        if (args.Count != 2 || !int.TryParse(args[1], out int expectedPlayerCount) || expectedPlayerCount < 1)
            return Failure("join-players", "expected settlement id and a positive player count");
        if (!TryGetServerSettlement(args, 2, out IObjectManager objectManager, out Settlement settlement, out string error))
            return Failure("join-players", error);
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return Failure("join-players", "player manager is unavailable");

        var camp = settlement.SiegeEvent?.BesiegerCamp;
        var connectedPlayers = playerManager.Players.Where(playerManager.IsConnected).ToArray();
        bool cleanBeforeJoin = camp != null && connectedPlayers.Length == expectedPlayerCount &&
            connectedPlayers.All(player =>
                objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) &&
                party.IsActive && party.MapEvent == null && party.BesiegerCamp == null &&
                party.CurrentSettlement == null);
        if (cleanBeforeJoin)
            SiegeDebugCommand.JoinPlayers(args);

        int joinedPlayerCount = connectedPlayers.Count(player =>
            objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party) &&
            party.BesiegerCamp == camp);
        return Result(new
        {
            success = cleanBeforeJoin && joinedPlayerCount == expectedPlayerCount,
            action = "join-players",
            settlement = settlement.StringId,
            expectedPlayerCount,
            connectedPlayerCount = connectedPlayers.Length,
            cleanBeforeJoin,
            joinedPlayerCount,
        });
    }

    [CommandLineArgumentFunction("setup_assault", "coop.debug.siege")]
    public static string StartAssault(List<string> args)
    {
        if (!TryGetServerSettlement(args, 1, out IObjectManager objectManager, out Settlement settlement, out string error))
            return Failure("start-assault", error);

        var leader = settlement.SiegeEvent?.BesiegerCamp?.LeaderParty;
        bool readyToStart = leader != null && leader.MapEvent == null && settlement.Party.MapEvent == null;
        if (readyToStart)
            SiegeDebugCommand.StartAssault(args);

        var mapEvent = settlement.Party.MapEvent;
        string mapEventId = mapEvent != null && objectManager.TryGetId(mapEvent, out string id)
            ? id
            : mapEvent?.StringId;
        return Result(new
        {
            success = readyToStart && mapEvent?.IsSiegeAssault == true && leader.MapEvent == mapEvent,
            action = "start-assault",
            settlement = settlement.StringId,
            readyToStart,
            siegeAssault = mapEvent?.IsSiegeAssault == true,
            mapEventId,
        });
    }

    [CommandLineArgumentFunction("setup_state", "coop.debug.siege")]
    public static string State(List<string> args)
    {
        if (!TryGetClientSettlement(args, out Settlement settlement, out string error))
            return Failure("state", error);

        var party = MobileParty.MainParty;
        var encounter = PlayerEncounter.Current;
        var mapEvent = settlement.Party.MapEvent;
        var defenderSide = mapEvent?.DefenderSide;
        var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
        bool localDefenderInsideCastle = party?.CurrentSettlement == settlement;
        bool localPartyOnDefenderSide = defenderSide != null && party?.Party?.MapEventSide == defenderSide;
        bool encounterOwnsMapEvent = encounter != null && ReferenceEquals(PlayerEncounter.Battle, mapEvent);
        return Result(new
        {
            success = party != null && localDefenderInsideCastle && localPartyOnDefenderSide &&
                encounterOwnsMapEvent && mapEvent?.IsSiegeAssault == true,
            action = "state",
            settlement = settlement.StringId,
            siegeAssault = mapEvent?.IsSiegeAssault == true,
            localDefenderInsideCastle,
            localPartyOnDefenderSide,
            partyInBesiegerCamp = party?.BesiegerCamp != null,
            partyInMapEvent = party?.MapEvent?.IsSiegeAssault == true,
            encounterActive = encounter != null,
            encounterOwnsMapEvent,
            encounterSiegeAssault = PlayerEncounter.Battle?.IsSiegeAssault == true,
            playerSiegeActive = party != null && PlayerSiege.PlayerSiegeEvent != null,
            currentMenu,
            currentSiegeState = settlement.CurrentSiegeState.ToString(),
        });
    }

    [CommandLineArgumentFunction("setup_defender_staging", "coop.debug.siege")]
    public static string DefenderStaging(List<string> args)
    {
        if (!TryGetClientSettlement(args, out Settlement settlement, out string error))
            return Failure("defender-staging", error);

        var party = MobileParty.MainParty;
        var settlementMapEvent = settlement.Party?.MapEvent;
        bool stagingReady = party?.Party != null && party.CurrentSettlement == settlement &&
            party.MapEvent == null && party.BesiegerCamp == null && settlementMapEvent == null;
        return Result(new
        {
            success = stagingReady,
            action = "defender-staging",
            settlement = settlement.StringId,
            localDefenderInsideCastle = party?.CurrentSettlement == settlement,
            localPartyReady = party?.Party != null,
            localPartyMapEventActive = party?.MapEvent != null,
            localPartyInBesiegerCamp = party?.BesiegerCamp != null,
            settlementMapEventActive = settlementMapEvent != null,
        });
    }

    [CommandLineArgumentFunction("setup_defender_topology", "coop.debug.siege")]
    public static string DefenderTopology(List<string> args)
    {
        if (!TryGetClientSettlement(args, out Settlement settlement, out string error))
            return Failure("defender-topology", error);

        var party = MobileParty.MainParty;
        var mapEvent = settlement.Party?.MapEvent;
        var defenderSide = mapEvent?.DefenderSide;
        var attackerSide = mapEvent?.AttackerSide;
        var defenderLeader = defenderSide?.LeaderParty;
        var attackerLeader = attackerSide?.LeaderParty;
        bool mapEventActive = mapEvent != null &&
            !mapEvent.IsFinalized && mapEvent.BattleState == BattleState.None;
        bool localDefenderInsideCastle = party?.CurrentSettlement == settlement;
        bool localPartyOnDefenderSide = defenderSide != null && party?.Party?.MapEventSide == defenderSide;
        bool topologyReady = party?.Party != null && mapEvent?.IsSiegeAssault == true && mapEventActive &&
            defenderSide != null && attackerSide != null && defenderLeader != null && attackerLeader != null &&
            localDefenderInsideCastle;
        return Result(new
        {
            success = topologyReady,
            action = "defender-topology",
            settlement = settlement.StringId,
            siegeAssault = mapEvent?.IsSiegeAssault == true,
            mapEventActive,
            defenderSideReady = defenderSide != null,
            attackerSideReady = attackerSide != null,
            defenderLeaderReady = defenderLeader != null,
            attackerLeaderReady = attackerLeader != null,
            localPartyReady = party?.Party != null,
            localDefenderInsideCastle,
            localPartyInExpectedMapEvent = party?.MapEvent == mapEvent,
            localPartyOnDefenderSide,
        });
    }

    [CommandLineArgumentFunction("setup_join_defender", "coop.debug.siege")]
    public static string JoinDefender(List<string> args)
    {
        if (!TryGetClientSettlement(args, out Settlement settlement, out string error))
            return Failure("join-defender", error);

        var party = MobileParty.MainParty;
        var mapEvent = settlement.Party?.MapEvent;
        var defenderSide = mapEvent?.DefenderSide;
        var attackerSide = mapEvent?.AttackerSide;
        var defenderLeader = defenderSide?.LeaderParty;
        var attackerLeader = attackerSide?.LeaderParty;
        bool mapEventActive = mapEvent != null &&
            !mapEvent.IsFinalized && mapEvent.BattleState == BattleState.None;
        bool localDefenderInsideCastle = party?.CurrentSettlement == settlement;
        bool localPartyInExpectedMapEvent = party?.MapEvent == mapEvent;
        bool localPartyOnDefenderSide = defenderSide != null && party?.Party?.MapEventSide == defenderSide;
        bool localPartyOnUnexpectedMapEvent = party?.MapEvent != null && party.MapEvent != mapEvent;
        bool localPartyOnUnexpectedMapEventSide = party?.Party?.MapEventSide != null && !localPartyOnDefenderSide;
        bool topologyReady = party?.Party != null && mapEvent?.IsSiegeAssault == true && mapEventActive &&
            defenderSide != null && attackerSide != null && defenderLeader != null && attackerLeader != null &&
            localDefenderInsideCastle;
        if (!topologyReady)
        {
            return Result(new
            {
                success = false,
                action = "join-defender",
                settlement = settlement.StringId,
                reason = "defender topology is not ready",
                siegeAssault = mapEvent?.IsSiegeAssault == true,
                mapEventActive,
                defenderSideReady = defenderSide != null,
                attackerSideReady = attackerSide != null,
                defenderLeaderReady = defenderLeader != null,
                attackerLeaderReady = attackerLeader != null,
                localPartyReady = party?.Party != null,
                localDefenderInsideCastle,
            });
        }
        if (localPartyOnUnexpectedMapEvent || localPartyOnUnexpectedMapEventSide || party.BesiegerCamp != null)
        {
            return Result(new
            {
                success = false,
                action = "join-defender",
                settlement = settlement.StringId,
                reason = "the local player is already in another siege setup state",
                localPartyOnUnexpectedMapEvent,
                localPartyOnUnexpectedMapEventSide,
                localPartyOnDefenderSide,
                localPartyInBesiegerCamp = party.BesiegerCamp != null,
            });
        }

        try
        {
            if (localPartyInExpectedMapEvent)
            {
                if (PlayerEncounter.Current == null || !ReferenceEquals(PlayerEncounter.Battle, mapEvent))
                {
                    PlayerEncounter.Start();
                    PlayerEncounter.Init();
                }
            }
            else
            {
                PlayerEncounter.Start();
                PlayerEncounter.Current.SetupFields(attackerLeader, party.Party);
                PlayerEncounter.JoinBattle(BattleSideEnum.Defender);
            }
        }
        catch (Exception exception)
        {
            return Result(new
            {
                success = false,
                action = "join-defender",
                settlement = settlement.StringId,
                exceptionType = exception.GetType().FullName,
                exceptionMessage = exception.Message,
            });
        }

        var encounter = PlayerEncounter.Current;
        bool encounterOwnsMapEvent = encounter != null && ReferenceEquals(PlayerEncounter.Battle, mapEvent);
        bool joinedPartyOnDefenderSide = party.Party.MapEventSide == defenderSide;
        return Result(new
        {
            success = encounterOwnsMapEvent && joinedPartyOnDefenderSide && party.CurrentSettlement == settlement &&
                party.BesiegerCamp == null,
            action = "join-defender",
            settlement = settlement.StringId,
            topologyReady = true,
            localDefenderInsideCastle = party.CurrentSettlement == settlement,
            existingMapEventParty = localPartyInExpectedMapEvent,
            localPartyOnDefenderSide = joinedPartyOnDefenderSide,
            encounterActive = encounter != null,
            encounterOwnsMapEvent,
            encounterSiegeAssault = PlayerEncounter.Battle?.IsSiegeAssault == true,
        });
    }

    [CommandLineArgumentFunction("setup_start_mission", "coop.debug.siege")]
    public static string StartMission(List<string> args)
    {
        if (args.Count != 0)
            return Failure("start-mission", "expected no arguments");
        if (ModInformation.IsServer)
            return Failure("start-mission", "command must run on a client");

        var party = MobileParty.MainParty;
        var mapEvent = party?.MapEvent;
        if (mapEvent?.IsSiegeAssault != true)
        {
            return Result(new
            {
                success = false,
                action = "start-mission",
                siegeAssault = false,
            });
        }
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetId(mapEvent, out var mapEventId) ||
            !objectManager.TryGetId(party, out var partyId))
            return Failure("start-mission", "unable to resolve the local battle ids");
        var coordinator = BattleStartCoordinator.Instance;
        if (coordinator == null)
            return Failure("start-mission", "battle start coordinator is unavailable");

        try
        {
            bool accepted = coordinator.RequestBlocking(BattleStartMode.Mission, mapEventId, partyId);
            return Result(new
            {
                success = accepted,
                action = "start-mission",
                mapEventId,
                requestAccepted = accepted,
            });
        }
        catch (Exception exception)
        {
            return Result(new
            {
                success = false,
                action = "start-mission",
                mapEventId,
                exceptionType = exception.GetType().FullName,
                exceptionMessage = exception.Message,
            });
        }
    }

    private static bool TryGetServerSettlement(
        List<string> args,
        int expectedArgumentCount,
        out IObjectManager objectManager,
        out Settlement settlement,
        out string error)
    {
        objectManager = null;
        settlement = null;
        error = null;
        if (args.Count != expectedArgumentCount)
        {
            error = "unexpected argument count";
            return false;
        }
        if (ModInformation.IsClient)
        {
            error = "command must run on the server";
            return false;
        }
        if (!ContainerProvider.TryResolve<IObjectManager>(out objectManager))
        {
            error = "object manager is unavailable";
            return false;
        }
        if (!objectManager.TryGetObject<Settlement>(args[0], out settlement))
        {
            error = "settlement was not found";
            return false;
        }
        return true;
    }

    private static bool TryGetClientSettlement(List<string> args, out Settlement settlement, out string error)
    {
        settlement = null;
        error = null;
        if (args.Count != 1)
        {
            error = "expected a settlement id";
            return false;
        }
        if (ModInformation.IsServer)
        {
            error = "command must run on a client";
            return false;
        }
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObject<Settlement>(args[0], out settlement))
        {
            error = "settlement was not found";
            return false;
        }
        return true;
    }

    private static string Failure(string action, string reason)
    {
        return Result(new
        {
            success = false,
            action,
            reason,
        });
    }

    private static string Result(object value)
    {
        return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(value);
    }
}
#endif
