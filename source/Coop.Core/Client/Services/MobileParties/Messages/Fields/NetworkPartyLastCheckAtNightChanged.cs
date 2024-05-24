using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

[ProtoContract(SkipConstructor = true)]
public record NetworkPartyLastCheckAtNightChanged(bool PartyLastCheckAtNight, string MobilePartyId) : ICommand
{
    public bool PartyLastCheckAtNight { get; } = PartyLastCheckAtNight;
    public string MobilePartyId { get; } = MobilePartyId;
}