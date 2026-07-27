using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyHeroJoinedParty : ICommand
{
    [ProtoMember(1)]
    public readonly string NewPartyId;

    [ProtoMember(2)]
    public readonly string CompanionId;

    public NetworkNotifyHeroJoinedParty(
        string newPartyId,
        string companionId)
    {
        NewPartyId = newPartyId;
        CompanionId = companionId;
    }
}
