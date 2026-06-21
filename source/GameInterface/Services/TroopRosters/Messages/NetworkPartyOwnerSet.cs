using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Broadcast from the server to assign a <c>TroopRoster.OwnerParty</c> back-reference on every client.
/// <see cref="OwnerPartyId"/> is null when the roster has no owner party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkPartyOwnerSet(string RosterId, string OwnerPartyId) : ICommand
{
    [ProtoMember(1)]
    public string RosterId { get; } = RosterId;
    [ProtoMember(2)]
    public string OwnerPartyId { get; } = OwnerPartyId;
}
