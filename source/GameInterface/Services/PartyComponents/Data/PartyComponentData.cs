using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Data;

[ProtoContract(SkipConstructor = true)]
public record PartyComponentData(int TypeIndex, string Id, uint Handle)
{
    [ProtoMember(1)]
    public int TypeIndex = TypeIndex;

    [ProtoMember(2)]
    public string Id { get; } = Id;

    [ProtoMember(3)]
    public uint Handle { get; } = Handle;

    /// <summary>
    /// Optional constructor Settlement for party components whose home is stored in a field.
    /// Bundled with creation so the client can restore the field before the component is used.
    /// </summary>
    [ProtoMember(4)]
    public uint HomeSettlementId { get; set; }

    /// <summary>
    /// Optional: the IsNaval flag for a <c>PatrolPartyComponent</c>.
    /// False for all other component types.
    /// </summary>
    [ProtoMember(5)]
    public bool IsNaval { get; set; } = false;

    /// <summary>
    /// Optional: the full name of a module subclass of the <see cref="TypeIndex"/> type, so the client creates the same type.
    /// Null when the component is exactly the <see cref="TypeIndex"/> type.
    /// </summary>
    [ProtoMember(6)]
    public string ConcreteTypeName { get; set; }
}
