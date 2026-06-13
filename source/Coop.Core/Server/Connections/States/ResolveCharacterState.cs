using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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
    private readonly IModuleInfoProvider moduleInfoProvider;
    public ResolveCharacterState(IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        IModuleValidator moduleValidator,
        IPlayerManager playerManager,
        IModuleInfoProvider moduleInfoProvider) 
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.moduleValidator = moduleValidator;
        this.playerManager = playerManager;
        this.moduleInfoProvider = moduleInfoProvider;

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
        var clientModules = obj.What.Modules;
        var serverModules = moduleInfoProvider.GetModuleInfos();

        var result = moduleValidator.Validate(serverModules, clientModules.Select(ConvertToModuleInfo), out var error);

        var validateMessage = new NetworkModuleVersionsValidated(result, error);
        var playerPeer = ConnectionLogic.Peer;
        network.Send(playerPeer, validateMessage);
    }

    internal void Handle_ClientValidate(MessagePayload<NetworkClientValidate> obj)
    {
        var peer = obj.Who as NetPeer;
        if (peer != ConnectionLogic.Peer) return;

        if (playerManager.TryGetPlayer(obj.What.PlayerId, out var player))
        {
            network.Send(peer, new NetworkClientValidated(true, player));
            ConnectionLogic.TransferSave();
        }
        else
        {
            network.Send(peer, new NetworkClientValidated(false, null));
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
