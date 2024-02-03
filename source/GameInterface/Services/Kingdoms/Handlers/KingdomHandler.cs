using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Handlers;

/// <summary>
/// Handler for <see cref="Kingdom"/> messages
/// </summary>
public class KingdomHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public KingdomHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
    }

    public void Dispose()
    {
    }
}
