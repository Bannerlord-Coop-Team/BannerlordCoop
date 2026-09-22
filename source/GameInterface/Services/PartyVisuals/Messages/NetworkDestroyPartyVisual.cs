using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyVisuals.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkDestroyPartyVisual : ICommand
    {
        [ProtoMember(1)]
        public uint MobilePartyHandle { get; }

        public NetworkDestroyPartyVisual(uint mobilePartyHandle)
        {
            MobilePartyHandle = mobilePartyHandle;
        }
    }
}
