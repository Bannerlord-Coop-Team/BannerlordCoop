using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages.Data;

/// <summary>
/// Data required for adding and removing parties from the attached parties list
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record AttachedPartyData
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string ListPartyId { get; }

    public AttachedPartyData(string partyId, string removedPartyId)
    {
        PartyId = partyId;
        ListPartyId = removedPartyId;
    }
}
