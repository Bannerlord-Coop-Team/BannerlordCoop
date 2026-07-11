using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyRemovedSupporter : ICommand
{
    [ProtoMember(1)]
    public readonly string NotableId;

    [ProtoMember(2)]
    public readonly string SupportedClanId;

    public NetworkNotifyRemovedSupporter(
        string notableId,
        string supportedClanId)
    {
        NotableId = notableId;
        SupportedClanId = supportedClanId;
    }
}
