using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyVisuals.Messages
{
    /// <summary>Creates a map visual for the party identified by its handle.</summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkCreatePartyVisual : ICommand
    {
        // Tags 1 and 2 belonged to the retired visual id and handle.
        [ProtoMember(3)]
        public uint MobilePartyHandle { get; }

        public NetworkCreatePartyVisual(uint mobilePartyHandle)
        {
            MobilePartyHandle = mobilePartyHandle;
        }
    }
}
