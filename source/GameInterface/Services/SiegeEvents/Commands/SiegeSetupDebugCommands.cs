#if DEBUG
using Autofac;
using Common;
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

    [CommandLineArgumentFunction("setup_join_active_assault", "coop.debug.siege")]
    public static string JoinActiveAssault(List<string> args)
    {
        if (!TryGetClientSettlement(args, out Settlement settlement, out string error))
            return Failure("join-active-assault", error);

        var party = MobileParty.MainParty;
        bool readyToJoin = party != null && party.BesiegerCamp != null && party.MapEvent == null &&
            settlement.Party.MapEvent?.IsSiegeAssault == true && PlayerEncounter.Current == null;
        if (readyToJoin)
            SiegeDebugCommand.JoinActiveAssault(args);

        var encounter = PlayerEncounter.Current;
        string currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
        bool joined = party?.MapEvent?.IsSiegeAssault == true && PlayerEncounter.Battle?.IsSiegeAssault == true;
        return Result(new
        {
            success = readyToJoin && joined && currentMenu == "menu_siege_strategies",
            action = "join-active-assault",
            settlement = settlement.StringId,
            readyToJoin,
            joined,
            currentMenu,
        });
    }

    [CommandLineArgumentFunction("setup_begin_assault", "coop.debug.siege")]
    public static string BeginAssault(List<string> args)
    {
        if (args.Count != 0)
            return Failure("begin-assault", "expected no arguments");
        if (ModInformation.IsServer)
            return Failure("begin-assault", "command must run on a client");

        var party = MobileParty.MainParty;
        var encounter = PlayerEncounter.Current;
        var settlement = party?.BesiegedSettlement;
        bool readyToBegin = party?.MapEvent?.IsSiegeAssault == true &&
            PlayerEncounter.Battle?.IsSiegeAssault == true && settlement != null &&
            PlayerSiege.PlayerSiegeEvent != null &&
            settlement.CurrentSiegeState == Settlement.SiegeState.OnTheWalls;
        if (!readyToBegin)
        {
            return Result(new
            {
                success = false,
                action = "begin-assault",
                readyToBegin,
                siegeAssault = party?.MapEvent?.IsSiegeAssault == true,
                encounterSiegeAssault = PlayerEncounter.Battle?.IsSiegeAssault == true,
                playerSiegeActive = PlayerSiege.PlayerSiegeEvent != null,
                settlement = settlement?.StringId,
            });
        }

        try
        {
            PlayerSiege.StartSiegeMission(settlement);
            return Result(new
            {
                success = true,
                action = "begin-assault",
                settlement = settlement.StringId,
                missionStartRequested = true,
            });
        }
        catch (Exception exception)
        {
            return Result(new
            {
                success = false,
                action = "begin-assault",
                settlement = settlement.StringId,
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
