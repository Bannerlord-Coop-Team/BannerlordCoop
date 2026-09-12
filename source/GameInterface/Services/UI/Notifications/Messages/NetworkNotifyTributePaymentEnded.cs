using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyTributePaymentEnded : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly string PayerFactionId;

    public NetworkNotifyTributePaymentEnded(
        string clanId,
        string payerFactionId)
    {
        ClanId = clanId;
        PayerFactionId = payerFactionId;
    }
}
