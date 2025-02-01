using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobilePartyAIs.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkDestroyMobilePartyAi : ICommand
{
    public NetworkDestroyMobilePartyAi(string mobilePartyAiId)
    {
        MobilePartyAiId = mobilePartyAiId;
    }

    [ProtoMember(1)]
    public string MobilePartyAiId { get; }
}
