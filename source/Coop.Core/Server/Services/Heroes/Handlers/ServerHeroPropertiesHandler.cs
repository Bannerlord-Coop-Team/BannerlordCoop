using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Handlers;
using Coop.Core.Server.Services.Heroes.Messages.Properties;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Messages.Properties;

namespace Coop.Core.Server.Services.Heroes.Handlers;

public class ServerHeroPropertiesHandler : AbstractServerHandler<HeroPropertiesHandler>
{
    public ServerHeroPropertiesHandler(IMessageBroker messageBroker, INetwork network) : base(messageBroker, network)
    {
        messageBroker.Subscribe<StaticBodyPropertiesChanged>(Handle);
    }

    private void Handle(MessagePayload<StaticBodyPropertiesChanged> payload)
    {
        var data = payload.What;
        
        messageBroker.Publish(this, new NetworkStaticBodyProperties(data.Id, data.Target, data.KeyParty1, data.KeyParty2, data.KeyParty3, data.KeyParty4, data.KeyParty5, data.KeyParty6, data.KeyParty7, data.KeyParty8));
    }

    protected override void Unsubscribe()
    {
        messageBroker.Unsubscribe<StaticBodyPropertiesChanged>(Handle);
    }
}