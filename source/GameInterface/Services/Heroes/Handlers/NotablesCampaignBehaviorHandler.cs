using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Buildings.Messages;
using GameInterface.Services.Heroes.Interfaces;
using Serilog;

namespace GameInterface.Services.Heroes.Handlers;

internal class NotablesCampaignBehaviorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NotablesCampaignBehaviorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IHeroRelationsInterface heroRelationsInterface;

    public NotablesCampaignBehaviorHandler(IMessageBroker messageBroker, IHeroRelationsInterface heroRelationsInterface)
    {
        this.messageBroker = messageBroker;
        this.heroRelationsInterface = heroRelationsInterface;
        messageBroker.Subscribe<UpdateNotableRelations>(Handle_UpdateNotableRelations);
        messageBroker.Subscribe<UpdateNotableSupport>(Handle_UpdateNotableSupport);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateNotableRelations>(Handle_UpdateNotableRelations);
        messageBroker.Unsubscribe<UpdateNotableSupport>(Handle_UpdateNotableSupport);
    }

    private void Handle_UpdateNotableRelations(MessagePayload<UpdateNotableRelations> obj)
    {
        heroRelationsInterface.UpdateNotableRelations(obj.What.Notable);
    }

    private void Handle_UpdateNotableSupport(MessagePayload<UpdateNotableSupport> obj)
    {
        if (obj.What.Notable.SupporterOf == null) return;

        heroRelationsInterface.UpdateNotableSupport(obj.What.Notable);
    }
}