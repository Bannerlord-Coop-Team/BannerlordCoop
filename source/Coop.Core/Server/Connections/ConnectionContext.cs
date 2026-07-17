using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.CoopSessionData;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;

namespace Coop.Core.Server.Connections;

/// <summary>
/// Aggregates the services shared across the connection states so each state can be created with
/// <c>new</c> instead of being resolved from the DI container. Registered once and threaded through
/// the connection state machine by <see cref="ConnectionLogic"/>.
/// </summary>
public class ConnectionContext
{
    public ConnectionContext(
        IMessageBroker messageBroker,
        INetwork network,
        IModuleValidator moduleValidator,
        IModuleInfoProvider moduleInfoProvider,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IHeroInterface heroInterface,
        ICoopSessionProvider coopSessionProvider,
        ISaveInterface saveInterface,
        IConnectionMessageQueue connectionMessageQueue,
        ISendCoalescer coalescer,
        IAttachmentIdMapper attachmentIdMapper,
        IExistingPlayerSender existingPlayerSender)
    {
        MessageBroker = messageBroker;
        Network = network;
        ModuleValidator = moduleValidator;
        ModuleInfoProvider = moduleInfoProvider;
        PlayerManager = playerManager;
        ObjectManager = objectManager;
        HeroInterface = heroInterface;
        CoopSessionProvider = coopSessionProvider;
        SaveInterface = saveInterface;
        ConnectionMessageQueue = connectionMessageQueue;
        Coalescer = coalescer;
        AttachmentIdMapper = attachmentIdMapper;
        ExistingPlayerSender = existingPlayerSender;
    }

    public IMessageBroker MessageBroker { get; }
    public INetwork Network { get; }
    public IModuleValidator ModuleValidator { get; }
    public IModuleInfoProvider ModuleInfoProvider { get; }
    public IPlayerManager PlayerManager { get; }
    public IObjectManager ObjectManager { get; }
    public IHeroInterface HeroInterface { get; }
    public ICoopSessionProvider CoopSessionProvider { get; }
    public ISaveInterface SaveInterface { get; }
    public IConnectionMessageQueue ConnectionMessageQueue { get; }
    public ISendCoalescer Coalescer { get; }
    public IAttachmentIdMapper AttachmentIdMapper { get; }
    public IExistingPlayerSender ExistingPlayerSender { get; }
}
