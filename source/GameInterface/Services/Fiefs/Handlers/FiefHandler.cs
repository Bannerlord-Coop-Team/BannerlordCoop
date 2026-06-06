using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
//using GameInterface.Services.Fiefs.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Fiefs.Handlers;
/// <summary>
/// Lifetime handler for <see cref="Fief"/> objects.
/// </summary>
internal class FiefHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<FiefHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    public FiefHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        // TODO: Add messageBroker subscriptions
    }

    public void Dispose()
    {
        // TODO: Add messageBroker unsubscribing
    }
}