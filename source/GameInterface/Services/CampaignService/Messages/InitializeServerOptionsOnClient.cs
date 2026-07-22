using Common.Messaging;
using GameInterface.Services.CampaignService.Data;

namespace GameInterface.Services.CampaignService.Messages;

public class InitializeServerOptionsOnClient : IEvent
{
    public readonly ServerOptions ServerOptions;

    public InitializeServerOptionsOnClient(ServerOptions serverOptions)
    {
        ServerOptions = serverOptions;
    }
}
