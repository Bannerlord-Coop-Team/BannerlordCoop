using Common.Messaging;
using GameInterface.Configuration;
using ProtoBuf;

namespace GameInterface.Services.CampaignService.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestServerModConfig : ICommand { }

/// <summary>Server → client: the options the host resolved from its config, pushed verbatim so every
/// client runs on the host's values rather than its own file.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkLoadModConfig : IEvent
{
    [ProtoMember(1)]
    public readonly ModOptions ModOptions;

    public NetworkLoadModConfig(ModOptions modOptions)
    {
        ModOptions = modOptions;
    }
}
