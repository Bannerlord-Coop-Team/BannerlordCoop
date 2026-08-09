using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkOnClanSupported : ICommand
{
    [ProtoMember(1)]
    public readonly string SupporterClanId;
    [ProtoMember(2)]
    public readonly string SupportedClanId;

    public NetworkOnClanSupported(string supporterClanId, string supportedClanId)
    {
        SupporterClanId = supporterClanId;
        SupportedClanId = supportedClanId;
    }
}
