using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyComponents.Messages;

/// <summary>
/// Broadcast from the server to assign a <c>LordPartyComponent.Owner</c> on every client.
/// <see cref="OwnerId"/> is null when the component has no owner.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkLordPartyOwnerChanged(string LordPartyComponentId, string OwnerId) : ICommand
{
    [ProtoMember(1)]
    public string LordPartyComponentId { get; } = LordPartyComponentId;
    [ProtoMember(2)]
    public string OwnerId { get; } = OwnerId;
}
