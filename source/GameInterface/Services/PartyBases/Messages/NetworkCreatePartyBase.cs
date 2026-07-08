using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyBases.Messages;

/// <summary>
/// Replaces the generic <c>NetworkCreateInstance&lt;PartyBase&gt;</c>: carries the owning
/// MobileParty's id so the client can adopt its locally-constructed PartyBase under the
/// server's id instead of skip-constructing a duplicate shell.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCreatePartyBase : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyBaseId;

    /// <summary>Owning MobileParty id; null for settlement-owned or ownerless PartyBases.</summary>
    [ProtoMember(2)]
    public readonly string OwnerMobilePartyId;

    public NetworkCreatePartyBase(string partyBaseId, string ownerMobilePartyId)
    {
        PartyBaseId = partyBaseId;
        OwnerMobilePartyId = ownerMobilePartyId;
    }
}
