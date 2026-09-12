using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Data;

[ProtoContract(SkipConstructor = true)]
public record PartyComponentData(int TypeIndex, string Id)
{
    [ProtoMember(1)]
    public int TypeIndex = TypeIndex;

    [ProtoMember(2)]
    public string Id { get; } = Id;

    /// <summary>
    /// Optional constructor Settlement for party components whose home is stored in a field.
    /// Bundled with creation so the client can restore the field before the component is used.
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
