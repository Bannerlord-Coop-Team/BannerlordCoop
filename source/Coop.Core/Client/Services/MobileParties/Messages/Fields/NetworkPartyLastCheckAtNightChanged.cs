using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

[ProtoContract(SkipConstructor = true)]
public record NetworkPartyLastCheckAtNightChanged(bool PartyLastCheckAtNight, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public bool PartyLastCheckAtNight { get; } = PartyLastCheckAtNight;
    [ProtoMember(2)]
    public string MobilePartyId { get; } = MobilePartyId;
}