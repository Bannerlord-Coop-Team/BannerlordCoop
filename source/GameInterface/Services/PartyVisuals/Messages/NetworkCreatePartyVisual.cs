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

        // Headless servers identify client-only visuals by their owning party, without a visual registration.
        public NetworkCreatePartyVisual(uint mobilePartyHandle)
            : this(null, 0, mobilePartyHandle)
        {
        }

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
