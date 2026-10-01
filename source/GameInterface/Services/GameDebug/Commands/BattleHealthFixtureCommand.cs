#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Missions;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.GameDebug.Commands;

/// <summary>Requests production mission actions on the selected client's current battle.</summary>
public sealed class BattleHealthFixtureCommand : ICoopCommand
{
    public string Prefix => "coop.debug.map_event";
    public string Name => "health_fixture_request";
    public string Description => "Requests deployment, damage, rout or retreat from a current battle member.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "Connected battle member."),
        new ExpectedArgs("map_event_id", "Exact unresolved battle."),
        new ExpectedArgs("operation", "deploy, damage, rout or retreat."),
        new ExpectedArgs("agent_id", "Registered agent for damage or rout.", false),
        new ExpectedArgs("damage", "Positive integer damage for a blow.", false)
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient)
            return new CoopCommandResult(false, "Run this command on the server.", "command_failed");
        if (args.Count < 3 || args.Count > 5)
            return new CoopCommandResult(false, "Expected controller, map event and operation.", "command_failed");
        Guid agentId = Guid.Empty;
        int damage = 0;
        bool valid = (args[2] == "retreat" || args[2] == "deploy") && args.Count == 3;
        if ((args[2] == "damage" && args.Count == 5) || (args[2] == "rout" && args.Count == 4))
        {
            valid = Guid.TryParse(args[3], out agentId) && agentId != Guid.Empty;
            if (args[2] == "damage")
                valid = valid && int.TryParse(args[4], out damage) && damage > 0 && damage <= 10000;
        }
        if (!valid)
            return new CoopCommandResult(false, "Invalid operation, agent or damage.", "command_failed");
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var players) ||
            !players.TryGetPlayer(args[0], out var player) || !players.IsConnected(player) ||
            !players.TryGetPeer(args[0], out var peer) ||
            !ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var membership) ||
            !membership.IsControllerInMission(args[0]) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objects) ||
            !objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ||
            party.MapEvent == null || party.MapEvent.IsFinalized ||
            !objects.TryGetId(party.MapEvent, out string mapEventId) || mapEventId != args[1] ||
            !ContainerProvider.TryResolve<INetwork>(out var network))
            return new CoopCommandResult(false, "A connected participant in the exact unresolved battle is required.", "command_failed");
        network.Send(peer, new NetworkBattleHealthFixture(mapEventId, args[2], agentId, damage));
        return new CoopCommandResult(true, "Requested " + args[2] + ". Read the client state to verify completion.");
    }
}

/// <summary>Reads existing server reserve values without rebuilding or consuming them.</summary>
public sealed class BattleHealthReserveStateCommand : ICoopCommand
{
    public string Prefix => "coop.debug.map_event";
    public string Name => "health_reserve_state";
    public string Description => "Reads troop identities, health and supplied counts in the battle ledger.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("map_event_id", "Exact registered battle.")
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient || args.Count != 1 ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objects) ||
            !objects.TryGetObject<MapEvent>(args[0], out _) ||
            !ContainerProvider.TryResolve<IBattleTroopLedger>(out var ledger))
            return new CoopCommandResult(false, "An exact server battle is required.", "command_failed");
        var parties = ledger.GetParties(args[0]).Select(partyId =>
        {
            ledger.TryGetReserve(args[0], partyId, out var entries, out int supplied);
            return new { partyId, supplied, entries };
        }).ToArray();
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new { mapEventId = args[0], parties }));
    }
}

/// <summary>Server request applied only by the selected mission or agent owner.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkBattleHealthFixture : IEvent
{
    [ProtoMember(1)] public string MapEventId { get; }
    [ProtoMember(2)] public string Operation { get; }
    [ProtoMember(3)] public Guid AgentId { get; }
    [ProtoMember(4)] public int Damage { get; }

    public NetworkBattleHealthFixture(string mapEventId, string operation, Guid agentId, int damage)
    {
        MapEventId = mapEventId;
        Operation = operation;
        AgentId = agentId;
        Damage = damage;
    }
}
#endif
