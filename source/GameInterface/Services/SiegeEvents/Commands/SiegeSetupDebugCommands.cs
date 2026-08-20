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
using TaleWorlds.CampaignSystem.Encounters;
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
        var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
        return Result(new
        {
            success = party != null && mapEvent?.IsSiegeAssault == true,
            action = "state",
            settlement = settlement.StringId,
            siegeAssault = mapEvent?.IsSiegeAssault == true,
            partyInBesiegerCamp = party?.BesiegerCamp != null,
            partyInMapEvent = party?.MapEvent?.IsSiegeAssault == true,
            encounterActive = encounter != null,
            encounterSiegeAssault = PlayerEncounter.Battle?.IsSiegeAssault == true,
            playerSiegeActive = party != null && PlayerSiege.PlayerSiegeEvent != null,
            currentMenu,
            currentSiegeState = settlement.CurrentSiegeState.ToString(),
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
