using Common.Network;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Instances.Sessions;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// First state of a connection to a campaign-free session server. Module validation and the ban check are
/// inherited; resolving the character registers a player with no hero or party, sends the session
/// definition and moves to <see cref="MissionState"/>. It never transfers a save, so the connection queue
/// would drop every later send to this peer; the accepted join marks the peer live.
/// </summary>
public class SessionJoinState : ResolveCharacterState
{
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ISessionDefinitionStore sessionDefinitions;
    private readonly IConnectionMessageQueue connectionMessageQueue;

    public SessionJoinState(IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        IModuleValidator moduleValidator,
        IPlayerManager playerManager,
        IPlayerPartyRestorer playerPartyRestorer,
        IObjectManager objectManager,
        IModuleInfoProvider moduleInfoProvider,
        IModuleNameProvider moduleNameProvider,
        IExistingPlayerSender existingPlayerSender,
        ISteamBanList steamBanList,
        IJoinValidationDenialLog denialLog,
        ISessionDefinitionStore sessionDefinitions,
        IConnectionMessageQueue connectionMessageQueue)
        : base(connectionLogic, messageBroker, network, moduleValidator, playerManager, playerPartyRestorer, objectManager,
            moduleInfoProvider, moduleNameProvider, existingPlayerSender, steamBanList, denialLog)
    {
        this.network = network;
        this.playerManager = playerManager;
        this.sessionDefinitions = sessionDefinitions;
        this.connectionMessageQueue = connectionMessageQueue;
    }

    protected override void ResolveCharacter(NetPeer peer, string controllerId)
    {
        var definition = sessionDefinitions.Current;
        if (definition == null)
        {
            network.SendImmediate(peer, new NetworkSessionJoined(false, "The co-op session has ended", null, null, null, null));
            return;
        }

        if (!playerManager.TryGetPlayer(controllerId, out var player))
        {
            player = new Player(controllerId, null, null, null, null);
            playerManager.AddPlayer(player);
        }

        playerManager.SetPeer(controllerId, peer);
        network.SendImmediate(peer, new NetworkSessionJoined(true, null, definition.SessionId, definition.ModId, definition.Payload, player));
        connectionMessageQueue.MarkLive(peer);
        ConnectionLogic.EnterMission();
    }

    public override void EnterMission()
    {
        ConnectionLogic.SetState<MissionState>();
    }
}
