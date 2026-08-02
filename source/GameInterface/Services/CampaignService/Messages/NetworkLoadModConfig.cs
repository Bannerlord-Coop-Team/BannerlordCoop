using Common.Messaging;
using GameInterface.Configuration;
using ProtoBuf;

namespace GameInterface.Services.CampaignService.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestServerModConfig : ICommand { }

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkLoadModConfig : ICommand
{
    [ProtoMember(1)]
    public readonly ModOptions ModOptions;

    public NetworkLoadModConfig(ModOptions modOptions)
    {
        ModOptions = modOptions;
    }
}
