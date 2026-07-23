using Common.Messaging;
using GameInterface.Services.CampaignService.Data;
using ProtoBuf;

namespace GameInterface.Services.CampaignService.Messages;

public readonly struct UpdateOtherOptions : IEvent { }

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateOtherOptions : ICommand
{
    [ProtoMember(1)]
    public readonly ServerOptions ServerOptions;

    public NetworkUpdateOtherOptions(ServerOptions serverOptions)
    {
        ServerOptions = serverOptions;
    }
}