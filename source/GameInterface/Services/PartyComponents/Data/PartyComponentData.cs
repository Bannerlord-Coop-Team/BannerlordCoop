using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Data;

[ProtoContract(SkipConstructor = true)]
public record PartyComponentData(int TypeIndex, string Id, string MobilePartyId)
{
    [ProtoMember(1)]
    public int TypeIndex = TypeIndex;

    [ProtoMember(2)]
    public string Id { get; } = Id;

    public string MobilePartyId { get; } = MobilePartyId;

    /// <summary>
    /// Optional: the StringId of the home Settlement for a <c>PatrolPartyComponent</c>.
    /// Null for all other component types. Bundled with creation so the client can call
    /// <c>InitializePartyComponentProperties</c> immediately without waiting for a
    /// separate DynamicSync field message (which may arrive in any order).
    /// </summary>
    [ProtoMember(3)]
    public string HomeSettlementId { get; set; } = null;

    /// <summary>
    /// Optional: the IsNaval flag for a <c>PatrolPartyComponent</c>.
    /// False for all other component types.
    /// </summary>
    [ProtoMember(4)]
    public bool IsNaval { get; set; } = false;
}
