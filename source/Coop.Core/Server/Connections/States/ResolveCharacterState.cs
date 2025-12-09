using System.Linq;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.GameDebug.Messages;
using LiteNetLib;
using Serilog;
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
    private readonly IModuleInfoProvider moduleInfoProvider;
    public ResolveCharacterState(IConnectionLogic connectionLogic,
        IMessageBroker messageBroker,
        INetwork network,
        IModuleValidator moduleValidator,
        IModuleInfoProvider moduleInfoProvider) 
        : base(connectionLogic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.moduleValidator = moduleValidator;
        this.moduleInfoProvider = moduleInfoProvider;

        messageBroker.Subscribe<NetworkClientValidate>(ClientValidateHandler);
        messageBroker.Subscribe<HeroResolved>(ResolveHeroHandler);
        messageBroker.Subscribe<ResolveHeroNotFound>(HeroNotFoundHandler);
        messageBroker.Subscribe<NetworkModuleVersionsValidate>(ModuleVersionsValidateHandler);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<NetworkClientValidate>(ClientValidateHandler);
        messageBroker.Unsubscribe<HeroResolved>(ResolveHeroHandler);
        messageBroker.Unsubscribe<ResolveHeroNotFound>(HeroNotFoundHandler);
        messageBroker.Unsubscribe<NetworkModuleVersionsValidate>(ModuleVersionsValidateHandler);
    }

    internal void ModuleVersionsValidateHandler(MessagePayload<NetworkModuleVersionsValidate> obj)
    {
        var clientModules = obj.What.Modules;
        var serverModules = moduleInfoProvider.GetModuleInfos();

        Logger.Information("Validating modules: client={ClientCount} server={ServerCount}", clientModules.Length, serverModules.Count);
        var result = moduleValidator.Validate(serverModules, clientModules.Select(ConvertToModuleInfo).ToList());
        if (result == null)
        {
            Logger.Information("Module validation succeeded");
        }
        else
        {
            Logger.Warning("Module validation failed: {Reason}", result);
        }

        var validateMessage = new NetworkModuleVersionsValidated(result == null, result);
        var playerPeer = ConnectionLogic.Peer;
        network.Send(playerPeer, validateMessage);
    }

    internal void ClientValidateHandler(MessagePayload<NetworkClientValidate> obj)
    {
        var peer = obj.Who as NetPeer;
        if (peer != ConnectionLogic.Peer) return;

        messageBroker.Publish(this, new SendInformationMessage("Validation client reçue, démarrage transfert (bypass héros)"));
        var validateMessage = new NetworkClientValidated(true, string.Empty);
        network.Send(peer, validateMessage);
        ConnectionLogic.TransferSave();
    }

    internal void ResolveHeroHandler(MessagePayload<HeroResolved> obj)
    {
        messageBroker.Publish(this, new SendInformationMessage("Validation client OK, transfert sauvegarde"));
        var validateMessage = new NetworkClientValidated(true, obj.What.HeroId);
        var playerPeer = ConnectionLogic.Peer;
        network.Send(playerPeer, validateMessage);
        ConnectionLogic.TransferSave();
    }

    internal void HeroNotFoundHandler(MessagePayload<ResolveHeroNotFound> obj)
    {
        messageBroker.Publish(this, new SendInformationMessage("Aucun héros trouvé, création de personnage"));
        var validateMessage = new NetworkClientValidated(false, string.Empty);
        var playerPeer = ConnectionLogic.Peer;
        network.Send(playerPeer, validateMessage);
        ConnectionLogic.CreateCharacter();
    }

    public override void CreateCharacter()
    {
        ConnectionLogic.SetState<CreateCharacterState>();
    }

    public override void TransferSave()
    {
        ConnectionLogic.SetState<TransferSaveState>();
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
            Version = new ApplicationVersion((ApplicationVersionType)networkModuleInfo.Version.ApplicationVersionType, networkModuleInfo.Version.Major, networkModuleInfo.Version.Minor, networkModuleInfo.Version.Revision, networkModuleInfo.Version.ChangeSet)
        };
    }
}
