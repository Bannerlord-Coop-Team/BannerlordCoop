using Autofac;
using Common;
using Common.Logging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

using GameInterface.Services.SiegeEngines;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;
namespace GameInterface.Services.SiegeEvents.Commands;

public class SiegeDebugCommand
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeDebugCommand>();

    [CommandLineArgumentFunction("leave_settlement", "coop.debug.siege")]
    public static string LeaveSettlement(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.siege.leave_settlement";
        }

        if (ModInformation.IsServer)
        {
            return "This command can only be used by a client";
        }

        var party = MobileParty.MainParty;
        if (party == null)
        {
            return "The local player party is unavailable";
        }

        if (party.CurrentSettlement == null)
        {
            return "The local player party is not in a settlement encounter";
        }

        var settlementName = party.CurrentSettlement.Name;
        PlayerLeaveSettlementPatch.RequestLeave();
        return $"Requested that the local player party leave {settlementName}";
    }

    // coop.debug.siege.start
    /// <summary>
    /// Starts a siege of a settlement, led by the given party or the strongest hostile lord party when
    /// none is given. Server only; the siege replicates to clients.
    /// </summary>
    /// <param name="args">first arg : settlementId ; optional second arg : besieger partyId</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("start", "coop.debug.siege")]
    public static string StartSiege(List<string> args)
    {
        if (args.Count < 1 || args.Count > 2)
        {
            return "Usage: coop.debug.siege.start <settlementId> [besiegerPartyId]";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        if (!settlement.IsFortification)
        {
            return $"{settlement.Name} is not a fortification";
        }

        if (settlement.SiegeEvent != null)
        {
            return $"{settlement.Name} is already under siege";
        }

        MobileParty besieger;
        if (args.Count == 2)
        {
            if (!objectManager.TryGetObject(args[1], out besieger))
            {
                return $"Party with id {args[1]} not found";
            }
        }
        else
        {
            besieger = MobileParty.AllLordParties
                .Where(party => !party.IsPlayerParty()
                    && party.MapFaction?.IsAtWarWith(settlement.MapFaction) == true
                    && party.LeaderHero != null && party.CurrentSettlement == null
                    && party.MapEvent == null && party.BesiegerCamp == null && party.Army == null)
                .OrderByDescending(party => party.Party.CalculateCurrentStrength())
                .FirstOrDefault();
            if (besieger == null)
            {
                return $"No hostile lord party available to besiege {settlement.Name}; pass a partyId explicitly";
            }
        }

        // Put the besieger at the gate and commit its AI to the siege.
        besieger.Position = settlement.GatePosition;
        besieger.SetMoveBesiegeSettlement(settlement, MobileParty.NavigationType.Default);
        Campaign.Current.SiegeEventManager.StartSiegeEvent(settlement, besieger);

        return $"{besieger.Name} ({besieger.StringId}) is now besieging {settlement.Name}";
    }

    /// <summary>
    /// Joins every connected player party to an active siege on the authoritative server.
    /// </summary>
    [CommandLineArgumentFunction("join_players", "coop.debug.siege")]
    public static string JoinPlayers(List<string> args)
    {
        if (args.Count != 2 || !int.TryParse(args[1], out int expectedPlayerCount) || expectedPlayerCount < 1)
        {
            return "Usage: coop.debug.siege.join_players <settlementId> <expectedPlayerCount>";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)
            || !ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
        {
            return "Unable to resolve siege fixture services";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        var camp = settlement.SiegeEvent?.BesiegerCamp;
        if (camp == null)
        {
            return $"{settlement.Name} is not under siege";
        }

        var connectedPlayers = playerManager.Players.Where(playerManager.IsConnected).ToArray();
        if (connectedPlayers.Length != expectedPlayerCount)
        {
            return $"Expected {expectedPlayerCount} connected players, found {connectedPlayers.Length}";
        }

        var parties = new List<(string ControllerId, string PartyId, MobileParty Party)>();
        foreach (var player in connectedPlayers)
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party))
            {
                return $"Unable to resolve player party {player.MobilePartyId}";
            }

            if (!party.IsActive || party.MapEvent != null || party.BesiegerCamp != null || party.CurrentSettlement != null)
            {
                return $"Player {player.ControllerId} is not clean for the fixture: " +
                    $"active={party.IsActive} mapEvent={party.MapEvent != null} " +
                    $"besiegerCamp={party.BesiegerCamp != null} settlement={party.CurrentSettlement?.StringId ?? "none"}";
            }

            if (!settlement.SiegeEvent.CanPartyJoinSide(party.Party, BattleSideEnum.Attacker))
            {
                return $"Player {player.ControllerId} cannot join the attacking side at {settlement.Name}";
            }

            parties.Add((player.ControllerId, player.MobilePartyId, party));
        }

        for (int i = 0; i < parties.Count; i++)
        {
            for (int j = i + 1; j < parties.Count; j++)
            {
                if (parties[i].Party.MapFaction.IsAtWarWith(parties[j].Party.MapFaction))
                {
                    return $"Players {parties[i].ControllerId} and {parties[j].ControllerId} cannot join the same siege side";
                }
            }
        }

        var joined = new List<string>();
        foreach (var item in parties)
        {
            siegeEventInterface.JoinSiegeCamp(item.Party, settlement);
            if (item.Party.BesiegerCamp != camp)
            {
                return $"Failed to join player {item.ControllerId} to the siege";
            }

            joined.Add($"{item.ControllerId}:{item.PartyId}");
        }

        return $"Joined {joined.Count} connected player parties to the siege of {settlement.Name}:\n" +
            string.Join(Environment.NewLine, joined);
    }

    [CommandLineArgumentFunction("stage_machines", "coop.debug.siege")]
    public static string StageMachines(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.stage_machines <settlementId>";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
        {
            return "Unable to resolve siege fixture services";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        var siegeEvent = settlement.SiegeEvent;
        if (siegeEvent?.BesiegerCamp == null)
        {
            return $"{settlement.Name} is not under siege";
        }

        var attacker = siegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker);
        if (!attacker.SiegeEngines.SiegePreparations.IsConstructed)
        {
            attacker.SiegeEngines.SiegePreparations.SetProgress(1f);
            siegeEvent.CreateSiegeObject(attacker.SiegeEngines.SiegePreparations, attacker);
        }

        var machines = new[]
        {
            (Side: BattleSideEnum.Attacker, Type: DefaultSiegeEngineTypes.Ram, Index: 0),
            (Side: BattleSideEnum.Attacker, Type: DefaultSiegeEngineTypes.Onager, Index: 0),
            (Side: BattleSideEnum.Defender, Type: DefaultSiegeEngineTypes.Ballista, Index: 0),
        };
        var staged = new List<string>();
        foreach (var machine in machines)
        {
            siegeEventInterface.DeploySiegeEngine(siegeEvent, machine.Side, machine.Type, machine.Index);
            var side = siegeEvent.GetSiegeEventSide(machine.Side);
            var slots = machine.Type.IsRanged
                ? side.SiegeEngines.DeployedRangedSiegeEngines
                : side.SiegeEngines.DeployedMeleeSiegeEngines;
            var progress = machine.Index < slots.Length ? slots[machine.Index] : null;
            if (progress?.SiegeEngine != machine.Type)
            {
                return $"Failed to stage {machine.Type.StringId} for {machine.Side}";
            }

            bool needsSiegeObject = !progress.IsConstructed
                || (machine.Type.IsRanged && progress.RangedSiegeEngine == null);
            if (!progress.IsConstructed)
            {
                progress.SetProgress(1f);
            }
            if (progress.IsBeingRedeployed)
            {
                progress.SetRedeploymentProgress(1f);
            }
            if (needsSiegeObject)
            {
                siegeEvent.CreateSiegeObject(progress, side);
            }
            if (!progress.IsActive)
            {
                return $"Failed to activate {machine.Type.StringId} for {machine.Side}";
            }

            staged.Add($"{machine.Side}:{machine.Type.StringId}[{machine.Index}]");
        }

        return $"Staged {staged.Count} constructed siege engines at {settlement.Name}: {string.Join(", ", staged)}";
    }

    /// <summary>
    /// Starts the wall assault for an existing AI-led siege. Server only; the resulting map event uses the
    /// same authoritative action as campaign AI.
    /// </summary>
    [CommandLineArgumentFunction("assault", "coop.debug.siege")]
    public static string StartAssault(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.assault <settlementId>";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        var attacker = settlement.SiegeEvent?.BesiegerCamp?.LeaderParty;
        if (attacker == null)
        {
            return $"{settlement.Name} has no active siege leader";
        }

        if (attacker.IsPlayerParty())
        {
            return $"{settlement.Name} is player-led; this command only starts AI assaults";
        }

        if (attacker.MapEvent != null)
        {
            return $"{attacker.Name} is already in an active map event";
        }

        if (settlement.Party.MapEvent != null)
        {
            return $"{settlement.Name} already has an active map event";
        }

        StartBattleAction.ApplyStartAssaultAgainstWalls(attacker, settlement);

        var mapEvent = settlement.Party.MapEvent;
        if (mapEvent?.IsSiegeAssault != true)
        {
            return $"Failed to start an AI siege assault against {settlement.Name}";
        }

        var mapEventId = objectManager.TryGetId(mapEvent, out string id) ? id : mapEvent.StringId;
        return $"Started AI siege assault by {attacker.Name} against {settlement.Name} (MapEvent {mapEventId})";
    }

    // coop.debug.siege.list
    /// <summary>
    /// Lists the active sieges with their preparation progress and deployed engine counts.
    /// </summary>
    /// <param name="args">no args</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("list", "coop.debug.siege")]
    public static string ListSieges(List<string> args)
    {
        var siegeEvents = SiegeContainerLookup.ActiveSieges().ToList();
        if (siegeEvents.Count == 0)
        {
            return "No active sieges";
        }

        var sb = new StringBuilder();
        foreach (var siegeEvent in siegeEvents)
        {
            var camp = siegeEvent.BesiegerCamp;
            sb.AppendLine($"{siegeEvent.BesiegedSettlement?.Name} ({siegeEvent.BesiegedSettlement?.StringId}): " +
                $"leader={camp?.LeaderParty?.Name} preparation={camp?.SiegeEngines?.SiegePreparations?.Progress:0.00} " +
                $"attackerEngines={camp?.SiegeEngines?.DeployedSiegeEngines?.Count ?? 0} " +
                $"defenderEngines={siegeEvent.BesiegedSettlement?.SiegeEngines?.DeployedSiegeEngines?.Count ?? 0} " +
                $"strategy={camp?.SiegeStrategy?.Name}");
        }

        return sb.ToString();
    }

    // coop.debug.siege.dump_party <heroName|main|partyId>
    /// <summary>
    /// Dumps a party's siege-relevant state — CurrentSettlement, BesiegerCamp, BesiegedSettlement, Position —
    /// with its co-op registry id. Read-only; run on the SERVER and BOTH clients right after a siege capture and
    /// compare the co-besieger's party. A CurrentSettlement set on the server/host but null on the co-besieger's
    /// own client (party still at the camp Position, its BesiegerCamp maybe uncleared) pinpoints why it is left
    /// outside. Resolve by "main" (that client's own party), a coop id, or a leader-hero name.
    /// </summary>
    /// <param name="args">first arg: heroName | main | partyId</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("dump_party", "coop.debug.siege")]
    public static string DumpParty(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.dump_party <heroName|main|partyId>";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        string arg = args[0];
        MobileParty party;
        if (arg.Equals("main", StringComparison.OrdinalIgnoreCase))
        {
            party = MobileParty.MainParty;
        }
        else if (!objectManager.TryGetObject(arg, out party))
        {
            party = MobileParty.All.FirstOrDefault(p => p.LeaderHero?.Name != null
                && p.LeaderHero.Name.ToString().IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (party == null)
        {
            return $"No party found for '{arg}' (use \"main\", a coop id, or a leader-hero name)";
        }

        var settlement = party.CurrentSettlement ?? party.BesiegedSettlement;

        var sb = new StringBuilder();
        sb.AppendLine($"[{(ModInformation.IsServer ? "SERVER" : "CLIENT")}] siege state: {party.Name} ({party.StringId}) coopId={IdOf(objectManager, party)}");
        sb.AppendLine($"  Leader: {party.LeaderHero?.Name?.ToString() ?? "null"}  IsActive: {party.IsActive}");
        var pos = party.GetPosition2D;
        sb.AppendLine($"  Position2D: {pos.x:0.00}, {pos.y:0.00}");
        sb.AppendLine($"  CurrentSettlement: {Describe(party.CurrentSettlement)}");
        sb.AppendLine($"  BesiegerCamp: {(party.BesiegerCamp != null ? "present" : "null")}");
        sb.AppendLine($"  BesiegedSettlement: {Describe(party.BesiegedSettlement)}");
        sb.AppendLine($"  MapEvent: {party.MapEvent?.EventType.ToString() ?? "null"}  ShortTermBehavior: {party.ShortTermBehavior}");

        if (settlement != null)
        {
            sb.AppendLine($"  -- {settlement.Name} ({settlement.StringId}): owner={settlement.OwnerClan?.Name?.ToString() ?? "null"} " +
                $"underSiege={settlement.IsUnderSiege} siegeEvent={(settlement.SiegeEvent != null ? "active" : "null")}");
        }

        return sb.ToString();
    }

    // coop.debug.siege.dump_engines
    /// <summary>
    /// Dumps every active siege's engines with their co-op registry id, hitpoints, progress and aim.
    /// Read-only; run on BOTH the server and a client and compare. Matching ids with matching hitpoints
    /// means the sync works (a stale on-screen value is then a UI-refresh bug); a differing or UNREGISTERED
    /// id on the client means it is rendering a local duplicate the server's hitpoint/aim updates never reach.
    /// </summary>
    /// <param name="args">no args</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("dump_engines", "coop.debug.siege")]
    public static string DumpEngines(List<string> args)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        var siegeEvents = SiegeContainerLookup.ActiveSieges().ToList();
        if (siegeEvents.Count == 0)
        {
            return "No active sieges";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[{(ModInformation.IsServer ? "SERVER" : "CLIENT")}] siege engines:");

        foreach (var siegeEvent in siegeEvents)
        {
            sb.AppendLine($"Siege of {siegeEvent.BesiegedSettlement?.StringId} (SiegeEvent {IdOf(objectManager, siegeEvent)})");
            DumpSide(sb, objectManager, "ATTACKER", siegeEvent.BesiegerCamp?.SiegeEngines);
            DumpSide(sb, objectManager, "DEFENDER", siegeEvent.BesiegedSettlement?.SiegeEngines);
        }

        var dump = sb.ToString();
        // Into the Coop log too, so the machines' dumps can be compared from their log files.
        Logger.Information("[EngineDump]\n{Dump}", dump);
        return dump;
    }

    private static void DumpSide(StringBuilder sb, IObjectManager objectManager, string label, SiegeEnginesContainer container)
    {
        if (container == null)
        {
            sb.AppendLine($"  {label}: no container");
            return;
        }

        DumpEngine(sb, objectManager, $"  {label} prep    ", container.SiegePreparations);
        foreach (var engine in container.DeployedSiegeEngines)
        {
            DumpEngine(sb, objectManager, $"  {label} deployed", engine);
        }
        foreach (var engine in container.ReservedSiegeEngines)
        {
            DumpEngine(sb, objectManager, $"  {label} reserve ", engine);
        }
    }

    private static void DumpEngine(StringBuilder sb, IObjectManager objectManager, string slot, SiegeEngineConstructionProgress engine)
    {
        if (engine == null) return;

        var ranged = engine.RangedSiegeEngine;
        var aim = ranged != null ? $"{ranged.CurrentTargetType}[{ranged.CurrentTargetIndex}]" : "none";
        sb.AppendLine($"{slot}: type={engine.SiegeEngine?.StringId} id={IdOf(objectManager, engine)} " +
            $"hp={engine.Hitpoints:0}/{engine.MaxHitPoints:0} prog={engine.Progress:0.00} redeploy={engine.RedeploymentProgress:0.00} aim={aim}");
    }

    private static string IdOf(IObjectManager objectManager, object obj)
    {
        return obj != null && objectManager.TryGetId(obj, out var id) ? id : "UNREGISTERED";
    }

    private static string Describe(Settlement settlement)
        => settlement != null ? $"{settlement.Name} ({settlement.StringId})" : "null";

    // coop.debug.siege.dump_machines
    /// <summary>
    /// Dumps the siege weapons and deployment points of the current mission — the exact flags the use
    /// prompt and the AI read — sorted so two clients' dumps diff line by line, and written to the Coop
    /// log for post-run comparison. Pass "all" to include every usable machine. Read-only.
    /// </summary>
    [CommandLineArgumentFunction("dump_machines", "coop.debug.siege")]
    public static string DumpMachines(List<string> args)
    {
        var mission = TaleWorlds.MountAndBlade.Mission.Current;
        if (mission == null)
        {
            return "No mission is running";
        }

        bool includeAll = args.Count > 0 && args[0] == "all";
        var lines = new List<string>();
        foreach (var missionObject in mission.MissionObjects)
        {
            if (missionObject is TaleWorlds.MountAndBlade.UsableMachine machine
                && (includeAll || machine is TaleWorlds.MountAndBlade.SiegeWeapon))
            {
                int deactivatedPoints = 0, usedPoints = 0;
                foreach (var point in machine.StandingPoints)
                {
                    if (point.IsDeactivated) deactivatedPoints++;
                    if (point.UserAgent != null) usedPoints++;
                }

                lines.Add($"machine {machine.Id.Id:D5} {machine.GetType().Name,-16}" +
                    $" disabled={(machine.IsDisabled ? 1 : 0)} visible={(machine.GameEntity.IsVisibleIncludeParents() ? 1 : 0)}" +
                    $" deactivated={(machine.IsDeactivated ? 1 : 0)} aiOff={(machine.IsDisabledForAI ? 1 : 0)}" +
                    $" simLocal={(SiegeMissionAuthorityGate.IsMachineSimulatedLocally(machine.Id.Id) ? 1 : 0)}" +
                    $" pts={machine.StandingPoints.Count} ptsOff={deactivatedPoints} ptsUsed={usedPoints}");
            }
            else if (missionObject is TaleWorlds.MountAndBlade.DeploymentPoint deploymentPoint)
            {
                var variants = deploymentPoint._weapons?
                    .Where(weapon => weapon != null)
                    .ToArray() ?? Array.Empty<TaleWorlds.MountAndBlade.SynchedMissionObject>();
                var deployedWeapon = deploymentPoint.DeployedWeapon;
                var deployedWeaponType = deployedWeapon == null
                    ? "none"
                    : TaleWorlds.MountAndBlade.Missions.MissionSiegeWeaponsController.GetWeaponType(deployedWeapon)?.Name
                        ?? deployedWeapon.GetType().Name;
                lines.Add($"point   {deploymentPoint.Id.Id:D5} {deploymentPoint.Side,-16}" +
                    $" disabled={(deploymentPoint.IsDisabled ? 1 : 0)} deployed={(deploymentPoint.IsDeployed ? 1 : 0)}" +
                    $" weapon={deployedWeaponType}" +
                    $" weaponId={(deployedWeapon != null ? deployedWeapon.Id.Id.ToString("D5") : "none")}" +
                    $" weaponVisible={(deployedWeapon?.GameEntity.IsVisibleIncludeParents() == true ? 1 : 0)}" +
                    $" variants={variants.Length} variantsVisible={variants.Count(weapon => weapon.GameEntity.IsVisibleIncludeParents())}");
            }
        }

        lines.Sort(StringComparer.Ordinal);
        lines.Insert(0, $"siege={mission.IsSiegeBattle} authority={SiegeMissionAuthorityGate.IsLocalAuthority} known={SiegeMissionAuthorityGate.IsAuthorityKnown} entries={lines.Count}");

        var dump = string.Join(Environment.NewLine, lines);
        Logger.Information("[MachineDump]\n{Dump}", dump);
        return dump;
    }
}
