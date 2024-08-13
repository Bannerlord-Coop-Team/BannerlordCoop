using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Handlers;
using Coop.Core.Server.Services.Heroes.Messages.Properties;
using GameInterface.Common.Commands;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Messages.Properties;

namespace Coop.Core.Client.Services.Heroes.Handlers;

public class ClientHeroPropertiesHandler : AbstractClientHandler<HeroPropertiesHandler>
{
    public ClientHeroPropertiesHandler(IMessageBroker messageBroker) : base(messageBroker)
    {
        messageBroker.Subscribe<NetworkStaticBodyProperties>(Handle);
    }
    
    private void Handle(MessagePayload<NetworkStaticBodyProperties> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new ChangeStaticBodyProperties(data.Id, data.Target, data.KeyParty1, data.KeyParty2, data.KeyParty3, data.KeyParty4, data.KeyParty5, data.KeyParty6, data.KeyParty7, data.KeyParty8));
    }

    protected override void Unsubscribe()
    {
        messageBroker.Unsubscribe<NetworkStaticBodyProperties>(Handle);
    }
}