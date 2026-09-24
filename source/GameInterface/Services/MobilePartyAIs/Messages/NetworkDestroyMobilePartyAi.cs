using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobilePartyAIs.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkDestroyMobilePartyAi : ICommand
{
    public NetworkDestroyMobilePartyAi(uint mobilePartyAiId)
    {
        MobilePartyAiId = mobilePartyAiId;
    }

    [ProtoMember(1)]
    public uint MobilePartyAiId { get; }
}
