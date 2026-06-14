using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Registry;
using GameInterface.Registry.Messages;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using Serilog;

namespace Coop.Core.Server.States;

/// <summary>
/// State represting the server has just started
/// </summary>
public class InitialServerState : ServerStateBase
{
    private static readonly ILogger Logger = LogManager.GetLogger<InitialServerState>();

    private readonly IMessageBroker messageBroker;
    private readonly IRegistryManager registryManager;
    private readonly IModuleValidator moduleValidator;
    private readonly IDebugGameInterface debugGameInterface;
    private readonly IModuleInfoProvider moduleInfoProvider;

    public InitialServerState(
        IServerLogic context,
        IMessageBroker messageBroker,
        IRegistryManager registryManager,
        IModuleValidator moduleValidator,
        IDebugGameInterface debugGameInterface,
        IModuleInfoProvider moduleInfoProvider) :
        base(context)
    {
        this.messageBroker = messageBroker;
        this.registryManager = registryManager;
        this.moduleValidator = moduleValidator;
        this.debugGameInterface = debugGameInterface;
        this.moduleInfoProvider = moduleInfoProvider;
        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);
    }

    internal void Handle_CampaignReady(MessagePayload<CampaignReady> payload)
    {
        // Coop does not support DLC. Warn the host if any is enabled before the server becomes
        // joinable; clients with DLC enabled are rejected separately during module validation.
        if (!moduleValidator.ValidateNoDlc(moduleInfoProvider.GetModuleInfos(), out var dlcError))
        {
            Logger.Error("Hosting with unsupported modules enabled. {error}", dlcError);
        }

        // Remove server party
        messageBroker.Publish(this, new RemoveMainParty());

        // Register all objects after main party is removed to keep order
        registryManager.RegisterAllGameObjects();
        registryManager.PatchLifetimes();

        Logic.SetState<ServerRunningState>();
    }

    public override void Start()
    {
#if DEBUG
        debugGameInterface.LoadDebugGame();
#endif
    }

    public override void Stop()
    {
    }
}
