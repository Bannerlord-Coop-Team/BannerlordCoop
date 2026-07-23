using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Core.Server.Connections.States;

/// <summary>
/// State representing a connection determining if a character already
/// exists for this connection
/// </summary>
public class ResolveCharacterState : ConnectionStateBase
{
    private static readonly ILogger Logger = LogManager.GetLogger<ResolveCharacterState>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IModuleValidator moduleValidator;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly IModuleInfoProvider moduleInfoProvider;
    private readonly IExistingPlayerSender existingPlayerSender;

    public ResolveCharacterState(IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        IModuleValidator moduleValidator,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IModuleInfoProvider moduleInfoProvider,
        IExistingPlayerSender existingPlayerSender)
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.moduleValidator = moduleValidator;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.moduleInfoProvider = moduleInfoProvider;
        this.existingPlayerSender = existingPlayerSender;

        messageBroker.Subscribe<NetworkClientValidate>(Handle_ClientValidate);
        messageBroker.Subscribe<NetworkModuleVersionsValidate>(Handle_ModuleVersionsValidate);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkClientValidate>(Handle_ClientValidate);
        messageBroker.Unsubscribe<NetworkModuleVersionsValidate>(Handle_ModuleVersionsValidate);
    }

    internal void Handle_ModuleVersionsValidate(MessagePayload<NetworkModuleVersionsValidate> obj)
    {
        // Same guard as Handle_ClientValidate: every connection in this state receives every
        // client's broadcastable messages, so without it each concurrent joiner would also be
        // answered with a result computed from another client's module list.
        var peer = obj.Who as NetPeer;
        if (peer != ConnectionLogic.Peer) return;

        bool result;
        string error;
        try
        {
            var clientModules = obj.What.Modules;
            var serverModules = moduleInfoProvider.GetModuleInfos();

            result = moduleValidator.Validate(serverModules, clientModules.Select(ConvertToModuleInfo), out error);
        }
        catch (Exception e)
        {
            // A throw here used to die in the network poller, so the client never received an
            // answer and sat on the "Validating modules..." loading screen forever. Answer with a
            // denial instead so the joiner gets a visible reason.
            Logger.Error(e, "Module validation threw for peer {Peer}", ConnectionLogic.Peer?.Id);
            result = false;
            error = $"The server failed to validate the module list ({e.GetType().Name}). " +
                    "Check that the client and server run the same game and mod versions.";
        }

        var validateMessage = new NetworkModuleVersionsValidated(result, error);
        network.SendImmediate(ConnectionLogic.Peer, validateMessage);
    }

    internal void Handle_ClientValidate(MessagePayload<NetworkClientValidate> obj)
    {
        var peer = obj.Who as NetPeer;
        if (peer != ConnectionLogic.Peer) return;

        if (playerManager.TryGetPlayer(obj.What.PlayerId, out var player) &&
            objectManager.TryGetObjectWithLogging(player.HeroId, out Hero _)) // If new save, player hero will not exist
        {
            // This peer is a new NetPeer for an already registered player, so the
            // peer-Player link must be established here
            playerManager.SetPeer(obj.What.PlayerId, peer);
            network.SendImmediate(peer, new NetworkClientValidated(true, player));
            ConnectionLogic.TransferSave();

            existingPlayerSender.SendExistingPlayers(peer, obj.What.PlayerId);
        }
        else
        {
            network.SendImmediate(peer, new NetworkClientValidated(false, null));
            ConnectionLogic.CreateCharacter();
        }
    }

    public override void CreateCharacter()
    {
        ConnectionLogic.SetState<CreateCharacterState>();
    }

    public override void TransferSave()
    {
        // SetState packages and sends the save synchronously; then move to LoadingState to await the
        // client reporting it has entered the campaign. Load() must run here (not inside the
        // TransferSaveState ctor) so it resolves against TransferSaveState, not this state.
        ConnectionLogic.SetState<TransferSaveState>();
        ConnectionLogic.Load();
    }

    public override void Load()
    {
    }

    public override void EnterCampaign()
    {
    }

    public override void EnterMission()
    {
    }

    private static ModuleInfo ConvertToModuleInfo(NetworkModuleInfo networkModuleInfo)
    {
        return new ModuleInfo()
        {
            Id = networkModuleInfo.Id,
            IsOfficial = networkModuleInfo.IsOfficial,
            IsDlc = networkModuleInfo.IsDlc,
            Version = new ApplicationVersion((ApplicationVersionType)networkModuleInfo.Version.ApplicationVersionType, networkModuleInfo.Version.Major, networkModuleInfo.Version.Minor, networkModuleInfo.Version.Revision, networkModuleInfo.Version.ChangeSet)
        };
    }
}
