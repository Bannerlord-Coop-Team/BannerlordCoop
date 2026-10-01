using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyVisuals.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkCreatePartyVisual : ICommand
    {
        [ProtoMember(1)]
        public string PartyVisualId { get; }

        [ProtoMember(2)]
        public uint PartyVisualHandle { get; }

        [ProtoMember(3)]
        public uint MobilePartyHandle { get; }

        public NetworkCreatePartyVisual(
            string partyVisualId,
            uint partyVisualHandle,
            uint mobilePartyHandle)
        {
            PartyVisualId = partyVisualId;
            PartyVisualHandle = partyVisualHandle;
            MobilePartyHandle = mobilePartyHandle;
        }
    }
}
