using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyTributePaymentEnded : ICommand
{
    public readonly string ClanId;
    public readonly string PayerFactionId;

    public NetworkNotifyTributePaymentEnded(
        string clanId,
        string payerFactionId)
    {
        ClanId = clanId;
        PayerFactionId = payerFactionId;
    }
}
