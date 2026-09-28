#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Missions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Commands;
using LiteNetLib;
using Missions.Battles;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace Missions.Diagnostics;

public interface IAgentHitSoundFixtureHandler : IHandler
{
    string Snapshot();
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkAgentHitSoundFixtureRequest : ICommand
{
    [ProtoMember(1)] public string MapEventId { get; }
    [ProtoMember(2)] public string ControllerId { get; }
    [ProtoMember(3)] public string RequestId { get; }
    [ProtoMember(4)] public string Action { get; }

    public NetworkAgentHitSoundFixtureRequest(string mapEventId, string controllerId,
        string requestId, string action)
    {
        MapEventId = mapEventId;
        ControllerId = controllerId;
        RequestId = requestId;
        Action = action;
    }
}

internal sealed class AgentHitSoundFixtureHandler : IAgentHitSoundFixtureHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ICoopCommandArgsFactory argsFactory;
    private readonly HashSet<string> requests = new HashSet<string>();
    private object lastResult;

    public AgentHitSoundFixtureHandler(IMessageBroker messageBroker,
        ICoopCommandArgsFactory argsFactory)
    {
        this.messageBroker = messageBroker;
        this.argsFactory = argsFactory;
        messageBroker.Subscribe<NetworkAgentHitSoundFixtureRequest>(Handle);
    }

    public void Dispose() =>
        messageBroker.Unsubscribe<NetworkAgentHitSoundFixtureRequest>(Handle);

    public string Snapshot() => JsonConvert.SerializeObject(lastResult ?? new { status = "none" });

    private void Handle(MessagePayload<NetworkAgentHitSoundFixtureRequest> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() => Apply(payload.What), context: nameof(AgentHitSoundFixtureHandler));
    }

    private void Apply(NetworkAgentHitSoundFixtureRequest request)
    {
        Mission mission = Mission.Current;
        var session = mission?.GetMissionBehavior<CoopBattleController>()?.Session;
        if (session == null || session.InstanceId != request.MapEventId ||
            session.OwnControllerId != request.ControllerId ||
            string.IsNullOrWhiteSpace(request.RequestId) || requests.Contains(request.RequestId) ||
            requests.Count >= 64 || (request.Action != "begin" && request.Action != "charge"))
            return;

        requests.Add(request.RequestId);
        ICoopCommandArgs empty = argsFactory.FromValues(Array.Empty<string>());
        CoopCommandResult result = request.Action == "begin"
            ? new MapEventDebugCommands.LateJoinModeBeginFieldBattleCoopCommand().ProcessCommand(empty)
            : new BattleDebugCommands.ChargeOwnedFormationsCoopCommand().ProcessCommand(empty);
        lastResult = new
        {
            request.MapEventId,
            request.ControllerId,
            request.RequestId,
            request.Action,
            result.Succeeded,
            result.Output,
            result.ErrorCode
        };
    }
}

public sealed class AgentHitSoundFixtureRouteCoopCommand : ICoopCommand
{
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly INetwork network;

    public string Prefix => "coop.debug.battle";
    public string Name => "hit_sound_fixture_route";
    public string Description => "Routes one field-battle fixture action from the server to its client authority.";
    public CoopCommandSide Side => CoopCommandSide.Server;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("controller_id", "Connected field-battle participant."),
        new ExpectedArgs("request_id", "Unique fixture request id."),
        new ExpectedArgs("action", "begin or charge.")
    };

    public AgentHitSoundFixtureRouteCoopCommand(IPlayerManager players, IObjectManager objects,
        INetwork network)
    {
        this.players = players;
        this.objects = objects;
        this.network = network;
    }

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsClient || args.Count != 3 ||
            string.IsNullOrWhiteSpace(args[1]) ||
            (args[2] != "begin" && args[2] != "charge"))
            return Failed("Run controller_id, unique request_id and begin or charge on the server.");
        if (!ContainerProvider.TryResolve<IMissionMembershipRegistry>(out var membership))
            return Failed("Mission membership is unavailable on the server.");

        if (!players.TryGetPlayer(args[0], out var player) ||
            !players.TryGetPeer(args[0], out var peer) || peer == null ||
            peer.ConnectionState != ConnectionState.Connected ||
            !players.TryGetPlayer(peer, out var boundPlayer) || !ReferenceEquals(player, boundPlayer) ||
            !membership.IsControllerInMission(args[0]) ||
            !objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party) ||
            party.MapEvent?.IsFieldBattle != true ||
            !objects.TryGetId(party.MapEvent, out string mapEventId))
            return Failed("The controller must be a connected participant in a current field battle.");

        network.Send(peer, new NetworkAgentHitSoundFixtureRequest(mapEventId, args[0], args[1], args[2]));
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            status = "routed_outcome_pending", mapEventId, controllerId = args[0],
            requestId = args[1], action = args[2]
        }));
    }

    private static CoopCommandResult Failed(string message) =>
        new CoopCommandResult(false, message, "command_failed");
}

public sealed class AgentHitSoundFixtureStateCoopCommand : ICoopCommand
{
    private readonly IAgentHitSoundFixtureHandler handler;

    public AgentHitSoundFixtureStateCoopCommand(IAgentHitSoundFixtureHandler handler)
    {
        this.handler = handler;
    }

    public string Prefix => "coop.debug.battle";
    public string Name => "hit_sound_fixture_state";
    public string Description => "Reads the last server-routed hit-sound fixture action result.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args) =>
        ModInformation.IsClient && args.Count == 0
            ? new CoopCommandResult(true, "LIVE_TEST_JSON=" + handler.Snapshot())
            : new CoopCommandResult(false, "Run without arguments on a client.", "command_failed");
}
#endif
